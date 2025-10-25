using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MusicRpc.Win32Api;

/// <summary>
/// 提供在目标进程中进行内存扫描的功能。
/// </summary>
internal static class Memory
{
    // 缓存模块的内存块，避免重复读取同一个模块的整个 .text 节，提升重复扫描的性能。
    private static readonly Dictionary<(int, nint), (nint pStart, byte[] memory)> ModuleCache = new();
    private static int _lastProcessId = -1;

    /// <summary>
    /// 在指定进程的模块中查找内存特征码 (Pattern/AOB)。
    /// </summary>
    /// <param name="pattern">要查找的特征码字符串，例如 "48 8D 0D ? ? ? ?"。</param>
    /// <param name="processId">目标进程的ID。</param>
    /// <param name="moduleBaseAddress">模块的基地址。</param>
    /// <param name="pointer">如果找到，此参数将返回匹配位置的内存地址。</param>
    /// <returns>如果成功找到特征码，返回 true。</returns>
    public static bool FindPattern(string pattern, int processId, nint moduleBaseAddress, out nint pointer)
    {
        if (processId != _lastProcessId)
        {
            ModuleCache.Clear();
            _lastProcessId = processId;
        }

        if (!ModuleCache.TryGetValue((processId, moduleBaseAddress), out (nint pStart, byte[] memoryBlock) cacheEntry))
        {
            var memory = new ProcessMemory(processId);

            try
            {
                var ntOffset = memory.ReadInt32(moduleBaseAddress, 0x3C);
                var ntHeader = moduleBaseAddress + ntOffset;

                var fileHeader = ntHeader + 4;
                var optHeader = fileHeader + 20;

                var sectionSize = memory.ReadInt16(fileHeader, 16);
                var sections = memory.ReadInt16(ntHeader, 6);
                var sectionHeader = optHeader + sectionSize;

                var cursor = sectionHeader;

                for (var i = 0; i < sections; i++)
                {
                    if (memory.ReadInt64(cursor) == 0x747865742E)
                    {
                        var pOffset = memory.ReadInt32(cursor, 12);
                        var pSize = memory.ReadInt32(cursor, 8);

                        cacheEntry.pStart = moduleBaseAddress + pOffset;
                        cacheEntry.memoryBlock = memory.ReadBytes(cacheEntry.pStart, pSize);

                        ModuleCache[(processId, moduleBaseAddress)] = cacheEntry;
                        break;
                    }

                    cursor += 40; // Size of IMAGE_SECTION_HEADER
                }
            }
            catch (Exception)
            {
                pointer = nint.Zero;
                return false;
            }
        }

        if (cacheEntry.memoryBlock is null)
        {
            pointer = nint.Zero;
            return false;
        }

        pointer = FindPattern(pattern, cacheEntry.pStart, cacheEntry.memoryBlock);

        return pointer != nint.Zero;
    }

    /// <summary>
    /// 在一个字节数组中查找特征码。
    /// </summary>
    private static nint FindPattern(string pattern, nint pStart, byte[] memoryBlock)
    {
        if (string.IsNullOrEmpty(pattern) || pStart == nint.Zero || memoryBlock.Length == 0)
        {
            return nint.Zero;
        }

        var patternBytes = ParseSignature(pattern);
        var firstByte = patternBytes[0];
        var searchRange = memoryBlock.Length - patternBytes.Length;

        for (var i = 0; i < searchRange; i++)
        {
            if (firstByte != 0xFFFF)
            {
                i = Array.IndexOf(memoryBlock, (byte)firstByte, i);
                if (i == -1)
                {
                    break;
                }
            }

            var found = true;
            for (var j = 1; j < patternBytes.Length; j++)
            {
                if (patternBytes[j] == 0xFFFF || patternBytes[j] == memoryBlock[i + j]) continue;
                found = false;
                break;
            }

            if (found)
            {
                return nint.Add(pStart, i);
            }
        }

        return nint.Zero;
    }

    /// <summary>
    /// 将特征码字符串 (例如 "48 8D ? ?") 解析为用于匹配的字节/通配符数组。
    /// </summary>
    private static ushort[] ParseSignature(string signature)
    {
        var bytesStr = signature.Split(' ');
        var bytes = new ushort[bytesStr.Length];

        for (var i = 0; i < bytes.Length; i++)
        {
            var str = bytesStr[i];
            if (str.Contains('?'))
            {
                bytes[i] = 0xFFFF;
            }
            else
            {
                bytes[i] = Convert.ToByte(str, 16);
            }
        }

        return bytes;
    }
}

/// <summary>
/// 提供对目标进程内存进行读写操作的封装类。
/// </summary>
internal sealed partial class ProcessMemory(nint process)
{
    // 这个构造函数链允许我们通过进程ID方便地创建实例。
    public ProcessMemory(int processId) : this(OpenProcess(
        0x0010, // PROCESS_VM_READ: 请求读取目标进程虚拟内存的权限
        false, // bInheritHandle: false 表示这个句柄不被子进程继承
        processId))
    {
    }

    public byte[] ReadBytes(IntPtr offset, int length)
    {
        var bytes = new byte[length];
        ReadProcessMemory(process, offset, bytes, length, IntPtr.Zero);

        return bytes;
    }

    public float ReadFloat(IntPtr address, int offset = 0)
        => BitConverter.ToSingle(ReadBytes(IntPtr.Add(address, offset), 4), 0);

    public double ReadDouble(IntPtr address, int offset = 0)
        => BitConverter.ToDouble(ReadBytes(IntPtr.Add(address, offset), 8), 0);

    public long ReadInt64(IntPtr address, int offset = 0)
        => BitConverter.ToInt64(ReadBytes(IntPtr.Add(address, offset), 8), 0);

    public ulong ReadUInt64(IntPtr address, int offset = 0)
        => BitConverter.ToUInt64(ReadBytes(IntPtr.Add(address, offset), 8), 0);

    public short ReadInt16(IntPtr address, int offset = 0)
        => BitConverter.ToInt16(ReadBytes(IntPtr.Add(address, offset), 2), 0);

    public int ReadInt32(IntPtr address, int offset = 0)
        => BitConverter.ToInt32(ReadBytes(IntPtr.Add(address, offset), 4), 0);

    public uint ReadUInt32(IntPtr address, int offset = 0)
        => BitConverter.ToUInt32(ReadBytes(IntPtr.Add(address, offset), 4), 0);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        [Out] byte[] lpBuffer,
        int nSize,
        IntPtr lpNumberOfBytesRead);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(
        int dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int dwProcessId);
}
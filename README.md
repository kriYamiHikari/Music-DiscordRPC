# Music Discord Rpc (Community Edition)

**将网易云音乐、QQ 音乐和洛雪音乐的播放状态实时同步到 Discord Rich Presence。**

<p align="center">
  <img src="https://github.com/user-attachments/assets/746d8a29-807e-4197-8b9a-562e14266b1e" width="450" alt="Discord RPC Preview" />
</p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/42f56f23-2cf9-4d5b-a165-b06caa76767d" width="400" alt="Settings UI 1" />
  &nbsp;&nbsp;
  <img src="https://github.com/user-attachments/assets/f5b66ce0-59bb-4fd0-89a9-9e5e5da93994" width="250" alt="Settings UI 2" />
</p>

---

> **⚠️ 注意：这是一个社区维护版本**
>
> 本项目基于 [Kxnrl (Kyle) 的原始作品](https://github.com/Kxnrl/NetEase-Cloud-Music-DiscordRPC) 进行二次开发。由于原项目已停止维护，本仓库将进行持续的功能更新、错误修复和性能优化。

### ✨ 主要功能 (Features)

-   ✅ **多平台支持**: 兼容最新版的网易云音乐、QQ 音乐和洛雪音乐 PC 客户端。
-   🎵 **实时同步**: 精准显示歌曲、歌手、专辑、封面和播放进度，支持多端同时显示。
-   📻 **模式兼容**: 支持网易云音乐的 FM 电台和漫游模式。
-   🔘 **自定义按钮**: 
    -   支持在 Discord 状态中添加最多 **2 个自定义按钮**。
    -   预设“一起听 (Listen)”功能，好友可直接跳转至歌曲页面 (**注：洛雪音乐暂不支持此功能**)。
    -   支持自定义按钮文字与链接。
-   🎨 **高度自定义**:
    -   **全新设置界面**: 提供完整的图形化配置窗口，支持实时应用更改。
    -   **显示选项**: 自由开关歌曲、歌手、专辑的 Emoji 图标。
    -   **状态优先**: 可选优先显示歌曲名、歌手名或播放器名称。
    -   **后台运行**: 支持开机自启、关闭到托盘、启动时最小化到托盘。

### 📥 安装与使用 (Installation)

1.  确保系统已安装 **[.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0)** 运行库。
2.  前往 **[Releases 页面](https://github.com/kriYamiHikari/Music-DiscordRPC/releases)** 下载最新版本的 `MusicRpc.exe`。
3.  运行程序，所有配置可通过右键托盘图标进入“显示设置”进行修改。
4.  **洛雪音乐用户请注意**:
    -   需在洛雪音乐的 **`设置 -> 开放 API`** 中，**启用开放 API 服务**。

### 🛠️ 技术特性 (Technical Details)

-   基于 **.NET 9** 开发，利用最新的 C# 特性与 AOT 兼容性优化。
-   使用 **Win32 API (`EmptyWorkingSet`)** 进行深层内存压缩。
-   **后台优化**：仅在程序处于后台或最小化时执行激进内存回收，确保前台交互流畅。

### 📜 许可证 (License)

本项目沿用原项目的许可证。详情请参阅 [LICENSE](LICENSE) 文件。

### 🙏 致谢 (Acknowledgements)

特别感谢 **Kyle (Kxnrl)** 创建了这个优秀的项目，为本项目奠定了坚实的基础。

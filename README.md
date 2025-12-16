# Music Discord Rpc (Community Edition)

**将网易云音乐、QQ 音乐和洛雪音乐的播放状态实时同步到 Discord Rich Presence。**  
Enables Discord [Rich Presence](https://discordapp.com/rich-presence) for Netease Cloud Music, Tencent QQ Music, and LX Music.

<div align="center">
  <img width="450" alt="Screenshot" src="https://github.com/user-attachments/assets/746d8a29-807e-4197-8b9a-562e14266b1e" />
</div>

---

> **⚠️ 注意：这是一个社区维护版本**
>
> 本项目基于 [Kxnrl (Kyle) 的原始作品](https://github.com/Kxnrl/NetEase-Cloud-Music-DiscordRPC)
> 进行二次开发。由于原项目已停止维护，本仓库将进行持续的功能更新、错误修复和社区支持。

### ✨ 主要功能 (Features)

-   ✅ **多平台支持**: 兼容最新版的网易云音乐、QQ 音乐和洛雪音乐 PC 客户端。
-   🎵 **实时同步**: 精准显示歌曲、歌手、专辑、封面和播放进度，可多端同时显示。
-   📻 **模式兼容**: 支持网易云音乐的 FM 电台和漫游模式。
-   🔗 **听歌互动**: 在 Discord 状态中生成“一起听”按钮，好友可直接点击跳转至歌曲页面 (洛雪音乐暂不支持此功能)。
-   🎨 **高度自定义**:
    -   通过托盘菜单，自由选择是否显示歌曲、歌手和专辑的 emoji 图标。
    -   自由切换状态显示模式（优先显示歌曲名或应用名）。
    -   所有设置均会自动保存。
-   🚀 **开机自启**: 可通过托盘菜单方便地设置是否开机自启，默认关闭。

### 📥 安装与使用 (Installation)

1.  确保系统已安装 **[.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0)** 运行库
2.  前往 **[Releases 页面](https://github.com/kriYamiHikari/Music-DiscordRPC/releases)**。
3.  下载最新版本的 `MusicRpc.exe` 应用程序。
4.  运行即可！程序将在系统托盘中显示一个图标，所有配置可通过右键托盘图标菜单修改。
5.  **洛雪音乐用户请注意**:
    -   为了让本程序能够识别到洛雪音乐，你需要先在洛雪音乐的 **`设置 -> 开放 API`** 中，**启用开放 API 服务**。

### 📜 许可证 (License)

本项目沿用原项目的许可证。详情请参阅 [LICENSE](LICENSE) 文件。

### 🙏 致谢 (Acknowledgements)

特别感谢 **Kyle (Kxnrl)** 创建了这个优秀的项目，为本项目奠定了坚实的基础。

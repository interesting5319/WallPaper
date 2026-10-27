# WallPaper

WallPaper 是一款面向 Windows 的轻量动态壁纸程序。它可以将本地视频作为桌面壁纸播放，为多台显示器分别指定壁纸，并在全屏应用或电池供电时自动暂停，以减少不必要的资源消耗。

程序使用 WPF `MediaElement` 和 Windows 自带的媒体解码能力播放视频，不内置第三方播放器或编解码器。项目目前为早期版本（1.0.0）。

项目开发过程中使用了 **MiMo** 进行 AI 辅助开发。

## 功能

- **多显示器独立设置**：为每台显示器分别选择一组壁纸视频，并调整播放顺序。
- **循环播放**：视频播放结束后自动从头循环。
- **失败自动跳过**：当前文件无法解码时尝试播放列表中的下一个文件；全部失败后可显示备用静态图片。
- **音频控制**：默认静音，可取消静音并调节音量。
- **节能暂停**：可设置在检测到全屏应用、D3D 全屏游戏、演示模式或电池供电时暂停视频。
- **系统托盘控制**：通过托盘菜单打开设置、暂停或恢复壁纸、管理自启动及退出程序。
- **开机自启动**：可在设置中启用或关闭。
- **显示器与桌面恢复处理**：显示器配置改变时重建壁纸窗口；Windows 资源管理器重启后尝试重新附着到桌面壁纸层。
- **故障恢复与日志**：记录运行日志；异常退出后尝试恢复运行，短时间内反复崩溃时会停止自动重启。

## 支持的文件格式

| 用途 | 文件扩展名 | 说明 |
| --- | --- | --- |
| 动态壁纸 | `.mp4` | 使用 Windows Media Foundation 解码。建议优先使用 **H.264 8-bit** 编码的视频；扩展名为 MP4 并不代表其中的编码一定受支持。 |
| 动态壁纸 | `.webm` | 依赖系统的 WebM/VP9 解码组件。缺少组件时，程序会提示安装 Microsoft Store 中的 **VP9 Video Extensions** 和/或 **Web Media Extensions**。 |
| 备用静态壁纸 | `.jpg`、`.jpeg`、`.png`、`.bmp` | 仅在壁纸视频列表为空或所有视频均无法播放时用作静态回退图片。 |

视频支持情况取决于 Windows 版本、系统媒体组件和视频内部使用的编码、位深及配置。程序不自带解码器；某些格式或编码即使扩展名在列表中，也可能无法播放。尤其是 **10-bit H.264** 等片源可能因系统解码不支持而失败，可尝试转换为 H.264 8-bit。

Windows N/KN 版本如果缺少 Media Foundation，视频壁纸将无法播放；请先安装 **Media Feature Pack**。WebM/VP9 也需要相应的系统扩展。

## 系统要求

- Windows 10 版本 1809（Build 17763）或更高版本。
- .NET 8 Windows Desktop Runtime。当前 Release 发布命令使用框架依赖部署，因此运行电脑需要安装相应运行时。
- 播放视频需要可用的 Windows Media Foundation 及对应编码的系统解码支持。

## 获取与运行

从 GitHub Releases 下载并解压发布包后，运行 `WallPaper.exe`。程序启动后会驻留在系统托盘；右键托盘图标为目标屏幕选择壁纸，双击托盘图标可打开设置。

首次使用：

1. 运行 `WallPaper.exe`。
2. 右键系统托盘中的 WallPaper 图标，在“为屏幕设置壁纸...”菜单中选择目标显示器并添加一个或多个视频。
3. 双击托盘图标打开设置，可调整各显示器的播放列表、静音与音量、暂停条件、开机自启动和备用静态图片。

添加多个视频时，程序会按列表顺序播放；播放失败时会尝试下一个视频。若没有可播放的视频且设置了备用图片，则显示该静态图片。

## 从源码构建

项目使用 .NET 8、WPF 和 Windows Forms。请在 Windows 上安装 .NET 8 SDK，然后在项目目录执行：

```powershell
dotnet build .\WallPaper.csproj -c Release
```

发布为依赖框架的 Windows x64 单文件程序：

```powershell
dotnet publish .\WallPaper.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\publish
```

发布产物位于 `publish` 目录。依赖框架的发布方式不会打包 .NET Desktop Runtime。

## 配置与日志

- Release 发布版将 `settings.json` 和日志保存在 `WallPaper.exe` 所在目录，适合便携使用；请确保该目录可写。
- 在 Visual Studio 中以 Debug 方式运行时，配置和日志位于 `%APPDATA%\WallPaper`。
- 程序也会在配置目录中保留配置备份，并在需要时尝试从旧配置位置迁移设置。
- 可从托盘菜单选择“打开配置目录”查看配置和日志。

## 优点与限制

### 优点

- 无需额外安装第三方播放器，直接使用 Windows 媒体播放能力。
- 支持多显示器分别设置壁纸及播放列表。
- 全屏和电池暂停、默认静音等选项有助于控制资源与电量消耗。
- 支持便携式 Release 配置、托盘操作、播放失败跳过和异常恢复。

### 当前限制

- **仅支持 Windows**，并使用 Windows 桌面和媒体组件。
- **视频格式支持受系统解码器限制**，不提供自带编解码器；MP4 或 WebM 容器本身不能保证任意内部编码均可播放。
- WebM/VP9 可能需要单独安装 Microsoft Store 扩展；Windows N/KN 可能需要 Media Feature Pack。
- 静态图片只用于失败回退，不支持图片轮播、动态图片或其他桌面主题功能。
- 壁纸依赖 Windows 桌面窗口层（WorkerW）挂接。若挂接失败，程序会降级为置底窗口运行，此时桌面图标可能被遮挡。
- 项目当前为早期版本，未包含安装程序或内置编解码器。

## 技术栈

- C# / .NET 8
- WPF 与 Windows Forms
- Windows Media Foundation（由 WPF `MediaElement` 使用）
- Windows API（桌面壁纸层挂接、显示器及系统状态检测）

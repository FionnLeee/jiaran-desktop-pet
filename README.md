# 嘉然 · 番茄炒蛋桌宠

经典常服的 Q 版嘉然，陪你写代码、学习和休息。这个仓库同时提供 **Codex 原生宠物包** 和 **Windows 独立桌面版源码**。

![嘉然动作预览](docs/actions.gif)

| 版本 | 适合谁 | 主要功能 |
| --- | --- | --- |
| [Codex 版](codex/README.md) | 使用支持本地宠物包的 Codex Desktop | 9 种动作、16 个视线方向，随宿主状态切换 |
| [Windows 桌面版](desktop/README.md) | 想在桌面上养嘉然 | 透明置顶、拖动、摸头、跳跃、喂糖、散步、睡觉、专注计时、可选 Codex 状态联动 |

## 下载和开始使用

前往 [Releases](https://github.com/FionnLeee/jiaran-desktop-pet/releases/latest) 下载：

- **`jiaran-codex-v3.zip`**：解压后运行 `install.ps1`，再到 Codex 的宠物设置刷新并选择嘉然。此包依赖宿主支持本地 `spriteVersionNumber: 2` 格式。
- **`jiaran-desktop-windows-x64.zip`**：Windows 10/11 x64，需 .NET Framework 4.8。解压整个目录，双击 `嘉然桌宠.exe`。桌面版可以单独使用。
- **`SHA256SUMS.txt`**：下载包的 SHA-256 校验值。

公开版默认不附带从官方视频裁切的声音文件。取得适用授权后，可按 [素材说明](ASSETS.md) 将自备音频放入 `desktop/audio/`；缺少音频时仍能正常使用桌宠。

## Windows 桌面版怎么玩

- 拖动移动位置；单击摸摸头；双击跳跃；右键打开互动与设置菜单。
- 右键菜单提供专注计时和互动；双击托盘图标找回桌宠位置。
- 支持 15 / 25 / 45 分钟专注、5 分钟休息，以及暂停、继续和结束。
- 可调尺寸、音量、静音、散步、显示状态和本地 Codex 联动。
- 设置保存在当前用户的本地应用数据目录，详细位置与卸载方法见 [桌面版说明](desktop/README.md)。

## 从源码编译

在 Windows PowerShell 中进入仓库根目录：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\desktop\src\build.ps1
& '.\desktop\嘉然桌宠.exe'
```

构建使用 Windows 的 .NET Framework C# 编译器和 WPF，不依赖 API Key、Node.js 或在线模型服务。源码、安装脚本与资源使用相对路径或用户环境目录。

## 目录

```text
codex/           Codex v2 宠物包、安装脚本与中文说明
desktop/src/     C# / WPF 源码与编译脚本
desktop/assets/  桌面动画和素材布局
desktop/audio/   可选音频的裁切时码说明
docs/            纯角色动作与方向预览
ASSETS.md        素材来源与权利范围
SECURITY.md      隐私边界与提交注意事项
```

## 隐私

桌面版没有网络请求、遥测或在线登录。启用“跟随本地 Codex 任务”时，会读取本机 `CODEX_HOME/sessions`（默认用户目录的 `.codex/sessions`）里的事件日志，只将任务开始、完成、失败或中止事件用于动画状态，不上传或保存对话正文。该功能可在菜单中关闭。

仓库采用文件白名单，只收录必要源码、资源和公开说明；密钥、环境配置、个人文档、聊天记录、安装回执、日志与本机设置均不在发布内容中。提交使用 GitHub `noreply` 邮箱。后续贡献前仍应核对 `git diff --cached`；`.gitignore` 不会自动清除已经提交的敏感信息。

## 许可与说明

本项目是非官方同人作品，与 A-SOUL、嘉然及 OpenAI 无隶属或背书关系。

项目代码和安装脚本按 [MIT License](LICENSE) 开源。角色、名称、生成图片及第三方音视频的权利范围另见 [ASSETS.md](ASSETS.md)，不因代码的 MIT 许可证而一并获得授权。欢迎通过 Issues / Pull Requests 反馈问题和改进源码。

# 嘉然桌宠 · Windows 桌面版

一个用 C# / WPF 编写的嘉然桌面陪伴小工具。支持拖动、摸头、跳跃、喂嘉心糖、睡眠、散步和鼠标视线跟随，通过右键菜单使用专注计时与设置。公开版默认不附带原声音频，动画、互动和专注计时均可独立使用。

## 环境与运行

- Windows 10 / 11，x64；需要 .NET Framework 4.8。
- 使用系统 WPF、Windows Forms、Win32 和本地媒体播放器，不需要 Python、Node.js、NuGet 包或 API key。
- 不支持 macOS / Linux；Windows ARM、32 位系统和其他兼容层未经验证。

从 Release 下载桌面版压缩包后，完整解压，再双击 `嘉然桌宠.exe`。保留同目录的 `assets`、`audio`、`voice_manifest.json` 和 `嘉然桌宠.exe.config`，不要单独移动 exe。源码仓库不跟踪编译产物，克隆后请先编译。

桌宠采用单实例运行。从右键菜单或托盘菜单选择「退出」可完整退出。

## 互动与设置

| 操作 | 功能 |
| --- | --- |
| 按住拖动 | 移动桌宠，松手后限制在显示器工作区内 |
| 单击 / 双击 | 摸摸头 / 元气跳跃；睡眠时单击唤醒 |
| 右键 | 挥手、喂糖、睡眠、心情、专注、显示、语音与尺寸设置 |
| 托盘图标 | 隐藏/找回桌宠、互动菜单、退出；双击找回位置 |

右键菜单提供 15 / 25 / 45 分钟专注、5 分钟休息，以及暂停、继续和结束；完成专注后累计轮数。桌宠有 160 / 220 / 300 三档大小、置顶、对白和状态标签开关，以及多显示器移动与位置恢复功能。散步、语音和音量可在设置中调整。设置会保存桌宠位置、大小、嘉心糖数量、完成的专注轮数和选项；当前正在进行的倒计时不会跨进程恢复。

## 源码编译

在此 `desktop` 目录打开 PowerShell：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\src\build.ps1'
```

脚本调用系统 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，编译 `src/*.cs` 并嵌入 `app.manifest` 和图标，生成此目录的 `嘉然桌宠.exe`。`v4.0.30319` 是 .NET Framework 4.x 的系统目录名称，运行配置要求 .NET Framework 4.8。编译目标是 Windows x64 GUI 程序。

## 安装与卸载

便携运行无需安装。希望固定安装并创建桌面快捷方式时，编译或解压 Release 后执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\安装到本机.ps1'
```

默认复制到 `%LOCALAPPDATA%\JiaranDesktopPet\app`，创建「嘉然 · 番茄炒蛋桌宠」桌面快捷方式。安装不需要管理员权限，不设置开机启动。脚本只复制列出的必要文件，先检查动作素材；升级时只关闭目标安装目录中的桌宠，保留父目录已有设置。

可选参数：

```powershell
# 安装后启动桌宠
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\安装到本机.ps1' -Launch

# 自定义目录必须以 JiaranDesktopPet\app 结尾；不创建快捷方式
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\安装到本机.ps1' -TargetDir 'C:\Apps\JiaranDesktopPet\app' -NoShortcuts
```

卸载时先从托盘退出，删除所选安装位置的 `JiaranDesktopPet\app` 文件夹以及「嘉然 · 番茄炒蛋桌宠」桌面快捷方式即可。需要清除个人设置时，再删除 `JiaranDesktopPet` 父目录内的 `settings.json`、`settings.json.tmp` 和 `last-error.txt`；若用过 `JIARAN_CONFIG_DIR`，到该目录自行清除。便携版直接删除解压目录，设置仍存于用户数据目录。

## 本地数据与 Codex 状态检测

桌宠默认每 3 秒检测本机 Codex 状态；可在「陪伴设置 → 跟随本地 Codex 任务」关闭。它与 Codex 版桌宠是两种独立使用方式，不会替换 Codex 素材或修改 Codex 配置。

`CodexWatcher` 访问的目录是 `$env:CODEX_HOME\sessions`；未设置 `CODEX_HOME` 时，使用当前用户的 `%USERPROFILE%\.codex\sessions`。它列出当天及前两天日期目录下的 `*.jsonl`，每轮选择最近修改的最多 32 个会话文件，以只读、允许其他程序同时写入的方式读取。

会话 JSONL 本身可能包含聊天正文和本地路径。检测器读取这些文件的增量字节，仅用首行 `session_meta` 元信息排除子代理，并使用 `task_started`、`task_complete`、`turn_aborted`、`task_failed` 的事件类型和时间戳切换动画。文件路径、读取偏移、未完成行缓存和状态在进程内存中使用，不另存会话副本或对话正文；代码没有网络请求、遥测、API 调用或消息发送，也不会写回会话文件。首次启动不会重播旧完成事件；找不到目录时可继续使用普通桌宠功能。

这项检测依赖本地会话目录和事件格式。部分 Codex 版本、远程会话、云端任务或安装方式可能无法被检测，任务状态也不是官方稳定接口；「等待」心情仍可手动选择。

个人设置和错误记录默认保存到 `%LOCALAPPDATA%\JiaranDesktopPet`；安装到 `JiaranDesktopPet\app` 时使用其父目录。环境变量 `JIARAN_CONFIG_DIR` 可指定其他设置目录。错误记录可能包含本机文件路径，分享日志前请自行脱敏。安装素材预检记录存于系统临时目录的 `JiaranDesktopPet\install-preflight`。

## 语音与素材

公开仓库和 Release 不包含官方视频裁切的 WAV。`voice_manifest.json` 保留原来源和事件映射，`audio/clips.json` 保留裁切时码说明；这些文件不是音频下载器，程序不会联网获取素材。

取得所用音频的必要授权后，可自行放入 `audio/greeting.wav`、`audio/happy.wav`、`audio/food.wav`、`audio/mua.wav`，或编辑 `voice_manifest.json` 映射到 `audio` 内其他本地文件。缺少音频时，程序仍可正常启动；已安装版本中自行放入的音频不会被无音频升级包删除。

本项目为爱好者作品，角色及相关素材权利归原权利人。代码与素材的许可范围不同，详见仓库根目录 `LICENSE` 和 `ASSETS.md`。

## 内置验证

以下命令输出到新的临时目录，不加载或改写正在运行桌宠的个人设置：

```powershell
$qaDir = Join-Path $env:TEMP ('jiaran-self-test-' + [Guid]::NewGuid().ToString('N'))
$qa = Start-Process -FilePath '.\嘉然桌宠.exe' -ArgumentList ('--self-test "' + $qaDir + '"') -PassThru -WindowStyle Hidden
$qa.WaitForExit()
if ($qa.ExitCode -ne 0) { throw "自测失败，查看 $qaDir" }
Get-Content -LiteralPath (Join-Path $qaDir 'engine-tests.txt')
Get-Content -LiteralPath (Join-Path $qaDir 'watcher-tests.txt')
Get-Content -LiteralPath (Join-Path $qaDir 'geometry-tests.txt')
Get-Content -LiteralPath (Join-Path $qaDir 'asset-tests.txt')
```

自测使用自己生成的会话和图片夹具，覆盖拖动、状态恢复、专注计时、睡眠、视线方向、会话增量读取、多屏坐标边界与素材帧检查。实际素材预检使用 `--asset-qa <输出文件>`；内置 `--render-qa <目录>` 会短暂打开独立 QA 窗口并导出渲染结果。`--audio-qa <输出文件>` 仅用于已自行配置 WAV 的环境。

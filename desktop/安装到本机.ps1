param(
    [string]$TargetDir,
    [switch]$Launch,
    [switch]$NoShortcuts
)
$ErrorActionPreference = 'Stop'
$sourceDir = [IO.Path]::GetFullPath($PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'JiaranDesktopPet\app'
}
$TargetDir = [IO.Path]::GetFullPath($TargetDir).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($TargetDir -eq $sourceDir.TrimEnd([IO.Path]::DirectorySeparatorChar)) { throw '源目录与安装目录不能相同。' }
if ((Split-Path $TargetDir -Leaf) -ne 'app' -or (Split-Path (Split-Path $TargetDir -Parent) -Leaf) -ne 'JiaranDesktopPet') {
    throw '安装目录必须以 JiaranDesktopPet\app 结尾，避免覆盖其他项目。'
}

# 固定白名单；不递归打包本地设置、日志、文档或其他临时文件。
$requiredFiles = @(
    '嘉然桌宠.exe','嘉然桌宠.exe.config','jiaran.ico','voice_manifest.json','使用说明.txt','README.md',
    'assets\manifest.json','assets\atlas.png','assets\wave-frames.png',
    'assets\candy-frames.png','assets\sleep-frames.png',
    'audio\clips.json',
    'src\App.cs','src\Assets.cs','src\AudioService.cs','src\CodexWatcher.cs',
    'src\PetEngine.cs','src\app.manifest','src\build.ps1'
)
foreach ($name in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDir $name) -PathType Leaf)) {
        throw "交付包缺少：$name。若缺少 exe，请先运行 src\build.ps1。"
    }
}

# 只读检查动作素材；检查记录放在临时目录，不读取或改写现有桌宠设置。
$preflightDir = Join-Path ([IO.Path]::GetTempPath()) ('JiaranDesktopPet\install-preflight\' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Force -Path $preflightDir | Out-Null
$assetCheck = Join-Path $preflightDir 'preflight-assets.txt'
$assetCheckProcess = Start-Process -FilePath (Join-Path $sourceDir '嘉然桌宠.exe') -ArgumentList ('--asset-qa "' + $assetCheck + '"') -PassThru -WindowStyle Hidden
$assetCheckProcess.WaitForExit()
if ($assetCheckProcess.ExitCode -ne 0) { throw "动作素材预检失败，桌宠文件尚未替换。检查记录：$preflightDir" }

# 升级时只关闭此安装目录中的桌宠进程；父级 settings.json 保留。
$targetExecutable = Join-Path $TargetDir '嘉然桌宠.exe'
$matchingProcesses = @(Get-Process -Name '嘉然桌宠' -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq $targetExecutable
})
foreach ($process in $matchingProcesses) {
    $closedGracefully = $process.CloseMainWindow()
    if (-not $closedGracefully -or -not $process.WaitForExit(5000)) {
        $stillRunning = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($stillRunning -and $stillRunning.Path -and [IO.Path]::GetFullPath($stillRunning.Path) -eq $targetExecutable) {
            Stop-Process -Id $stillRunning.Id -Force
            $stillRunning.WaitForExit(5000) | Out-Null
        }
    }
}

$optionalFiles = @('audio\greeting.wav','audio\happy.wav','audio\food.wav','audio\mua.wav')
$payloadFiles = @($requiredFiles) + @($optionalFiles | Where-Object { Test-Path -LiteralPath (Join-Path $sourceDir $_) -PathType Leaf })
$allowedPrefix = $TargetDir + [IO.Path]::DirectorySeparatorChar
foreach ($name in $payloadFiles) {
    $destination = [IO.Path]::GetFullPath((Join-Path $TargetDir $name))
    if (-not $destination.StartsWith($allowedPrefix,[StringComparison]::OrdinalIgnoreCase)) { throw '安装文件路径越界。' }
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceDir $name) -Destination $destination -Force
}

if (-not $NoShortcuts) {
    $desktopPath = [Environment]::GetFolderPath('Desktop')
    $shell = New-Object -ComObject WScript.Shell
    $linkPath = Join-Path $desktopPath '嘉然 · 番茄炒蛋桌宠.lnk'
    $shortcut = $shell.CreateShortcut($linkPath)
    $shortcut.TargetPath = $targetExecutable
    $shortcut.Arguments = ''
    $shortcut.WorkingDirectory = $TargetDir
    $shortcut.Description = '嘉然桌宠 · 互动与专注陪伴'
    $shortcut.IconLocation = Join-Path $TargetDir 'jiaran.ico'
    $shortcut.Save()
    Write-Output "已创建快捷方式：$linkPath"
}
if ($Launch) { Start-Process -FilePath $targetExecutable -WorkingDirectory $TargetDir -WindowStyle Normal }
Write-Output "已安装：$TargetDir"
Write-Output "素材预检记录：$preflightDir"
Write-Output '保留现有 settings.json。未设置开机启动。'

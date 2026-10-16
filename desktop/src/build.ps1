$ErrorActionPreference = 'Stop'
$appDir = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    throw '未找到 64 位 .NET Framework 编译器，请在 Windows x64 上安装 .NET Framework 4.8。'
}
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/r:' + (Join-Path "$framework\WPF" $_) }
$sourceFiles = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName
$iconArgument = @()
if (Test-Path -LiteralPath (Join-Path $appDir 'jiaran.ico')) { $iconArgument = @('/win32icon:' + (Join-Path $appDir 'jiaran.ico')) }
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/win32manifest:$PSScriptRoot\app.manifest" "/out:$appDir\嘉然桌宠.exe" $iconArgument $references $sourceFiles
if ($LASTEXITCODE -ne 0) { throw '嘉然桌宠编译失败' }
Write-Output (Join-Path $appDir '嘉然桌宠.exe')

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$DestinationRoot,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$petId = 'jiaran-classic-v3'
$manifestPath = Join-Path $PSScriptRoot 'pet.json'
$spritePath = Join-Path $PSScriptRoot 'spritesheet.webp'
function Get-SpriteHash([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($hasher.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    } finally {
        $stream.Dispose()
        $hasher.Dispose()
    }
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.id -ne $petId -or $manifest.spriteVersionNumber -ne 2 -or $manifest.spritesheetPath -ne 'spritesheet.webp') {
    throw 'The manifest does not match the Jiaran v2 package.'
}
$expectedHash = '951a2bb9f5ff2fec689394b0fdfa45e0c8e17ccdb43a82350e4725c90159155b'
if ((Get-SpriteHash $spritePath) -ne $expectedHash) {
    throw 'Sprite SHA-256 validation failed.'
}
if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    $nativeHome = $env:CODEX_HOME
    if ([string]::IsNullOrWhiteSpace($nativeHome)) { $nativeHome = Join-Path $env:USERPROFILE '.codex' }
    $DestinationRoot = Join-Path $nativeHome 'pets'
}
$destination = [System.IO.Path]::GetFullPath((Join-Path $DestinationRoot $petId))
if ((Test-Path -LiteralPath $destination) -and -not $Force) {
    throw 'The pet directory already exists. Use -Force to back it up and update it.'
}
if ($PSCmdlet.ShouldProcess($destination, 'Install the Jiaran pet package')) {
    if (Test-Path -LiteralPath $destination) {
        $backupRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'JiaranPetBackups'
        $backupPath = Join-Path $backupRoot ($petId + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination $backupPath -Recurse
        Write-Output ('Previous package backed up to: ' + $backupPath)
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $destination 'pet.json') -Force
    Copy-Item -LiteralPath $spritePath -Destination (Join-Path $destination 'spritesheet.webp') -Force
    if ((Get-SpriteHash (Join-Path $destination 'spritesheet.webp')) -ne $expectedHash) {
        throw 'Installed sprite SHA-256 validation failed.'
    }
    Write-Output ('Installed: ' + $destination)
    Write-Output 'Refresh the pet list in Codex settings and select the Jiaran pet.'
}

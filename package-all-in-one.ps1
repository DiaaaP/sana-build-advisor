$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleVersion = '2026.10.22'
$bepInExVersion = '5.4.23.5'
$bepInExSha256 = '82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4'
$bepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$bepInExVersion/BepInEx_win_x64_$bepInExVersion.zip"
$bepInExLicenseUrl = "https://raw.githubusercontent.com/BepInEx/BepInEx/v$bepInExVersion/LICENSE"

$mods = @(
    @{
        Name = 'SanaQuickItems'
        Version = '4.0.0'
        Archive = 'SanaQuickItems-4.0.0.zip'
        Url = 'https://raw.githubusercontent.com/DiaaaP/sana-quick-items/v4.0.0/packages/SanaQuickItems-4.0.0.zip'
        Sha256 = '768b0ed00905b36ab5de8592f10267b284f81082498a1bb53d0e9742e13725b4'
        Dll = 'SanaQuickItems.dll'
    },
    @{
        Name = 'SanaBuildOverlay'
        Version = '0.14.3'
        Archive = 'SanaBuildAdvisor-0.14.3.zip'
        Url = 'https://raw.githubusercontent.com/DiaaaP/sana-build-advisor/v0.14.3/packages/SanaBuildAdvisor-0.14.3.zip'
        Sha256 = 'ec42459b053cf6cd8ed467b39bf2a3a04a94092de34ec4214b8e1ab6125d45ef'
        Dll = 'SanaBuildOverlay.dll'
    },
    @{
        Name = 'RiskUpgrader'
        Version = '0.3.0'
        Archive = 'RiskUpgrader-0.3.0.zip'
        Url = 'https://raw.githubusercontent.com/DiaaaP/risk-upgrader/v0.3.0/packages/RiskUpgrader-0.3.0.zip'
        Sha256 = '1ccc23ed849a71974bd593353298d9643001872439d47f594559b4db5a243363'
        Dll = 'RiskUpgrader.dll'
    }
)

$cache = Join-Path ([IO.Path]::GetTempPath()) 'SanaRoR2ModpackCache'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$bepInExZip = Join-Path $cache "BepInEx_win_x64_$bepInExVersion.zip"
if (-not (Test-Path -LiteralPath $bepInExZip)) {
    Invoke-WebRequest -Uri $bepInExUrl -OutFile $bepInExZip
}
$actualBepInExHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $bepInExZip).Hash.ToLowerInvariant()
if ($actualBepInExHash -ne $bepInExSha256) {
    throw "BepInEx SHA256 mismatch. Expected $bepInExSha256, got $actualBepInExHash"
}

$license = Join-Path $cache 'BepInEx-LICENSE.txt'
if (-not (Test-Path -LiteralPath $license)) {
    Invoke-WebRequest -Uri $bepInExLicenseUrl -OutFile $license
}

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$stage = Join-Path $dist ("all-in-one-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Expand-Archive -LiteralPath $bepInExZip -DestinationPath $stage -Force

foreach ($mod in $mods) {
    $archive = Join-Path $cache $mod.Archive
    if (-not (Test-Path -LiteralPath $archive)) {
        Invoke-WebRequest -Uri $mod.Url -OutFile $archive
    }
    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
    if ($actualHash -ne $mod.Sha256) {
        throw "$($mod.Archive) SHA256 mismatch. Expected $($mod.Sha256), got $actualHash"
    }

    $expandedMod = Join-Path $cache ("expanded-" + $mod.Name + '-' + [guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $archive -DestinationPath $expandedMod -Force
    $pluginDir = Join-Path $stage ("BepInEx\plugins\" + $mod.Name)
    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $expandedMod $mod.Dll) -Destination $pluginDir -Force
    Get-ChildItem -LiteralPath $expandedMod -File | Where-Object { $_.Name -ne $mod.Dll } | Copy-Item -Destination $pluginDir -Force
    Remove-Item -LiteralPath $expandedMod -Recurse -Force
}

$thirdParty = Join-Path $stage 'THIRD-PARTY-NOTICES'
New-Item -ItemType Directory -Path $thirdParty -Force | Out-Null
Copy-Item -LiteralPath $license -Destination (Join-Path $thirdParty 'BepInEx-LICENSE.txt') -Force
@"
BepInEx $bepInExVersion (official Windows x64 release)
Source: https://github.com/BepInEx/BepInEx/tree/v$bepInExVersion
Release: https://github.com/BepInEx/BepInEx/releases/tag/v$bepInExVersion
Original archive SHA256: $bepInExSha256
The BepInEx files are redistributed unchanged from the official release archive.
"@ | Set-Content -LiteralPath (Join-Path $thirdParty 'BepInEx-SOURCE.txt') -Encoding utf8

@"
SANA RISK OF RAIN 2 MODPACK $bundleVersion - БЫСТРАЯ УСТАНОВКА

Содержимое:
- Sana Quick Items 4.0.0 — NumPad 8
- SANA Build Advisor 0.14.3 — F8
- Risk Upgrader 0.3.0 — F7
- официальный BepInEx $bepInExVersion для Windows x64

Установка:
1. Закрой Risk of Rain 2.
2. Открой Steam -> Risk of Rain 2 -> Управление -> Просмотреть локальные файлы.
3. Распакуй СОДЕРЖИМОЕ архива прямо в папку игры рядом с Risk of Rain 2.exe.
4. Запусти игру обычным способом через Steam. Больше ничего запускать не нужно.

Удаление отдельного мода: удали его папку из BepInEx\plugins.
"@ | Set-Content -LiteralPath (Join-Path $stage 'README-INSTALL-RU.txt') -Encoding utf8

$output = Join-Path $dist "Sana-RoR2-Modpack-$bundleVersion-Windows-OneClick.zip"
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $output, [IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -LiteralPath $stage -Recurse -Force
Write-Host "All-in-one package: $output" -ForegroundColor Green

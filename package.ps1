$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'build.ps1')
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'SanaBuildOverlay'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item (Join-Path $root 'build\SanaBuildOverlay.dll') $stage -Force
Copy-Item (Join-Path $root 'README.md') $stage -Force
Copy-Item (Join-Path $root 'LICENSE') $stage -Force
Copy-Item (Join-Path $root 'CHANGELOG.txt') $stage -Force
$zip = Join-Path $dist 'SanaBuildAdvisor-0.14.3.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Write-Host "Package: $zip" -ForegroundColor Green

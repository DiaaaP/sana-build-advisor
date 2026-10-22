param(
    [switch]$Install,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2'
)

$ErrorActionPreference = 'Stop'

# Prevent silent acceptance of misspelled switches such as -nstall.
if ($args.Count -gt 0) {
    throw ("Unknown argument(s): {0}. Use -Install." -f ($args -join ' '))
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$managed = Join-Path $GameDir 'Risk of Rain 2_Data\Managed'
$bepinex = Join-Path $GameDir 'BepInEx\core\BepInEx.dll'
$source = Join-Path $root 'BuildOverlay.cs'
$sources = @(Get-ChildItem -LiteralPath $root -Filter '*.cs' -File | ForEach-Object { $_.FullName })
$buildDir = Join-Path $root 'build'
$output = Join-Path $buildDir 'SanaBuildOverlay.dll'

$compilers = @(
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
)
$compiler = $compilers | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'C# compiler not found. Please send your older working Quick Items build.ps1.' }
if (-not (Test-Path -LiteralPath $bepinex)) { throw "Missing BepInEx: $bepinex" }
if (-not (Test-Path -LiteralPath $managed)) { throw "Missing game Managed directory: $managed" }
if (-not (Test-Path -LiteralPath $source)) { throw "Missing source file: $source" }

# Use the actual assemblies installed with this game, rather than updating
# Unity module references one by one when a new GUI type is used.
$references = @($bepinex)
$required = @(
    'RoR2.dll',
    'Unity.Addressables.dll',
    'Unity.ResourceManager.dll',
    'HGUnityUtils.dll',
    'com.unity.multiplayer-hlapi.Runtime.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.IMGUIModule.dll',
    'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.TextRenderingModule.dll'
)
foreach ($name in $required) {
    $file = Join-Path $managed $name
    if (-not (Test-Path -LiteralPath $file)) { throw "Required game assembly missing: $file" }
    $references += $file
}

# Unity splits its public APIs over multiple UnityEngine.*Module.dll files.
# Referencing the installed modules automatically avoids CS0012/CS1070
# whenever a public game type exposes a type from another Unity module.
$unityModules = @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEngine*.dll' -File)
if ($unityModules.Count -eq 0) { throw "No UnityEngine assemblies found in: $managed" }
$references += @($unityModules | ForEach-Object { $_.FullName })

foreach ($name in @('netstandard.dll', 'System.Runtime.dll')) {
    foreach ($folder in @($managed, (Split-Path -Parent $bepinex))) {
        $file = Join-Path $folder $name
        if (Test-Path -LiteralPath $file) {
            $references += $file
            break
        }
    }
}
$references = @($references | Select-Object -Unique)
Write-Host ("Using {0} reference assemblies ({1} UnityEngine)." -f $references.Count, $unityModules.Count) -ForegroundColor DarkCyan

New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$staging = Join-Path $buildDir 'staging'
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$temporary = Join-Path $staging 'SanaBuildOverlay.dll'

# Important: do NOT create compiler.rsp or insert literal quote characters.
# Windows PowerShell passes each array item as a separate native argument
# and automatically quotes file paths containing spaces.
$compilerArgs = @('/nologo', '/target:library', ('/out:' + $temporary))
foreach ($reference in ($references | Select-Object -Unique)) {
    $compilerArgs += '/reference:' + $reference
}
$compilerArgs += $sources

Write-Host 'Building SanaBuildOverlay against your installed game DLLs...' -ForegroundColor Cyan
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $temporary)) {
    throw 'Compilation failed. No game files were changed.'
}
Move-Item -LiteralPath $temporary -Destination $output -Force
Write-Host "Built: $output" -ForegroundColor Green

if ($Install) {
    if (Get-Process -Name 'Risk of Rain 2' -ErrorAction SilentlyContinue) { throw 'Close Risk of Rain 2 before installation. Installed DLL unchanged.' }
    $destination = Join-Path $GameDir 'BepInEx\plugins\SanaBuildOverlay'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $installed = Join-Path $destination 'SanaBuildOverlay.dll'
    if (Test-Path -LiteralPath $installed) {
        Copy-Item -LiteralPath $installed -Destination ($installed + '.' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.bak') -Force
    }
    Copy-Item -LiteralPath $output -Destination $installed -Force
    Write-Host "Installed: $installed" -ForegroundColor Green
    Write-Host 'Start Risk of Rain 2. F8 = toggle overlay; compact/expanded UI and live stats appear during a run.' -ForegroundColor Cyan
}

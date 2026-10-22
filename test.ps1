$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$build = Join-Path $root 'build'
$compiler = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $compiler)) { $compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" }
New-Item -ItemType Directory -Path $build -Force | Out-Null
$sources = @(
    'AdvancedAdvisor.cs', 'ArchetypeMatcher.cs', 'BuildArchetype.cs', 'DecisionAdvisor.cs',
    'EquipmentAdvisor.cs', 'InteractionPolicy.cs', 'ItemDatabase.cs', 'ReadinessAnalyzer.cs',
    'RecommendationEngine.cs', 'StatCalculator.cs', 'TeamAnalyzer.cs', 'tests\EngineTests.cs'
) | ForEach-Object { Join-Path $root $_ }
& $compiler /nologo /target:exe ('/out:' + (Join-Path $build 'EngineTests.exe')) $sources
if ($LASTEXITCODE -ne 0) { throw 'Tests failed to compile.' }
& (Join-Path $build 'EngineTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

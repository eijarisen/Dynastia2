param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot "Dynastia.slnx"
$TestProject = Join-Path $RepoRoot "tests\Dynastia.Core.Tests\Dynastia.Core.Tests.csproj"
$AppOutput = Join-Path $RepoRoot "src\Dynastia.App\bin\Debug\net10.0"
$InstalledPluginsRoot = Join-Path $AppOutput "plugins"

$PluginProjects = Get-ChildItem `
    (Join-Path $RepoRoot "plugins") `
    -Filter "*.csproj" `
    -Recurse

Write-Host "Removing stale development plugin binaries..."

# Plugin DLLs are loaded dynamically and can remain binary-compatible enough to
# survive normal incremental checks while still referencing an older Contracts
# API. Remove both plugin build outputs and installed plugin copies before the
# build so an old DLL can never satisfy an existence/timestamp check.
foreach ($Project in $PluginProjects) {
    $PluginBinDebug = Join-Path $Project.Directory.FullName "bin\Debug"
    $PluginObjDebug = Join-Path $Project.Directory.FullName "obj\Debug"

    if (Test-Path $PluginBinDebug) {
        Remove-Item $PluginBinDebug -Recurse -Force
    }

    if (Test-Path $PluginObjDebug) {
        Remove-Item $PluginObjDebug -Recurse -Force
    }
}

if (Test-Path $InstalledPluginsRoot) {
    Remove-Item $InstalledPluginsRoot -Recurse -Force
}

Write-Host "Building Dynastia solution (non-incremental)..."
dotnet build $Solution --no-incremental
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# A solution build should normally produce every plugin. Some SDK/.slnx
# combinations have skipped newly added projects in practice. Since outputs
# were deleted above, a missing assembly now unambiguously means this plugin
# was not built, so build that project directly.
foreach ($Project in $PluginProjects) {
    $ManifestPath = Join-Path $Project.Directory.FullName "plugin.json"
    if (-not (Test-Path $ManifestPath)) {
        continue
    }

    $PluginSourceDir = Join-Path $Project.Directory.FullName "bin\Debug\net10.0"
    $ExpectedAssembly = Join-Path $PluginSourceDir "$($Project.BaseName).dll"

    if (-not (Test-Path $ExpectedAssembly)) {
        Write-Host ""
        Write-Host "Plugin was skipped by the solution build; building $($Project.BaseName) directly..."
        dotnet build $Project.FullName --no-incremental
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    if (-not (Test-Path $ExpectedAssembly)) {
        throw "Plugin output was not produced for $($Project.BaseName): $ExpectedAssembly"
    }
}

if (-not $SkipTests) {
    Write-Host ""
    Write-Host "Running tests..."
    dotnet test $TestProject --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

foreach ($Project in $PluginProjects) {
    $PluginSourceDir = Join-Path $Project.Directory.FullName "bin\Debug\net10.0"
    $ManifestPath = Join-Path $Project.Directory.FullName "plugin.json"

    if (-not (Test-Path $ManifestPath)) {
        continue
    }

    $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    $PluginTargetDir = Join-Path $InstalledPluginsRoot $Manifest.id

    New-Item -ItemType Directory -Force $PluginTargetDir | Out-Null

    Get-ChildItem $PluginSourceDir -File | ForEach-Object {
        if ($_.Name -ne "Dynastia.Contracts.dll") {
            Copy-Item $_.FullName $PluginTargetDir -Force
        }
    }

    Copy-Item $ManifestPath $PluginTargetDir -Force

    $InstalledAssembly = Join-Path $PluginTargetDir $Manifest.assembly
    if (-not (Test-Path $InstalledAssembly)) {
        throw "Installed plugin assembly is missing for $($Manifest.id): $InstalledAssembly"
    }

    Write-Host "Installed $($Manifest.id)"
}

Write-Host ""
Write-Host "Development build complete."

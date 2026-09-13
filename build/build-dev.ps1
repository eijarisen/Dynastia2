param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot "Dynastia.slnx"
$TestProject = Join-Path $RepoRoot "tests\Dynastia.Core.Tests\Dynastia.Core.Tests.csproj"
$AppOutput = Join-Path $RepoRoot "src\Dynastia.App\bin\Debug\net10.0"

Write-Host "Building Dynastia solution..."
dotnet build $Solution
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipTests) {
    Write-Host ""
    Write-Host "Running tests..."
    dotnet test $TestProject --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$PluginProjects = Get-ChildItem `
    (Join-Path $RepoRoot "plugins") `
    -Filter "*.csproj" `
    -Recurse

foreach ($Project in $PluginProjects) {
    $PluginSourceDir = Join-Path $Project.Directory.FullName "bin\Debug\net10.0"
    $ManifestPath = Join-Path $Project.Directory.FullName "plugin.json"

    if (-not (Test-Path $ManifestPath)) {
        continue
    }

    if (-not (Test-Path $PluginSourceDir)) {
        throw "Plugin output was not produced for $($Project.BaseName): $PluginSourceDir"
    }

    $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    $PluginTargetDir = Join-Path $AppOutput "plugins\$($Manifest.id)"

    # Remove the old installed copy first so renamed/removed plugin files do
    # not survive a development build and mask source changes.
    if (Test-Path $PluginTargetDir) {
        Remove-Item $PluginTargetDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force $PluginTargetDir | Out-Null

    Get-ChildItem $PluginSourceDir -File | ForEach-Object {
        if ($_.Name -ne "Dynastia.Contracts.dll") {
            Copy-Item $_.FullName $PluginTargetDir -Force
        }
    }

    Copy-Item $ManifestPath $PluginTargetDir -Force

    Write-Host "Installed $($Manifest.id)"
}

Write-Host ""
Write-Host "Development build complete."

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Removing generated build output..."
Get-ChildItem $RepoRoot -Directory -Recurse -Force |
    Where-Object { $_.Name -in @("bin", "obj") } |
    Sort-Object FullName -Descending |
    ForEach-Object {
        if (Test-Path $_.FullName) {
            Remove-Item $_.FullName -Recurse -Force
        }
    }

$ObsoletePaths = @(
    "src\Dynastia.StandardUI",
    "tests\Dynastia.IntegrationTests",
    "src\Dynastia.Core\Class1.cs",
    "tests\Dynastia.Core.Tests\UnitTest1.cs",
    "docs\Architecture.md",
    "docs\GameplaySpecification.md",
    "docs\LegacyQuirks.md",
    "docs\PluginAPI.md",
    "data\Names\weighted_format_example.csv",
    "data\Common\recovery_activities.json",
    "build\remove-sample-plugin.ps1",
    "src\Dynastia.App\Models"
)

Write-Host "Removing obsolete placeholders and unused data..."
foreach ($RelativePath in $ObsoletePaths) {
    $Path = Join-Path $RepoRoot $RelativePath
    if (Test-Path $Path) {
        Remove-Item $Path -Recurse -Force
        Write-Host "Removed $RelativePath"
    }
}

$Docs = Join-Path $RepoRoot "docs"
if ((Test-Path $Docs) -and -not (Get-ChildItem $Docs -Force)) {
    Remove-Item $Docs -Force
}

Write-Host "Repository cleanup complete."

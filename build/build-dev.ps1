$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $RepoRoot "src\Dynastia.App\Dynastia.App.csproj"
$AppOutput = Join-Path $RepoRoot "src\Dynastia.App\bin\Debug\net10.0"

Write-Host "Building Dynastia.App..."
dotnet build $AppProject
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$PluginProjects = Get-ChildItem `
    (Join-Path $RepoRoot "plugins") `
    -Filter "*.csproj" `
    -Recurse

foreach ($Project in $PluginProjects) {
    Write-Host ""
    Write-Host "Building plugin: $($Project.BaseName)"

    dotnet build $Project.FullName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $PluginSourceDir = Join-Path $Project.Directory.FullName "bin\Debug\net10.0"
    $ManifestPath = Join-Path $Project.Directory.FullName "plugin.json"

    if (-not (Test-Path $ManifestPath)) {
        Write-Host "Skipping install; no plugin.json found."
        continue
    }

    $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    $PluginTargetDir = Join-Path $AppOutput "plugins\$($Manifest.id)"

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

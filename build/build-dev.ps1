param(
    [switch]$SkipTests,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'repository-artifact-policy.ps1')
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot "Dynastia.slnx"
$AppOutput = Join-Path $RepoRoot "src\Dynastia.App\bin\Debug\net10.0"
$InstalledPluginsRoot = Join-Path $AppOutput "plugins"

if ($Clean) {
    Write-Host "Removing repository build outputs for clean verification..."
    $Removed = Remove-RepositoryBuildArtifacts -Root $RepoRoot
    Write-Host "Removed $($Removed.RemovedDirectories) build-output directories."
}

# Only installed copies are disposable on a normal build. MSBuild tracks source
# and Contracts project-reference changes using the retained plugin bin/obj data.
Write-Host "Removing installed development plugin copies..."
if (Test-Path -LiteralPath $InstalledPluginsRoot) {
    Remove-Item -LiteralPath $InstalledPluginsRoot -Recurse -Force
}

$PluginProjects = @(Get-ChildItem (Join-Path $RepoRoot "plugins") -Filter "*.csproj" -File -Recurse |
    Where-Object { -not (Test-RepositorySourceExclusion (Get-RepositoryRelativePath $RepoRoot $_.FullName)) } |
    Sort-Object FullName)
# An omitted project with an existing DLL still needs an incremental direct build;
# otherwise it has no solution graph to perform its source/Contracts staleness check.
$SolutionXml = [xml](Get-Content -LiteralPath $Solution -Raw)
$SolutionProjects = @($SolutionXml.SelectNodes('//Project[@Path]') | ForEach-Object {
    [IO.Path]::GetFullPath((Join-Path $RepoRoot $_.Path))
})

Write-Host "Building Dynastia solution (incremental)..."
dotnet build $Solution
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Preserve the missing-output safety net, including SDK/solution omissions.
foreach ($Project in $PluginProjects) {
    $ManifestPath = Join-Path $Project.Directory.FullName "plugin.json"
    if (-not (Test-Path -LiteralPath $ManifestPath)) { continue }

    $Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $PluginSourceDir = Join-Path $Project.Directory.FullName "bin\Debug\net10.0"
    $ExpectedAssembly = Join-Path $PluginSourceDir $Manifest.assembly
    if (($SolutionProjects -notcontains $Project.FullName) -or
        -not (Test-Path -LiteralPath $ExpectedAssembly -PathType Leaf)) {
        Write-Host "Building plugin directly: $($Project.BaseName)..."
        dotnet build $Project.FullName
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    if (-not (Test-Path -LiteralPath $ExpectedAssembly -PathType Leaf)) {
        throw "Plugin output was not produced for $($Project.BaseName): $ExpectedAssembly"
    }
}

if (-not $SkipTests) {
    Write-Host "Running repository tooling checks..."
    & (Join-Path $PSScriptRoot 'test-repository-tooling.ps1')

    $TestProjects = @(Get-ChildItem (Join-Path $RepoRoot "tests") -Filter "*Tests.csproj" -File -Recurse |
        Where-Object { -not (Test-RepositorySourceExclusion (Get-RepositoryRelativePath $RepoRoot $_.FullName)) } |
        Sort-Object FullName)
    foreach ($TestProject in $TestProjects) {
        Write-Host "Running $($TestProject.BaseName)..."
        # Allow the incremental build/restore: newly discovered tests may not yet
        # be in the solution, and --no-build could silently use stale test binaries.
        dotnet test $TestProject.FullName
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

foreach ($Project in $PluginProjects) {
    $PluginSourceDir = Join-Path $Project.Directory.FullName "bin\Debug\net10.0"
    $ManifestPath = Join-Path $Project.Directory.FullName "plugin.json"
    if (-not (Test-Path -LiteralPath $ManifestPath)) { continue }

    $Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $PluginTargetDir = Join-Path $InstalledPluginsRoot $Manifest.id
    New-Item -ItemType Directory -Force -Path $PluginTargetDir | Out-Null

    Get-ChildItem -LiteralPath $PluginSourceDir -File | ForEach-Object {
        if ($_.Name -ne "Dynastia.Contracts.dll") {
            Copy-Item -LiteralPath $_.FullName -Destination $PluginTargetDir -Force
        }
    }
    Copy-Item -LiteralPath $ManifestPath -Destination $PluginTargetDir -Force

    $InstalledAssembly = Join-Path $PluginTargetDir $Manifest.assembly
    if (-not (Test-Path -LiteralPath $InstalledAssembly -PathType Leaf)) {
        throw "Installed plugin assembly is missing for $($Manifest.id): $InstalledAssembly"
    }
    Write-Host "Installed $($Manifest.id)"
}

Write-Host "Development build complete."

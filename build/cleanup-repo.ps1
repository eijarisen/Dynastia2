param(
    [switch]$IncludeArchives
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'repository-artifact-policy.ps1')
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Removing generated development artifacts..."
$Removed = Remove-RepositoryBuildArtifacts -Root $RepoRoot -IncludeGeneratedFiles -IncludeArchives:$IncludeArchives
Write-Host "Repository cleanup complete: $($Removed.RemovedDirectories) directories and $($Removed.RemovedFiles) files removed."
Write-Host "Source, data, documentation, Git metadata and saves were preserved."

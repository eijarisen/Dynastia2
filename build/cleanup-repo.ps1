param(
    [switch]$IncludeArchives
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot

$GeneratedDirectoryNames = @(
    ".vs",
    "bin",
    "obj",
    "artifacts",
    "releases",
    "TestResults",
    "test-results",
    "logs"
)

$GeneratedFilePatterns = @(
    "*.binlog",
    "*.user",
    "*.suo",
    "*.tmp",
    "*.bak",
    "*.orig"
)

Write-Host "Removing generated development directories..."
Get-ChildItem $RepoRoot -Directory -Recurse -Force |
    Where-Object {
        $RelativePath = [IO.Path]::GetRelativePath($RepoRoot, $_.FullName)
        $Segments = $RelativePath -split '[\\/]'
        ($Segments -notcontains ".git") -and
        ($GeneratedDirectoryNames -contains $_.Name)
    } |
    Sort-Object FullName -Descending |
    ForEach-Object {
        if (Test-Path $_.FullName) {
            Remove-Item $_.FullName -Recurse -Force
            Write-Host "Removed $([IO.Path]::GetRelativePath($RepoRoot, $_.FullName))"
        }
    }

Write-Host "Removing generated development files..."
foreach ($Pattern in $GeneratedFilePatterns) {
    Get-ChildItem $RepoRoot -File -Recurse -Force -Filter $Pattern |
        Where-Object {
            $RelativePath = [IO.Path]::GetRelativePath($RepoRoot, $_.FullName)
            ($RelativePath -split '[\\/]') -notcontains ".git"
        } |
        ForEach-Object {
            Remove-Item $_.FullName -Force
            Write-Host "Removed $([IO.Path]::GetRelativePath($RepoRoot, $_.FullName))"
        }
}

if ($IncludeArchives) {
    Write-Host "Removing local ZIP archives..."
    Get-ChildItem $RepoRoot -File -Recurse -Force -Filter "*.zip" |
        Where-Object {
            $RelativePath = [IO.Path]::GetRelativePath($RepoRoot, $_.FullName)
            ($RelativePath -split '[\\/]') -notcontains ".git"
        } |
        ForEach-Object {
            Remove-Item $_.FullName -Force
            Write-Host "Removed $([IO.Path]::GetRelativePath($RepoRoot, $_.FullName))"
        }
}

Write-Host "Repository cleanup complete. Source, data, documentation and saves were preserved."

param(
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$RepoName = Split-Path $RepoRoot -Leaf

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path $RepoRoot -Parent) "$RepoName-source.zip"
}
elseif (-not [IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $RepoRoot $OutputPath
}

$ExcludedDirectoryNames = @(
    ".git",
    ".vs",
    "bin",
    "obj",
    "artifacts",
    "releases",
    "logs",
    "saves"
)

$ExcludedExtensions = @(
    ".zip"
)

$TempRoot = Join-Path ([IO.Path]::GetTempPath()) ("dynastia-source-" + [Guid]::NewGuid().ToString("N"))
$StageRoot = Join-Path $TempRoot $RepoName

try {
    New-Item -ItemType Directory -Force $StageRoot | Out-Null

    Get-ChildItem $RepoRoot -File -Recurse -Force |
        Where-Object {
            $File = $_
            $RelativePath = [IO.Path]::GetRelativePath($RepoRoot, $File.FullName)
            $Segments = $RelativePath -split '[\\/]'

            -not ($Segments | Where-Object {
                $ExcludedDirectoryNames -contains $_.ToLowerInvariant()
            }) -and
            -not ($ExcludedExtensions -contains $File.Extension.ToLowerInvariant())
        } |
        ForEach-Object {
            $RelativePath = [IO.Path]::GetRelativePath($RepoRoot, $_.FullName)
            $Destination = Join-Path $StageRoot $RelativePath
            $DestinationDirectory = Split-Path $Destination -Parent

            New-Item -ItemType Directory -Force $DestinationDirectory | Out-Null
            Copy-Item $_.FullName $Destination -Force
        }

    $OutputDirectory = Split-Path $OutputPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
        New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
    }

    if (Test-Path $OutputPath) {
        Remove-Item $OutputPath -Force
    }

    Compress-Archive -Path $StageRoot -DestinationPath $OutputPath -CompressionLevel Optimal

    Write-Host "Source archive created: $OutputPath"
}
finally {
    if (Test-Path $TempRoot) {
        Remove-Item $TempRoot -Recurse -Force
    }
}

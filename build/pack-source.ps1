param(
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'repository-artifact-policy.ps1')
$RepoRoot = Split-Path -Parent $PSScriptRoot
$RepoName = Split-Path $RepoRoot -Leaf

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path $RepoRoot -Parent) "$RepoName-source.zip"
}
elseif (-not [IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $RepoRoot $OutputPath
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if ([IO.Path]::GetExtension($OutputPath) -ine '.zip') {
    throw 'Source archive OutputPath must have a .zip extension.'
}

$TempRoot = Join-Path ([IO.Path]::GetTempPath()) ("dynastia-source-" + [Guid]::NewGuid().ToString("N"))
$StageRoot = Join-Path $TempRoot $RepoName

try {
    New-Item -ItemType Directory -Force -Path $StageRoot | Out-Null
    foreach ($Entry in (Get-RepositorySourceEntries -Root $RepoRoot)) {
        $RelativePath = Get-RepositoryRelativePath $RepoRoot $Entry.FullName
        $Destination = Join-Path $StageRoot $RelativePath
        if ($Entry.PSIsContainer) {
            New-Item -ItemType Directory -Force -Path $Destination | Out-Null
        }
        else {
            New-Item -ItemType Directory -Force -Path (Split-Path $Destination -Parent) | Out-Null
            Copy-Item -LiteralPath $Entry.FullName -Destination $Destination -Force
        }
    }

    $Summary = Assert-RepositorySourceStage -StageRoot $StageRoot
    Write-Host "Validated source stage: $($Summary.FileCount) files, $($Summary.ByteCount) bytes."

    $OutputDirectory = Split-Path $OutputPath -Parent
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

    # ZipFile preserves .gitignore/.gitattributes and other legitimate hidden files,
    # which Compress-Archive can omit. Keep the existing single repository folder.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $TemporaryArchive = Join-Path $TempRoot 'source.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $StageRoot, $TemporaryArchive, [IO.Compression.CompressionLevel]::Optimal, $true)
    # Leave any previous handoff intact until staging, validation and compression succeed.
    Copy-Item -LiteralPath $TemporaryArchive -Destination $OutputPath -Force
    Write-Host "Source archive created: $OutputPath"
}
finally {
    if (Test-Path -LiteralPath $TempRoot) {
        Remove-Item -LiteralPath $TempRoot -Recurse -Force
    }
}

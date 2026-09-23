# Explicit repository policy. Keep generated categories in .gitignore in sync;
# .gitignore is never parsed by cleanup or source packaging.
# These helpers also support Windows PowerShell 5.1 (.NET Framework).

function Get-RepositoryArtifactPolicy {
    [pscustomobject]@{
        BuildOutputDirectories = @(
            'bin', 'obj', '.vs', 'artifacts', 'publish',
            'BenchmarkDotNet.Artifacts', 'TestResults', 'test-results', 'logs'
        )
        SourcePackageOnlyDirectories = @(
            '.git', 'releases', 'saves', 'packages', '.nuget', '.idea',
            '_ReSharper*', '_NCrunch_*', '__pycache__', 'node_modules',
            '.history', '.localhistory', '$RECYCLE.BIN'
        )
        GeneratedFilePatterns = @(
            '*.binlog', '*.log', 'coverage*.json', 'coverage*.xml', 'coverage*.info',
            '*.coverage', '*.coveragexml', '*.suo', '*.user', '*.rsuser',
            '*.userosscache', '*.sln.docstates', '*.DotSettings.user', '*.code-workspace',
            '*.tmp', '*.bak', '*.orig', '*.swp', '*~', '~$*', '*.nupkg', '*.snupkg',
            '.DS_Store', '._*', 'Thumbs.db', 'ehthumbs.db', 'Desktop.ini', '*.pyc'
        )
        ArchiveFilePatterns = @('*.zip')
        ProtectedCleanupDirectories = @('.git', 'saves')
        SharedEditorFiles = @('settings.json', 'tasks.json', 'launch.json', 'extensions.json')
        RequiredSourceFiles = @('Dynastia.slnx', 'global.json')
        RequiredSourceDirectories = @('src', 'plugins', 'data', 'tests', 'build')
    }
}

function Get-RepositoryRelativePath {
    param([string]$Root, [string]$Path)

    # Path.GetRelativePath is unavailable in Windows PowerShell 5.1. All callers
    # operate on descendants, so a checked prefix is sufficient (including []/spaces).
    $Separators = [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $Prefix = [IO.Path]::GetFullPath($Root).TrimEnd($Separators) + [IO.Path]::DirectorySeparatorChar
    $FullPath = [IO.Path]::GetFullPath($Path)
    $Comparison = [StringComparison]::Ordinal
    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        $Comparison = [StringComparison]::OrdinalIgnoreCase
    }
    if (-not $FullPath.StartsWith($Prefix, $Comparison)) {
        throw "Path is outside repository root: $Path"
    }
    return $FullPath.Substring($Prefix.Length)
}

function Test-RepositoryNamePattern {
    param([string]$Name, [string[]]$Patterns)

    foreach ($Pattern in $Patterns) {
        if ($Name -like $Pattern) { return $true }
    }
    return $false
}

function Test-RepositorySourceExclusion {
    param(
        [string]$RelativePath,
        [switch]$IsDirectory,
        $Policy = (Get-RepositoryArtifactPolicy)
    )

    $Segments = $RelativePath -split '[\\/]'
    # Linked Git worktrees use a .git file instead of a directory.
    if ($Segments -contains '.git') { return $true }
    $DirectoryCount = $Segments.Length
    if (-not $IsDirectory) { $DirectoryCount-- }
    $DirectoryPatterns = $Policy.BuildOutputDirectories + $Policy.SourcePackageOnlyDirectories
    for ($Index = 0; $Index -lt $DirectoryCount; $Index++) {
        if (Test-RepositoryNamePattern $Segments[$Index] $DirectoryPatterns) { return $true }
        # Retain the four shared editor files allowed by .gitignore; omit local state.
        if ($Segments[$Index] -eq '.vscode' -and $Index -lt $Segments.Length - 1) {
            if ($IsDirectory -or $Index -ne $Segments.Length - 2 -or
                $Policy.SharedEditorFiles -notcontains $Segments[-1]) { return $true }
        }
    }
    if (-not $IsDirectory) {
        return (Test-RepositoryNamePattern $Segments[-1] ($Policy.GeneratedFilePatterns + $Policy.ArchiveFilePatterns))
    }
    return $false
}

function Get-RepositorySourceEntries {
    param([string]$Root, $Policy = (Get-RepositoryArtifactPolicy))

    # Prune excluded directories before descending instead of scanning bin/obj,
    # package caches and Git metadata only to discard their files afterwards.
    $Pending = New-Object 'System.Collections.Generic.Stack[System.String]'
    $Pending.Push([IO.Path]::GetFullPath($Root))
    while ($Pending.Count -gt 0) {
        foreach ($Entry in (Get-ChildItem -LiteralPath ($Pending.Pop()) -Force | Sort-Object FullName)) {
            $RelativePath = Get-RepositoryRelativePath $Root $Entry.FullName
            if (Test-RepositorySourceExclusion $RelativePath -IsDirectory:$Entry.PSIsContainer -Policy $Policy) { continue }
            if ($Entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Source packaging cannot follow a link/junction: $RelativePath"
            }
            $Entry
            if ($Entry.PSIsContainer) { $Pending.Push($Entry.FullName) }
        }
    }
}

function Remove-RepositoryBuildArtifacts {
    param(
        [string]$Root,
        [switch]$IncludeGeneratedFiles,
        [switch]$IncludeArchives,
        $Policy = (Get-RepositoryArtifactPolicy)
    )

    $RemovedDirectories = 0
    $RemovedFiles = 0
    $FilePatterns = @()
    if ($IncludeGeneratedFiles) { $FilePatterns += $Policy.GeneratedFilePatterns }
    if ($IncludeArchives) { $FilePatterns += $Policy.ArchiveFilePatterns }
    $Pending = New-Object 'System.Collections.Generic.Stack[System.String]'
    $Pending.Push([IO.Path]::GetFullPath($Root))
    while ($Pending.Count -gt 0) {
        foreach ($Entry in (Get-ChildItem -LiteralPath ($Pending.Pop()) -Force | Sort-Object FullName)) {
            # Do not traverse or remove links, Git internals, or any saved games,
            # even when they contain names such as bin, *.bak or *.zip.
            if ($Entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($Entry.PSIsContainer) {
                if (Test-RepositoryNamePattern $Entry.Name $Policy.ProtectedCleanupDirectories) { continue }
                if (Test-RepositoryNamePattern $Entry.Name $Policy.BuildOutputDirectories) {
                    Remove-Item -LiteralPath $Entry.FullName -Recurse -Force
                    $RemovedDirectories++
                    Write-Host "Removed $(Get-RepositoryRelativePath $Root $Entry.FullName)"
                }
                else { $Pending.Push($Entry.FullName) }
            }
            elseif (Test-RepositoryNamePattern $Entry.Name $FilePatterns) {
                Remove-Item -LiteralPath $Entry.FullName -Force
                $RemovedFiles++
                Write-Host "Removed $(Get-RepositoryRelativePath $Root $Entry.FullName)"
            }
        }
    }
    [pscustomobject]@{ RemovedDirectories = $RemovedDirectories; RemovedFiles = $RemovedFiles }
}

function Assert-RepositorySourceStage {
    param([string]$StageRoot, $Policy = (Get-RepositoryArtifactPolicy))

    foreach ($Name in $Policy.RequiredSourceFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $StageRoot $Name) -PathType Leaf)) {
            throw "Source stage is missing required file: $Name"
        }
    }
    foreach ($Name in $Policy.RequiredSourceDirectories) {
        if (-not (Test-Path -LiteralPath (Join-Path $StageRoot $Name) -PathType Container)) {
            throw "Source stage is missing required directory: $Name"
        }
    }
    # Deliberately inspect the unfiltered stage, not Get-RepositorySourceEntries:
    # injected forbidden entries must fail validation rather than disappear from it.
    $FileCount = 0
    [long]$ByteCount = 0
    foreach ($Entry in (Get-ChildItem -LiteralPath $StageRoot -Recurse -Force)) {
        $RelativePath = Get-RepositoryRelativePath $StageRoot $Entry.FullName
        if ((Test-RepositorySourceExclusion $RelativePath -IsDirectory:$Entry.PSIsContainer -Policy $Policy) -or
            ($Entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Forbidden source-stage entry: $RelativePath"
        }
        if (-not $Entry.PSIsContainer) {
            $FileCount++
            $ByteCount += $Entry.Length
        }
    }
    [pscustomobject]@{ FileCount = $FileCount; ByteCount = $ByteCount }
}

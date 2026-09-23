param(
    # Internal child-process mode isolates the fake dotnet command from the real build.
    [string]$BuildFixtureRoot,
    [string]$Scenario
)

$ErrorActionPreference = 'Stop'

if ($BuildFixtureRoot) {
    $env:DYNASTIA_BUILD_FIXTURE = $BuildFixtureRoot
    $env:DYNASTIA_BUILD_SCENARIO = $Scenario
    function global:dotnet {
        $Arguments = @($args)
        $Root = $env:DYNASTIA_BUILD_FIXTURE
        $Mode = $env:DYNASTIA_BUILD_SCENARIO
        Add-Content -LiteralPath (Join-Path $Root 'commands.txt') -Value ($Arguments -join '|')
        $global:LASTEXITCODE = 0
        if ($Arguments -contains '--no-incremental') { throw 'Incrementality was disabled.' }
        if ($Arguments[0] -eq 'test') {
            if ($Arguments -contains '--no-build' -or $Arguments -contains '--no-restore') {
                throw 'A discovered test project must be allowed to build/restore.'
            }
            if ($Mode -eq 'TestFailure') { $global:LASTEXITCODE = 23 }
            if ($Mode -eq 'MissingInstalledAssembly') {
                Remove-Item -LiteralPath (Join-Path $Root 'plugins/Registered/bin/Debug/net10.0/Registered.Runtime.dll') -Force -ErrorAction SilentlyContinue
            }
            return
        }
        if ($Arguments[0] -ne 'build') { throw 'Unexpected dotnet command.' }
        $IsSolution = $Arguments[1] -like '*.slnx'
        if ($IsSolution) {
            if (Test-Path -LiteralPath (Join-Path $Root 'src/Dynastia.App/bin/Debug/net10.0/plugins')) {
                throw 'Stale installed plugins survived until the solution build.'
            }
            foreach ($Marker in @('plugins/Registered/bin/Debug/keep.txt', 'plugins/Registered/obj/Debug/keep.txt', 'src/Dynastia.Contracts/bin/Debug/keep.txt')) {
                $Exists = Test-Path -LiteralPath (Join-Path $Root $Marker)
                if ($Exists -eq ($Mode -eq 'Clean')) { throw "Wrong output retention in $Mode : $Marker" }
            }
            foreach ($Marker in @('saves/bin/save.bak', '.git/obj/protected.log', 'data/keep.txt')) {
                if (-not (Test-Path -LiteralPath (Join-Path $Root $Marker))) { throw "Deleted protected file: $Marker" }
            }
            if ($Mode -eq 'BuildFailure') { $global:LASTEXITCODE = 21; return }
        }
        elseif ($Mode -eq 'PluginFailure') { $global:LASTEXITCODE = 22; return }

        if ($IsSolution -and $Mode -in @('MissingOutput', 'NoPluginOutput')) { return }
        if ($Mode -eq 'NoPluginOutput') { return }
        $Name = 'Registered'
        if (-not $IsSolution) { $Name = [IO.Path]::GetFileNameWithoutExtension($Arguments[1]) }
        $Output = Join-Path $Root "plugins/$Name/bin/Debug/net10.0"
        New-Item -ItemType Directory -Path $Output -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $Output "$Name.Runtime.dll") -Value 'fresh'
        Set-Content -LiteralPath (Join-Path $Output 'Dynastia.Contracts.dll') -Value 'must not be installed'
    }
    $BuildArguments = @{}
    if ($Scenario -eq 'Clean') { $BuildArguments.Clean = $true }
    if ($Scenario -eq 'SkipTests') { $BuildArguments.SkipTests = $true }
    & (Join-Path $BuildFixtureRoot 'build/build-dev.ps1') @BuildArguments
    exit $LASTEXITCODE
}

. (Join-Path $PSScriptRoot 'repository-artifact-policy.ps1')
$Policy = Get-RepositoryArtifactPolicy
$RepoRoot = Split-Path -Parent $PSScriptRoot
$TestRoot = Join-Path ([IO.Path]::GetTempPath()) ('dynastia-tooling-' + [Guid]::NewGuid().ToString('N'))
$script:Checks = 0

function Assert-Tooling {
    param([bool]$Condition, [string]$Message)
    $script:Checks++
    if (-not $Condition) { throw "Repository tooling regression: $Message" }
}

function Assert-ToolingThrows {
    param([scriptblock]$Action, [string]$MessagePattern)
    $Caught = $null
    try { & $Action | Out-Null }
    catch { $Caught = $_.Exception.Message }
    Assert-Tooling ($null -ne $Caught -and $Caught -like $MessagePattern) "Expected '$MessagePattern'; got '$Caught'."
}

function Write-FixtureFile {
    param([string]$Root, [string]$RelativePath, [string]$Content = 'fixture')
    $Path = Join-Path $Root $RelativePath
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8
}

function New-RepositoryFixture {
    param([string]$Root)
    foreach ($Directory in @('src', 'plugins', 'data', 'tests', 'docs', 'build')) {
        Write-FixtureFile $Root "$Directory/keep.txt"
    }
    Write-FixtureFile $Root 'Dynastia.slnx' '<Solution />'
    Write-FixtureFile $Root 'global.json' '{}'
    foreach ($Name in @('repository-artifact-policy.ps1', 'cleanup-repo.ps1', 'pack-source.ps1', 'build-dev.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $Name) -Destination (Join-Path $Root "build/$Name")
    }
}

try {
    New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
    foreach ($ScriptName in @('repository-artifact-policy.ps1', 'build-dev.ps1', 'cleanup-repo.ps1', 'pack-source.ps1', 'test-repository-tooling.ps1')) {
        $Tokens = $null
        $ParseErrors = $null
        [System.Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $PSScriptRoot $ScriptName), [ref]$Tokens, [ref]$ParseErrors) | Out-Null
        Assert-Tooling ($ParseErrors.Count -eq 0) "PowerShell syntax: $ScriptName $ParseErrors"
    }
    # Explicit acceptance cases supplement the policy-driven cases below.
    foreach ($Directory in @('bin', 'obj', '.vs', 'artifacts', 'publish', 'BenchmarkDotNet.Artifacts', 'TestResults', 'test-results', 'logs', '.git', 'releases', 'saves', 'packages', '.nuget')) {
        Assert-Tooling (Test-RepositorySourceExclusion "nested/$Directory/forbidden.txt") "Excluded directory: $Directory"
        Assert-Tooling (Test-RepositorySourceExclusion "nested/$($Directory.ToUpperInvariant())" -IsDirectory) "Case-insensitive empty directory: $Directory"
    }
    foreach ($Pattern in $Policy.GeneratedFilePatterns + $Policy.ArchiveFilePatterns) {
        $Name = $Pattern.Replace('*', 'sample')
        Assert-Tooling (Test-RepositorySourceExclusion "nested/$Name") "Excluded file: $Name"
    }
    foreach ($Path in @('src/Program.cs', 'plugins/One/plugin.json', 'data/rules.json', 'docs/README.md', '.gitignore', '.gitattributes', 'src/Assets/image.png', 'data/binary_catalog.csv')) {
        Assert-Tooling (-not (Test-RepositorySourceExclusion $Path)) "Retained source: $Path"
    }

    # Git syntax is read only by this regression check, never by runtime policy.
    # Adding a generated category to .gitignore without policy coverage fails here.
    foreach ($Line in (Get-Content -LiteralPath (Join-Path $RepoRoot '.gitignore'))) {
        $Rule = $Line.Trim()
        if (-not $Rule -or $Rule.StartsWith('#')) { continue }
        if ($Rule.StartsWith('!')) {
            Assert-Tooling (-not (Test-RepositorySourceExclusion ($Rule.Substring(1)))) "Shared Git exception: $Rule"
            continue
        }
        $Directory = $Rule.EndsWith('/')
        $Sample = $Rule -replace '^\*\*/', ''
        $Sample = $Sample.TrimEnd('/').Replace('*', 'sample')
        Assert-Tooling (Test-RepositorySourceExclusion $Sample -IsDirectory:$Directory) "Policy does not cover .gitignore rule: $Rule"
    }
    Assert-Tooling (Test-RepositorySourceExclusion '.git') 'Worktree Git metadata must be excluded.'
    Assert-Tooling (Test-RepositorySourceExclusion '.vscode/local/settings.json') 'Nested editor state must be excluded.'
    Assert-Tooling (Test-RepositorySourceExclusion '.vscode/local' -IsDirectory) 'Local editor directories must be excluded.'

    $Stage = Join-Path $TestRoot 'stage'
    New-RepositoryFixture $Stage
    $Summary = Assert-RepositorySourceStage $Stage
    Assert-Tooling ($Summary.FileCount -gt 0 -and $Summary.ByteCount -gt 0) 'Stage statistics must include bytes and files.'
    foreach ($Path in @('src/bin/injected.dll', 'coverage.injected.xml', 'nested/source.ZIP', 'packages/package.txt', 'publish/app.exe')) {
        Write-FixtureFile $Stage $Path
        Assert-ToolingThrows { Assert-RepositorySourceStage $Stage } '*Forbidden source-stage entry*'
        if ($Path.Contains('/')) { Remove-Item -LiteralPath (Join-Path $Stage ($Path -split '/')[0]) -Recurse -Force }
        else { Remove-Item -LiteralPath (Join-Path $Stage $Path) -Force }
        # The src/bin injection removed the test's src directory; restore its baseline.
        if (-not (Test-Path -LiteralPath (Join-Path $Stage 'src'))) { Write-FixtureFile $Stage 'src/keep.txt' }
    }
    $EmptyForbidden = Join-Path $Stage 'obj'
    New-Item -ItemType Directory -Path $EmptyForbidden | Out-Null
    Assert-ToolingThrows { Assert-RepositorySourceStage $Stage } '*Forbidden source-stage entry*'
    Remove-Item -LiteralPath $EmptyForbidden
    foreach ($Required in $Policy.RequiredSourceFiles + $Policy.RequiredSourceDirectories) {
        $Path = Join-Path $Stage $Required
        $Holding = Join-Path $TestRoot 'holding'
        Move-Item -LiteralPath $Path -Destination $Holding
        Assert-ToolingThrows { Assert-RepositorySourceStage $Stage } '*missing required*'
        Move-Item -LiteralPath $Holding -Destination $Path
    }

    $Source = Join-Path $TestRoot 'repo [source]'
    New-RepositoryFixture $Source
    foreach ($Directory in $Policy.BuildOutputDirectories + $Policy.SourcePackageOnlyDirectories) {
        Write-FixtureFile $Source "$($Directory.Replace('*', 'cache'))/excluded.dat"
    }
    foreach ($Pattern in $Policy.GeneratedFilePatterns + $Policy.ArchiveFilePatterns) {
        Write-FixtureFile $Source ($Pattern.Replace('*', 'sample'))
    }
    foreach ($Name in $Policy.SharedEditorFiles) { Write-FixtureFile $Source ".vscode/$Name" '{}' }
    Write-FixtureFile $Source '.vscode/local.json' '{}'
    foreach ($Name in @('.gitignore', '.gitattributes')) { Write-FixtureFile $Source $Name }
    Write-FixtureFile $Source 'src/Assets/icon.png'
    $Archive = Join-Path $Source 'handoff.zip'
    & (Join-Path $Source 'build/pack-source.ps1') -OutputPath $Archive
    $Zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        $Names = @($Zip.Entries | ForEach-Object { ($_.FullName -replace '\\', '/') -replace '^[^/]+/', '' })
        foreach ($Required in @('Dynastia.slnx', 'global.json', '.gitignore', '.gitattributes', 'docs/keep.txt', 'src/Assets/icon.png', 'plugins/keep.txt', 'data/keep.txt', 'tests/keep.txt', 'build/build-dev.ps1', '.vscode/settings.json')) {
            Assert-Tooling ($Names -contains $Required) "ZIP is missing $Required"
        }
        foreach ($Name in $Names) {
            if ($Name) { Assert-Tooling (-not (Test-RepositorySourceExclusion ($Name.TrimEnd('/')) -IsDirectory:($Name.EndsWith('/')))) "Forbidden ZIP entry: $Name" }
        }
    }
    finally { $Zip.Dispose() }
    $ArchiveHash = (Get-FileHash -LiteralPath $Archive).Hash
    Remove-Item -LiteralPath (Join-Path $Source 'global.json')
    Assert-ToolingThrows { & (Join-Path $Source 'build/pack-source.ps1') -OutputPath $Archive } '*missing required file: global.json*'
    Assert-Tooling ((Get-FileHash -LiteralPath $Archive).Hash -eq $ArchiveHash) 'Failed packaging overwrote the previous ZIP.'
    Write-FixtureFile $Source 'global.json' '{}'

    foreach ($Protected in @('saves/bin/save.bak', 'saves/year.zip', '.git/obj/protected.log', '.git/backup.zip')) { Write-FixtureFile $Source $Protected }
    & (Join-Path $Source 'build/cleanup-repo.ps1')
    Assert-Tooling (Test-Path -LiteralPath $Archive) 'Default cleanup removed a ZIP.'
    foreach ($Directory in $Policy.BuildOutputDirectories) {
        Assert-Tooling (-not (Test-Path -LiteralPath (Join-Path $Source $Directory))) "Cleanup left build output: $Directory"
    }
    & (Join-Path $Source 'build/cleanup-repo.ps1') -IncludeArchives
    Assert-Tooling (-not (Test-Path -LiteralPath $Archive)) 'Opt-in archive cleanup did not remove the handoff.'
    foreach ($Protected in @('saves/bin/save.bak', 'saves/year.zip', '.git/obj/protected.log', '.git/backup.zip', 'data/keep.txt', 'docs/keep.txt', 'src/Assets/icon.png')) {
        Assert-Tooling (Test-Path -LiteralPath (Join-Path $Source $Protected)) "Cleanup deleted protected/source content: $Protected"
    }
    $CountRoot = Join-Path $TestRoot 'counts'
    Write-FixtureFile $CountRoot 'obj/output.txt'
    Write-FixtureFile $CountRoot 'trace.binlog'
    $Removed = Remove-RepositoryBuildArtifacts $CountRoot -IncludeGeneratedFiles
    Assert-Tooling ($Removed.RemovedDirectories -eq 1 -and $Removed.RemovedFiles -eq 1) 'Removal counts are incorrect.'

    $PowerShell = Join-Path $PSHOME 'powershell.exe'
    if (-not (Test-Path -LiteralPath $PowerShell)) { $PowerShell = Join-Path $PSHOME 'pwsh' }
    foreach ($Mode in @('Normal', 'Clean', 'SkipTests', 'MissingOutput', 'BuildFailure', 'PluginFailure', 'TestFailure', 'NoPluginOutput', 'MissingInstalledAssembly')) {
        $Fixture = Join-Path $TestRoot "build $Mode"
        New-RepositoryFixture $Fixture
        Write-FixtureFile $Fixture 'Dynastia.slnx' '<Solution><Project Path="plugins/Registered/Registered.csproj" /></Solution>'
        foreach ($Name in @('Registered', 'Omitted')) {
            Write-FixtureFile $Fixture "plugins/$Name/$Name.csproj" '<Project />'
            Write-FixtureFile $Fixture "plugins/$Name/plugin.json" "{`"id`":`"test.$Name`",`"assembly`":`"$Name.Runtime.dll`"}"
            Write-FixtureFile $Fixture "plugins/$Name/bin/Debug/net10.0/$Name.Runtime.dll" 'old'
        }
        if ($Mode -in @('MissingOutput', 'NoPluginOutput')) {
            Remove-Item -LiteralPath (Join-Path $Fixture 'plugins/Registered/bin/Debug/net10.0/Registered.Runtime.dll')
        }
        foreach ($Marker in @('plugins/Registered/bin/Debug/keep.txt', 'plugins/Registered/obj/Debug/keep.txt', 'src/Dynastia.Contracts/bin/Debug/keep.txt', 'saves/bin/save.bak', '.git/obj/protected.log', 'src/Dynastia.App/bin/Debug/net10.0/plugins/stale/stale.dll')) {
            Write-FixtureFile $Fixture $Marker
        }
        foreach ($Test in @('Zeta/Zeta.Tests.csproj', 'Alpha/AlphaTests.csproj', 'nested/OtherTests.csproj', 'bin/GeneratedTests.csproj')) {
            Write-FixtureFile $Fixture "tests/$Test" '<Project />'
        }
        Write-FixtureFile $Fixture 'build/test-repository-tooling.ps1' 'Set-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) "tooling-ran.txt") -Value "yes"'
        # Capture expected child failures without promoting native stderr on PS 5.1.
        $ErrorActionPreference = 'Continue'
        $Output = & $PowerShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $PSCommandPath -BuildFixtureRoot $Fixture -Scenario $Mode 2>&1
        $ExitCode = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        $ShouldSucceed = $Mode -in @('Normal', 'Clean', 'SkipTests', 'MissingOutput')
        Assert-Tooling (($ExitCode -eq 0) -eq $ShouldSucceed) "$Mode exit code $ExitCode. $($Output -join [Environment]::NewLine)"
        $Commands = @(Get-Content -LiteralPath (Join-Path $Fixture 'commands.txt'))
        if ($Mode -eq 'BuildFailure') { Assert-Tooling ($ExitCode -eq 21 -and $Commands.Count -eq 1) 'Solution failure was not propagated.'; continue }
        if ($Mode -eq 'PluginFailure') { Assert-Tooling ($ExitCode -eq 22) 'Plugin failure was not propagated.'; continue }
        if ($Mode -eq 'NoPluginOutput') {
            Assert-Tooling (@($Commands | Where-Object { $_ -like 'test|*' }).Count -eq 0) 'Missing output did not stop the build.'
            Assert-Tooling (($Output -join ' ') -like '*Plugin output was not produced*') 'Missing-output verification was not reached.'
            continue
        }
        if ($Mode -eq 'TestFailure') { Assert-Tooling ($ExitCode -eq 23 -and @($Commands | Where-Object { $_ -like 'test|*' }).Count -eq 1) 'Test failure was not propagated.'; continue }
        if ($Mode -eq 'MissingInstalledAssembly') {
            Assert-Tooling (($Output -join ' ') -like '*Installed plugin assembly is missing*') 'Installed-assembly verification was not reached.'
            continue
        }

        $TestCommands = @($Commands | Where-Object { $_ -like 'test|*' })
        if ($Mode -eq 'SkipTests') {
            Assert-Tooling ($TestCommands.Count -eq 0 -and -not (Test-Path -LiteralPath (Join-Path $Fixture 'tooling-ran.txt'))) '-SkipTests ran tests.'
        }
        else {
            Assert-Tooling ($TestCommands.Count -eq 3) "$Mode did not discover all source test projects."
            Assert-Tooling (($TestCommands -join "`n") -ceq (($TestCommands | Sort-Object) -join "`n")) 'Test discovery order is not deterministic.'
            Assert-Tooling (Test-Path -LiteralPath (Join-Path $Fixture 'tooling-ran.txt')) 'Tooling checks were not run.'
        }
        Assert-Tooling (@($Commands | Where-Object { $_ -like 'build|*Omitted.csproj' }).Count -eq 1) 'Existing output masked an omitted plugin project.'
        if ($Mode -eq 'MissingOutput') {
            Assert-Tooling (@($Commands | Where-Object { $_ -like 'build|*Registered.csproj' }).Count -eq 1) 'Missing plugin fallback did not run.'
        }
        foreach ($Name in @('Registered', 'Omitted')) {
            $Installed = Join-Path $Fixture "src/Dynastia.App/bin/Debug/net10.0/plugins/test.$Name"
            Assert-Tooling ((Get-Content -LiteralPath (Join-Path $Installed "$Name.Runtime.dll") -Raw).Trim() -eq 'fresh') 'Stale plugin was installed.'
            Assert-Tooling (Test-Path -LiteralPath (Join-Path $Installed 'plugin.json')) 'Plugin manifest was not installed.'
            Assert-Tooling (-not (Test-Path -LiteralPath (Join-Path $Installed 'Dynastia.Contracts.dll'))) 'Plugin-local Contracts was installed.'
        }
    }
    Write-Host "Repository tooling checks passed ($script:Checks assertions)."
}
finally {
    if (Test-Path -LiteralPath $TestRoot) { Remove-Item -LiteralPath $TestRoot -Recurse -Force }
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectRelative =
    "plugins\Dynastia.Plugin.Sample\Dynastia.Plugin.Sample.csproj"

$ProjectPath =
    Join-Path $RepoRoot $ProjectRelative

Push-Location $RepoRoot

try {
    if (Test-Path $ProjectPath) {
        Write-Host "Removing Sample plugin from solution..."

        dotnet sln remove $ProjectRelative

        if ($LASTEXITCODE -ne 0) {
            Write-Warning `
                "dotnet sln remove returned an error. Continuing with file cleanup."
        }
    }

    $SourceDirectory =
        Join-Path $RepoRoot "plugins\Dynastia.Plugin.Sample"

    if (Test-Path $SourceDirectory) {
        Write-Host "Deleting Sample plugin source..."
        Remove-Item $SourceDirectory -Recurse -Force
    }

    $AppBin =
        Join-Path $RepoRoot "src\Dynastia.App\bin"

    if (Test-Path $AppBin) {
        Get-ChildItem `
            $AppBin `
            -Directory `
            -Recurse `
            -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Name -ieq "dynastia.sample"
            } |
            ForEach-Object {
                Write-Host "Deleting runtime Sample plugin: $($_.FullName)"
                Remove-Item $_.FullName -Recurse -Force
            }
    }

    Write-Host "Sample plugin removed."
}
finally {
    Pop-Location
}

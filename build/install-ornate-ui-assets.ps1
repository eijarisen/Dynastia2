param(
    [string]$GraphicsZip = "",
    [string]$AudioDirectory = ""
)

$ErrorActionPreference = "Stop"

$RepoRoot =
    Split-Path -Parent $PSScriptRoot

$ProjectPath =
    Join-Path `
        $RepoRoot `
        "src\Dynastia.App\Dynastia.App.csproj"

$AssetsRoot =
    Join-Path `
        $RepoRoot `
        "src\Dynastia.App\Assets"

$FontsDirectory =
    Join-Path `
        $AssetsRoot `
        "Fonts"

$AudioTarget =
    Join-Path `
        $AssetsRoot `
        "Audio"

New-Item `
    -ItemType Directory `
    -Force `
    -Path $FontsDirectory |
    Out-Null

New-Item `
    -ItemType Directory `
    -Force `
    -Path $AudioTarget |
    Out-Null

if ([string]::IsNullOrWhiteSpace($GraphicsZip)) {
    $DefaultGraphics =
        Join-Path `
            $RepoRoot `
            "graphics.zip"

    if (Test-Path $DefaultGraphics) {
        $GraphicsZip =
            $DefaultGraphics
    }
}

if (![string]::IsNullOrWhiteSpace($GraphicsZip)) {
    if (!(Test-Path $GraphicsZip)) {
        throw "Graphics ZIP not found: $GraphicsZip"
    }

    Add-Type `
        -AssemblyName `
        System.IO.Compression.FileSystem

    $archive =
        [System.IO.Compression.ZipFile]::OpenRead(
            (Resolve-Path $GraphicsZip)
        )

    try {
        $fontEntry =
            $archive.Entries |
            Where-Object {
                $_.Name -eq "UnifrakturCook-Bold.ttf"
            } |
            Select-Object -First 1

        if ($null -eq $fontEntry) {
            throw "UnifrakturCook-Bold.ttf was not found in $GraphicsZip"
        }

        $fontTarget =
            Join-Path `
                $FontsDirectory `
                "UnifrakturCook-Bold.ttf"

        [System.IO.Compression.ZipFileExtensions]::ExtractToFile(
            $fontEntry,
            $fontTarget,
            $true
        )

        Write-Host `
            "Installed UI font from your graphics ZIP."
    }
    finally {
        $archive.Dispose()
    }
}
else {
    Write-Warning (
        "No graphics.zip path supplied. The PNG assets are already included, " +
        "but copy UnifrakturCook-Bold.ttf into " +
        "src\Dynastia.App\Assets\Fonts before building."
    )
}

if ([string]::IsNullOrWhiteSpace($AudioDirectory)) {
    $AudioDirectory =
        $RepoRoot
}

$audioCopied =
    0

for ($i = 1; $i -le 4; $i++) {
    $name =
        "Dynasty $i.mp3"

    $source =
        Join-Path `
            $AudioDirectory `
            $name

    if (!(Test-Path $source)) {
        continue
    }

    Copy-Item `
        -Force `
        $source `
        (Join-Path $AudioTarget $name)

    $audioCopied++

    Write-Host `
        "Installed $name"
}

if ($audioCopied -lt 4) {
    Write-Warning (
        "Only $audioCopied of 4 background music files were found. " +
        "Pass -AudioDirectory to the folder containing Dynasty 1.mp3 through Dynasty 4.mp3."
    )
}

if (!(Test-Path $ProjectPath)) {
    throw "Project not found: $ProjectPath"
}

$projectText =
    Get-Content `
        -Raw `
        $ProjectPath

if ($projectText -notmatch '<AvaloniaResource\s+Include="Assets\\\*\*') {
    $resourceBlock = @"

  <ItemGroup>
    <AvaloniaResource Include="Assets\**" />
  </ItemGroup>
"@

    $projectText =
        $projectText.Replace(
            "</Project>",
            "$resourceBlock`r`n</Project>"
        )

    Set-Content `
        -Path $ProjectPath `
        -Value $projectText `
        -Encoding UTF8

    Write-Host `
        "Added Assets\\** as Avalonia resources."
}

$projectText =
    Get-Content `
        -Raw `
        $ProjectPath

if ($projectText -notmatch 'PackageReference\s+Include="NAudio"') {
    Write-Host `
        "Adding NAudio package for MP3 playback..."

    dotnet add `
        $ProjectPath `
        package NAudio `
        --version 2.2.1

    if ($LASTEXITCODE -ne 0) {
        throw "Could not add the NAudio package."
    }
}
else {
    Write-Host `
        "NAudio is already referenced."
}

Write-Host ""
Write-Host "Ornate UI prerequisites are ready."
Write-Host "Now run:"
Write-Host "  powershell -ExecutionPolicy Bypass -File build\build-dev.ps1"

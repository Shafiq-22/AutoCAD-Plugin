<#
.SYNOPSIS
    Builds StructureGen and assembles the installable AutoCAD bundle.

.DESCRIPTION
    Produces artifacts/StructureGen.bundle, laid out exactly as AutoCAD expects:

        StructureGen.bundle/
          PackageContents.xml
          Contents/
            2025/   the plugin built against the AutoCAD 2025 API
            2026/   the plugin built against the AutoCAD 2026 API
            2027/   the plugin built against the AutoCAD 2027 API
            Data/   the structural database, shared by every release
            Docs/   the documentation

    Copy that folder to one of the ApplicationPlugins locations and restart AutoCAD.
    See docs/INSTALLATION.md.

    Each release is built separately because the AutoCAD API assemblies differ per release, and
    from 2027 the .NET runtime does too. Building against the lowest runtime present in a release
    series means one binary serves every update of that series.

.PARAMETER Versions
    Which AutoCAD releases to build. Defaults to all supported ones.

.PARAMETER Configuration
    The build configuration. Defaults to Release.

.PARAMETER SkipTests
    Skips the unit tests. They do not need AutoCAD and take a few seconds, so think twice.

.PARAMETER Zip
    Also produce artifacts/StructureGen.bundle.zip for distribution.

.EXAMPLE
    ./build/build.ps1
    ./build/build.ps1 -Versions 2026 -SkipTests
#>
[CmdletBinding()]
param(
    [ValidateSet('2025', '2026', '2027')]
    [string[]] $Versions = @('2025', '2026', '2027'),

    [string] $Configuration = 'Release',

    [switch] $SkipTests,

    [switch] $Zip
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
$artifacts  = Join-Path $repository 'artifacts'
$bundle     = Join-Path $artifacts  'StructureGen.bundle'
$contents   = Join-Path $bundle     'Contents'

function Write-Step([string] $message) {
    Write-Host ''
    Write-Host "==> $message" -ForegroundColor Cyan
}

Write-Step "StructureGen build"
Write-Host "    repository    : $repository"
Write-Host "    configuration : $Configuration"
Write-Host "    releases      : $($Versions -join ', ')"

if (-not $SkipTests) {
    Write-Step 'Running the unit tests'

    # The tests cover the platform-neutral layers only. They need no AutoCAD and no Windows, which
    # is why they can also run on a Linux build agent.
    & dotnet test (Join-Path $repository 'tests/StructureGen.Tests') -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The unit tests failed. The bundle was not assembled.' }
}

if (Test-Path $bundle) {
    Write-Step 'Clearing the previous bundle'
    Remove-Item $bundle -Recurse -Force
}

New-Item -ItemType Directory -Path $contents -Force | Out-Null

foreach ($version in $Versions) {
    Write-Step "Building for AutoCAD $version"

    $output = Join-Path $contents $version

    & dotnet publish (Join-Path $repository 'src/StructureGen.Acad/StructureGen.Acad.csproj') `
        -c $Configuration `
        -p:AcadVersion=$version `
        -p:DebugType=portable `
        -o $output `
        --nologo

    if ($LASTEXITCODE -ne 0) { throw "The build for AutoCAD $version failed." }

    # The Autodesk assemblies are compile-time references only; AutoCAD loads its own copies.
    # Shipping one alongside the plugin stops the plugin loading, so this guards against a
    # reference losing its ExcludeAssets setting.
    $autodesk = Get-ChildItem $output -Filter 'Ac*.dll' -ErrorAction SilentlyContinue
    $autodesk += Get-ChildItem $output -Filter 'Ad*.dll' -ErrorAction SilentlyContinue

    if ($autodesk) {
        $names = ($autodesk | Select-Object -ExpandProperty Name) -join ', '
        throw "Autodesk assemblies were copied into the output ($names). Check ExcludeAssets on the AutoCAD.NET references."
    }

    Write-Host "    -> $output"
}

Write-Step 'Copying the manifest, data and documentation'

Copy-Item (Join-Path $PSScriptRoot 'PackageContents.xml') $bundle

$dataTarget = Join-Path $contents 'Data'
New-Item -ItemType Directory -Path $dataTarget -Force | Out-Null
Copy-Item (Join-Path $repository 'data/*') $dataTarget -Recurse -Force

$docsTarget = Join-Path $contents 'Docs'
New-Item -ItemType Directory -Path $docsTarget -Force | Out-Null
Copy-Item (Join-Path $repository 'docs/*') $docsTarget -Recurse -Force
Copy-Item (Join-Path $repository 'README.md') $docsTarget -Force
Copy-Item (Join-Path $repository 'LICENSE') $docsTarget -Force

if ($Zip) {
    Write-Step 'Packing the bundle'

    $archive = Join-Path $artifacts 'StructureGen.bundle.zip'
    if (Test-Path $archive) { Remove-Item $archive -Force }

    Compress-Archive -Path $bundle -DestinationPath $archive
    Write-Host "    -> $archive"
}

Write-Step 'Done'
Write-Host "The bundle is at: $bundle"
Write-Host ''
Write-Host 'To install for the current user only, copy it to:'
Write-Host '    %APPDATA%\Autodesk\ApplicationPlugins\'
Write-Host 'To install for every user on the machine (needs administrator rights), copy it to:'
Write-Host '    %PROGRAMDATA%\Autodesk\ApplicationPlugins\'
Write-Host ''
Write-Host 'Then restart AutoCAD and type STRUCTUREGEN.'

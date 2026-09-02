<#
.SYNOPSIS
    Copies a built StructureGen bundle into AutoCAD's plugin folder.

.DESCRIPTION
    Installing StructureGen is nothing more than putting the StructureGen.bundle folder in one of
    AutoCAD's ApplicationPlugins locations. This script does that, and checks the things that
    usually go wrong when it is done by hand: the wrong folder level, a missing manifest, or an
    AutoCAD that is still running and holding the old assemblies open.

    There is no MSI and no registry key. AutoCAD's Autoloader reads the manifest at start-up, and
    uninstalling is deleting the folder.

.PARAMETER BundlePath
    The bundle to install. Defaults to artifacts/StructureGen.bundle, where build.ps1 puts it.

.PARAMETER AllUsers
    Install for every user on the machine. Needs administrator rights. The default is the current
    user only, which needs none.

.PARAMETER Force
    Replace an existing installation without asking.

.EXAMPLE
    ./installer/install.ps1
    ./installer/install.ps1 -AllUsers -Force
#>
[CmdletBinding()]
param(
    [string] $BundlePath,
    [switch] $AllUsers,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot

if (-not $BundlePath) {
    $BundlePath = Join-Path $repository 'artifacts/StructureGen.bundle'
}

if (-not (Test-Path $BundlePath)) {
    throw "No bundle found at '$BundlePath'. Run ./build/build.ps1 first, or pass -BundlePath."
}

$manifest = Join-Path $BundlePath 'PackageContents.xml'
if (-not (Test-Path $manifest)) {
    throw @"
'$BundlePath' does not look like a bundle: it has no PackageContents.xml directly inside it.

The usual cause is pointing at a folder that *contains* the bundle rather than at the bundle itself.
AutoCAD needs the folder whose immediate child is PackageContents.xml.
"@
}

# AutoCAD holds the plugin assemblies open while it runs, so replacing them underneath it fails
# with a file-in-use error that is far less clear than this message.
$running = Get-Process -Name 'acad' -ErrorAction SilentlyContinue
if ($running) {
    throw 'AutoCAD is running. Close it and run this again, or the files it has open cannot be replaced.'
}

$root = if ($AllUsers) {
    Join-Path $env:PROGRAMDATA 'Autodesk/ApplicationPlugins'
} else {
    Join-Path $env:APPDATA 'Autodesk/ApplicationPlugins'
}

$target = Join-Path $root 'StructureGen.bundle'

Write-Host "Installing from : $BundlePath"
Write-Host "Installing to   : $target"

if (Test-Path $target) {
    if (-not $Force) {
        $answer = Read-Host 'StructureGen is already installed there. Replace it? [y/N]'
        if ($answer -notmatch '^[Yy]') {
            Write-Host 'Nothing was changed.'
            return
        }
    }

    Remove-Item $target -Recurse -Force
}

New-Item -ItemType Directory -Path $root -Force | Out-Null
Copy-Item $BundlePath $target -Recurse -Force

$installed = @(Get-ChildItem $target -Recurse -File).Count
Write-Host ''
Write-Host "Installed $installed file(s)." -ForegroundColor Green
Write-Host ''
Write-Host 'Now start AutoCAD and type STRUCTUREGEN.'
Write-Host ''
Write-Host 'If the command is not recognised, see docs/INSTALLATION.md - it covers every cause worth checking.'

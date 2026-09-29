# Copyright (c) 2026 Neil Colvin. MIT with Commons Clause; see LICENSE.
[CmdletBinding()]
param([string]$Version='1.0.0', [switch]$RequireTag)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if($Version -cnotmatch '^\d+\.\d+\.\d+$'){throw 'Use major.minor.patch.'}
$props=[xml](Get-Content "$root/Directory.Build.props" -Raw)
if($props.Project.PropertyGroup.Version -cne $Version){throw 'Source version mismatch.'}
$manifest=Get-Content "$root/RainPointCrestronDriver/RainPointCrestronDriver.json" -Raw|ConvertFrom-Json
$v=[version]$manifest.GeneralInformation.DriverVersion
if("$($v.Major).$($v.Minor).$($v.Build)" -cne $Version){throw 'Driver manifest version mismatch.'}
foreach($file in @('README.md','RELEASE-NOTES.md','CHANGELOG.md','LICENSE','THIRD-PARTY-NOTICES.md','docs/PROGRAMMING.md','tests/README.md')){
    if(!(Test-Path "$root/$file")){throw "Missing $file"}
}
if(!(Get-Content "$root/RELEASE-NOTES.md" -Raw).StartsWith("# RainPointCrestronDriver v$Version")){throw 'Release notes mismatch.'}
if(!(Get-Content "$root/CHANGELOG.md" -Raw).Contains("## $Version —")){throw 'Changelog mismatch.'}
if($RequireTag){
    $head=git -C $root rev-parse HEAD
    if($LASTEXITCODE -ne 0){throw 'Cannot resolve source revision.'}
    $tag=git -C $root rev-parse "refs/tags/v$Version^{commit}"
    if($LASTEXITCODE -ne 0 -or $head -cne $tag){throw 'Release tag does not match checked-out source.'}
}
Write-Output "Release metadata verified: $Version / $($manifest.GeneralInformation.DriverVersion)"
# Copyright (c) 2026 Neil Colvin. MIT with Commons Clause; see LICENSE.
[CmdletBinding()]
param([string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$Version) { $Version = (& "$PSScriptRoot/Get-DriverReleaseVersion.ps1" -ManifestPath "$root/RainPointCrestronDriver/RainPointCrestronDriver.json").PackageVersion }
Push-Location $root
try {
    dotnet build RainPointCrestronDriver/RainPointCrestronDriver.csproj -c Release -p:BuildForTests=false "-p:DriverReleaseVersion=$Version"
    if ($LASTEXITCODE -ne 0) { throw 'Driver packaging failed.' }
    $assembly = Join-Path $root 'RainPointCrestronDriver/bin/Release/net472/patched/RainPointCrestronDriver.dll'
    $before = $env:RAINPOINT_PACKAGED_ASSEMBLY
    try {
        $env:RAINPOINT_PACKAGED_ASSEMBLY = $assembly
        dotnet test RainPointCrestronDriver.Lifecycle.Tests -c Release --filter TestCategory=Package --logger 'trx;LogFilePrefix=package' --results-directory artifacts/tests
        if ($LASTEXITCODE -ne 0) { throw 'Merged package smoke tests failed.' }
    } finally { $env:RAINPOINT_PACKAGED_ASSEMBLY = $before }
    $package = Join-Path $root 'RainPointCrestronDriver/bin/Release/net472/RainPointCrestronDriver.pkg'
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $manifests = @($archive.Entries | Where-Object Name -Like '*.dat')
        if ($manifests.Count -ne 1) { throw 'Expected exactly one package manifest.' }
        $reader = [IO.StreamReader]::new($manifests[0].Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }

        foreach ($required in @('UiDefinitions/UiDefinition.xml', 'Translations/en-US.json')) {
            if (-not @($archive.Entries | Where-Object { $_.FullName.Replace('\','/').EndsWith($required, [StringComparison]::OrdinalIgnoreCase) }).Count) { throw "Missing $required" }
        }
    } finally { $archive.Dispose() }
    $isCi = @($env:CI,$env:TF_BUILD,$env:GITHUB_ACTIONS | Where-Object { $_ -in @('true','1') }).Count -gt 0
    & "$PSScriptRoot/Assert-DriverPackageVersion.ps1" -PackagePath $package -ManifestPath "$root/RainPointCrestronDriver/RainPointCrestronDriver.json" -ReleaseVersion $Version -AllowLocalRevision:(!$isCi)
    Get-FileHash $package -Algorithm SHA256
} finally { Pop-Location }
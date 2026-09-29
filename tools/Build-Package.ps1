# Copyright (c) 2026 Neil Colvin. MIT with Commons Clause; see LICENSE.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet build RainPointCrestronDriver/RainPointCrestronDriver.csproj -c Release -p:BuildForTests=false
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
        if ([version]$manifest.driverVersion -ne [version]'1.1.1.19') { throw 'Unexpected release driver version.' }
        foreach ($required in @('UiDefinitions/UiDefinition.xml', 'Translations/en-US.json')) {
            if (-not @($archive.Entries | Where-Object { $_.FullName.Replace('\','/').EndsWith($required, [StringComparison]::OrdinalIgnoreCase) }).Count) { throw "Missing $required" }
        }
    } finally { $archive.Dispose() }
    Get-FileHash $package -Algorithm SHA256
} finally { Pop-Location }
# Copyright (c) 2026 Neil Colvin. MIT with Commons Clause; see LICENSE.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try{
    & ./tools/Test-ReleaseMetadata.ps1
    & ./.github/scripts/Test-RequiredReleaseChecks.ps1
    dotnet test RainPointCrestronDriver.Tests -c Release --logger 'trx;LogFilePrefix=portable' --results-directory artifacts/tests
    if($LASTEXITCODE -ne 0){throw 'Portable tests failed.'}
    dotnet test RainPointCrestronDriver.Lifecycle.Tests -c Release --filter 'TestCategory!=Package' --logger 'trx;LogFilePrefix=sdk' --results-directory artifacts/tests
    if($LASTEXITCODE -ne 0){throw 'SDK tests failed.'}
    dotnet build RainPointCrestronDriver.Automation.Tests -c Release
    if($LASTEXITCODE -ne 0){throw 'Installed-test harness build failed.'}
}finally{Pop-Location}
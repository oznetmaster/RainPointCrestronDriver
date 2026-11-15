# Copyright (c) 2026 Neil Colvin. MIT with Commons Clause; see LICENSE.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try{
    dotnet run --project tools/Documentation/Documentation.csproj -c Release -- --self-test
    if($LASTEXITCODE -ne 0){throw 'Documentation policy tests failed.'}
    & ./tools/Test-ReleaseMetadata.ps1
    & ./.github/scripts/Test-RequiredReleaseChecks.ps1
    dotnet test RainPointCrestronDriver.Tests -c Release --logger 'trx;LogFilePrefix=portable' --results-directory artifacts/tests
    if($LASTEXITCODE -ne 0){throw 'Portable tests failed.'}
    dotnet test RainPointCrestronDriver.Lifecycle.Tests -c Release -f net10.0 --filter 'TestCategory!=Package' --logger 'trx;LogFilePrefix=sdk' --results-directory artifacts/tests
    if($LASTEXITCODE -ne 0){throw 'SDK tests failed.'}
    # net472 SDK entity execution requires processor Mono; compile it here without
    # generating an all-skipped Windows TRX that would falsely pass or fail the gate.
    dotnet build RainPointCrestronDriver.Lifecycle.Tests -c Release -f net472
    if($LASTEXITCODE -ne 0){throw 'Mono SDK test build failed.'}
    dotnet build RainPointCrestronDriver.Automation.Tests -c Release
    if($LASTEXITCODE -ne 0){throw 'Installed-test harness build failed.'}
}finally{Pop-Location}
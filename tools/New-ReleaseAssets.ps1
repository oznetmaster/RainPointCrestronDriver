# Copyright (c) 2026 Neil Colvin. MIT with Commons Clause; see LICENSE.
[CmdletBinding()]
param([string]$Version='1.0.0', [string]$OutputDirectory='artifacts/release')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try{
    & ./tools/Test-ReleaseMetadata.ps1 -Version $Version
    $output=[IO.Path]::GetFullPath($OutputDirectory, $root)
    if(Test-Path $output){if(@(Get-ChildItem $output -Force).Count){throw 'Choose an empty output directory; existing release assets are never overwritten.'}}
    New-Item -ItemType Directory -Path $output -Force|Out-Null
    & ./tools/Build-Package.ps1
    $package=Join-Path $root 'RainPointCrestronDriver/bin/Release/net472/RainPointCrestronDriver.pkg'
    dotnet pack RainPointCrestronDriver/RainPointCrestronDriver.csproj -c Release --no-build -o $output "-p:PackageVersion=$Version" "-p:PackageDriverPkgPath=$package"
    if($LASTEXITCODE -ne 0){throw 'NuGet packaging failed.'}
    Copy-Item -LiteralPath $package -Destination $output
    foreach($name in @('README.md','RELEASE-NOTES.md','CHANGELOG.md','LICENSE','THIRD-PARTY-NOTICES.md')){Copy-Item -LiteralPath "$root/$name" -Destination $output}
    $nupkg=Join-Path $output "CrestronHomeDriver.RainPoint.Irrigation.$Version.nupkg"
    $archive=[IO.Compression.ZipFile]::OpenRead($nupkg)
    try{
        foreach($name in @('RainPointCrestronDriver.pkg','RainPointCrestronDriver.json','README.md','RELEASE-NOTES.md','LICENSE','THIRD-PARTY-NOTICES.md')){
            if(!$archive.GetEntry($name)){throw "Missing package entry: $name"}
        }
        $specs=@($archive.Entries|Where-Object FullName -Like '*.nuspec')
        if($specs.Count -ne 1){throw 'Expected one NuGet manifest.'}
        $reader=[IO.StreamReader]::new($specs[0].Open())
        try{$spec=[xml]$reader.ReadToEnd()}finally{$reader.Dispose()}
        if($spec.package.metadata.id -cne 'CrestronHomeDriver.RainPoint.Irrigation' -or $spec.package.metadata.version -cne $Version){throw 'NuGet identity mismatch.'}
        if($spec.package.metadata.packageTypes.packageType.name -cne 'CrestronHomeDriver'){throw 'Unexpected NuGet package type.'}
        if($spec.SelectNodes('//*[local-name()="dependency"]').Count){throw 'Driver archive must have no NuGet runtime dependency declarations.'}
        $entry=$archive.GetEntry('RainPointCrestronDriver.pkg').Open()
        try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($entry))}finally{$entry.Dispose()}
        if($hash -cne (Get-FileHash $package).Hash){throw 'Embedded driver package differs.'}
        if(@($archive.Entries|Where-Object FullName -match '^(lib|runtimes)/').Count){throw 'Driver distribution must not expose application assemblies.'}
    }finally{$archive.Dispose()}
    $lines=@(Get-ChildItem $output -File|Sort-Object Name|ForEach-Object{((Get-FileHash $_.FullName).Hash.ToLowerInvariant())+'  '+$_.Name})
    [IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'),$lines,[Text.UTF8Encoding]::new($false))
    Write-Output "Release assets validated: $output"
}finally{Pop-Location}
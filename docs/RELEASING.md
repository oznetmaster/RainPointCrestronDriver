# Preparing and publishing a release

The release version is 1.0.0. Its four-part Crestron manifest version is 1.0.000.0017; build 17 follows the development previews. Keep both versions, the manifest date, README, changelog and release notes aligned. Future releases must increase the installed manifest version.

## Local validation

Use Windows, .NET SDK 10.0.401, the .NET 8 runtime, and `dotnet-ilrepack` 2.0.45. Restore the SDK test project to obtain ManifestUtil 29.0.10 and its test-host compatibility assembly from NuGet.org. No private test DLL or credentials are needed for the offline build.

```powershell
dotnet tool install --global dotnet-ilrepack --version 2.0.45
./tools/Test-Offline.ps1
./tools/New-ReleaseAssets.ps1
```

The asset script builds the production merge, runs package tests, validates the manifest and required UI resources, builds the NuGet distribution and verifies that it embeds the same `.pkg`. It emits the package, NuGet distribution, README, changelog, release notes, license, notices and SHA256SUMS.txt under an empty artifacts/release directory. Choose a new OutputDirectory when repeating a build; it does not overwrite an existing candidate.

## GitHub and NuGet setup

Repository: oznetmaster/RainPointCrestronDriver, default branch main. The Tests workflow runs on pushes, pull requests and manual requests, including both portable runtimes, SDK tests and release packaging. Its artifacts provide the tested candidate and TRX results. Installed automation remains explicit and is not run by hosted CI.

Create a GitHub environment named release. Set NUGET_USER to the publishing account. Configure NuGet Trusted Publishing for package CrestronHomeDriver.RainPoint.Irrigation, owner oznetmaster, repository RainPointCrestronDriver, workflow release-package.yml and environment release. No persistent NuGet API key is required.

The release workflow requires successful Tests workflow evidence for the exact tag commit and reruns offline/package validation before publishing. Additional required check contexts and app IDs can be set in RELEASE_REQUIRED_CHECKS using the same policy as the other repositories. No hosted processor gate is configured by default; review the recorded local processor and installed evidence before creating the release tag. Only an explicit manual invocation with a recorded reason can bypass configured processor checks.

## Publication

After validation, commit the prepared source, wait for the Tests workflow to pass, then create and push an annotated v1.0.0 tag at that exact commit. The tag starts Release Package, which exchanges its GitHub identity for a short-lived NuGet credential, publishes the verified NuGet distribution, and publishes the GitHub release using RELEASE-NOTES.md and the prepared assets. Manual retry accepts an existing version tag; it never creates or moves a tag. Existing NuGet versions are immutable.

Verify the GitHub assets, NuGet availability and a fresh-cache download after publication. Processor tests live in the separate CrestronLibraryTests solution and are not production release assets. Local account settings, credentials, test journals and processor evidence must not be committed.
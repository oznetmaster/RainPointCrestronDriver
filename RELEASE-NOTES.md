# RainPointCrestronDriver 1.1.4

This patch corrects the dependency and lifecycle issues described below.

- Use RainPointClient 1.2.2.
- Extend the existing processor test package to run the SDK entity lifecycle and programming regressions on net472/Mono as well as the portable tests.
- No new watering behavior or configuration migration is introduced.

## Installation

Use the driver package from [GitHub](https://github.com/oznetmaster/RainPointCrestronDriver/releases/tag/v1.1.4) or the matching NuGet distribution. Import the package, then update the existing driver in Home Setup or Configure Pro. Importing alone does not update the running instance. Existing pairing and configuration formats are retained; removal and re-add is not a routine update step.

Version 1.1.4 is above the private 1.1.3 submission-validation candidates; it does not claim that the suspended Crestron submission or physical watering acceptance tests have completed.

## Validation

Passed 143 workstation Debug and 143 Release test executions, plus 143 native net472/Mono processor tests. Processor validation used fake transports and synthetic credentials; no new physical-device acceptance or endurance run is claimed. Existing processor configuration and all pairing files were preserved. See [development history](DEVELOPMENT-HISTORY.md) for provenance and limits.

This GitHub/NuGet release does not replace any submitted Crestron package. Submission-specific PDFs and correspondence are excluded from public artifacts.

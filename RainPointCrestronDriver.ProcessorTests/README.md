# RainPoint driver processor tests

## Pending source update

The next test-package build includes the portable tests and the SDK entity lifecycle/programming tests on net472/Mono. The latter also continue to run in the net10.0 workstation harness. Packaged UI and schema fixtures are loaded beside the test assembly; transferred private inputs use a separate directory. Installed automation and live watering tests remain separate, explicitly selected suites. The released 1.0.0 artifacts described below are unchanged.

## NUnit 5 test package

Test package **1.0.0** uses **NUnit 5.0.0**. It is independent of the product version. [Download package](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0.pkg), [documentation](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0-Documentation.zip), [validation](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0.validation.json), [exact source revisions](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0.sources.json), and [SHA-256 checksums](https://github.com/oznetmaster/RainPointCrestronDriver/releases/download/v1.1.1/RainPointCrestronDriver.ProcessorTests-1.0.0-SHA256SUMS.txt) are attached to the existing product release. No product binary or NuGet version changed for this test update.

Validated on 1 October 2026: 96 offline cases passed in each of two runs from the packaged assembly on Windows. All suite identities were checked against source discovery. Live/manual tests and execution on the processor were not repeated during this migration; earlier hardware results do not certify this new package.

Independent test package 1.0.0 uses NUnit 5.0.0 and CrestronHomeNUnit SDK 2.2.0. Contains the portable net472 unit suite against the existing driver core. The net10.0 lifecycle tests and installed-automation suite run on Windows; they are not silently omitted or counted as processor tests.

Build with `BuildProcessorTestPackages=true`, `DeployAfterBuild=false` and `ProcessorTestSdkRoot` pointing to the locked SDK. The package is a Utility test host with its own automatically allocated port. Installing this test host does not install or update the production RainPoint driver.

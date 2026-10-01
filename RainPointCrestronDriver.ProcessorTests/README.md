# RainPoint driver processor tests

Independent test package 1.0.0 uses NUnit 5.0.0 and CrestronHomeNUnit SDK 2.2.0. Contains the portable net472 unit suite against the existing driver core. The net10.0 lifecycle tests and installed-automation suite run on Windows; they are not silently omitted or counted as processor tests.

Build with `BuildProcessorTestPackages=true`, `DeployAfterBuild=false` and `ProcessorTestSdkRoot` pointing to the locked SDK. The package is a Utility test host with its own automatically allocated port. Installing this test host does not install or update the production RainPoint driver.

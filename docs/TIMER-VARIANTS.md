# Timer variants

Version 1.1.0 adds one-zone HTV145FRF and two-zone HTV245FRF handling alongside the existing three-zone HTV345FRF. Driver 1.0.0 and client 1.1.0 targeted HTV345FRF.

Each timer's model determines its zone count. One shared v3 UI definition uses zone-visibility properties to hide absent zone navigation, controls and plan summaries. Hidden UI bindings remain defined with programmability disabled; absent-zone programming-only properties, commands and events are removed. The model subtitle follows the discovered timer. Aggregate status, history requests, plan reads and Stop all use only the actual zones; a failed stop does not prevent attempts on the remaining zones. Timer identity and assigned rooms remain based on hub ID and RF address.

The client uses the app's shared per-zone control and plan contract, with separate compact single-zone status framing. See the [client evidence and capability limits](https://github.com/oznetmaster/RainPointClient/blob/main/docs/TIMER-VARIANTS.md). Only HTV345FRF has project hardware validation. Added models have protocol-reference and offline coverage, not a claim of tested hardware support.

## Coordinated local validation

The default package reference is RainPointClient 1.2.0. To validate future coordinated changes against a local client checkout, pass `-p:RainPointClientProject=C:/Projects/RainPointClient/src/RainPointClient/RainPointClient.csproj` to `dotnet test` for the portable and SDK test projects, and to `dotnet build` for the production driver. Release validation uses the published NuGet package; the override is only for coordinated local development.

This work does not deploy to a processor, operate valves, or alter the published 1.0.0 package.
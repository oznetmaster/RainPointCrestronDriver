# Development history

## 28 September 2026 — first local implementation

Created `RainPointCrestronDriver.slnx` and a local Git repository. Production references the public RainPointClient 1.0.1 NuGet package.

Implemented one irrigation extension per supported timer on the configured hub. Six pages cover the overview, each of three zones, saved-plan summaries and diagnostics. Every zone supports explicit timed start and stop. Unknown readings remain unknown; command acceptance does not change zone state. Session replacement cancels old work and drains active operations. Stop-all attempts all three zones independently without transferring remaining commands to a replacement connection.

Validation:

- Solution Release build: no warnings or errors.
- 47 portable offline NUnit cases passed on net472 and 47 on net10.0.
- 11 desktop SDK cases passed, including entity registration, six-page schema validation, all UI bindings, connection changes and reported-state rendering.
- 2 merged-package smoke cases passed: typed JSON models through synthetic HTTP login/discovery and embedded MQTT root-certificate loading.
- Local ManifestUtil package build succeeded with the complete runtime dependency merge.

No cloud credentials were used. No hardware was operated, deployed to or rebooted. No client defect was found, and RainPointClient source was not changed.

Still unverified for release: processor execution of the packaged assembly; actual Crestron Home rendering, navigation and plan-summary wrapping; live read-only login/discovery/push recovery; controlled all-zone commands with explicit live-test authorization. A processor test package and its own workflow solution should be added before processor validation. No GitHub remote or release has been created for this new repository.

## 28 September 2026 � processor UI preview

Installed development build 1.0.0.2 on the requested MC4-R, using the public RainPointClient 1.0.1 package. The configured platform discovered the paired HTV345FRF and its room-only RainPoint Garden tile was commissioned in Office. The user confirmed that tapping the tile opens the zone overview.

The initial preview exposed a driver presentation defect: an observer reconnect event incorrectly disabled fresh REST feedback. The corrected driver preserves availability across that event; expired authentication and stale readings still disable starts. Five additional SDK tests cover reconnection, stale feedback, and on/off icons. All 16 SDK tests and both merged-package smoke tests passed. The package build completed without warnings or errors.

A 90-second processor observation confirmed continuous online/ready state with the push observer connected. During that observation, a user-started five-minute zone-1 run changed the tile to Zone 1 active and the irrigation-on icon, with zone 1 Watering and zones 2/3 Idle. This is device-reported feedback, not an independent physical flow measurement. Deployment sent no watering commands and required no processor reboot.

Full all-zone driver control validation, detailed page rendering and saved-plan wrapping remain outstanding. This preview deployment is not a substitute for the future dedicated processor NUnit workflow.

The user-started run reported Watering at 12:16:56 UTC and Idle at 12:21:56 UTC. The tile returned to irrigation-off and last usage updated to 16.4 L. The final check remained online/ready during an observer reconnect; all 33 pre-existing device identities were preserved and the deployment reservation was released. No remaining-time countdown is implemented yet; the device event timestamp requires exact-hardware semantic validation first.


## 28 September 2026 � countdown and completion feedback

Development build 1.0.0.3 replaces the configured-duration status line with time left calculated from the zone event/end timestamp. Reference inspection of the previously retrieved RainPoint app shows its irrigation screen uses EventOrEndTime through irrigationEndTime, adjusts the home offset/daylight rules, and subtracts current time. The client already exposes that timestamp and the home calendar table. No client package change was needed.

The driver uses the home timezone rather than the processor timezone, rejects ambiguous or uncovered daylight times, and never marks a zone closed merely because the countdown expires. Phase countdowns are labelled separately for cyclic watering/soaking. Actual closure also replaces the previous command feedback, fixing the observed stale Watering message. The countdown implementation is covered by offline tests; a live running-countdown comparison on this hardware is still pending.

Validation: 62 portable tests on each of net472 and net10.0, 16 desktop SDK tests and two merged-package smoke tests passed. Production packaging completed without warnings or errors.

Build 1.0.0.3 was installed and independently verified online/ready with MQTT connected. During a user-initiated zone-2 run, the processor exposed Watering, the irrigation-on icon, and Time left: 04:08; zones 1 and 3 remained idle. This confirms a usable end timestamp and home timezone on the paired hardware. All 33 previous device identities remained intact, and the deployment reservation was released. The duration selector still affects the next command only and remains editable during a run.

## 28 September 2026 — duration controls during watering

Development build 1.0.0.4 binds each duration selector to that zone's fresh-idle/start-available state. The corresponding setter also rejects edits while the control is disabled, so a delayed UI request cannot change the duration during active or pending watering. Other zones remain independent, and duration editing returns after reported idle.

All 20 desktop SDK tests passed, including per-zone locking/unlocking and a pending-command guard. Both merged-package smoke tests passed. Production package build completed with no warnings or errors.

## 28 September 2026 — compact controls and live-feedback polling

Development build 1.0.0.5 replaces the separate start/stop buttons with one state-dependent action per zone. Idle offers Start timed watering; active or pending start offers Stop this zone; pending stop displays disabled Stopping... until feedback or timeout. The existing explicit programmable start/stop commands remain unchanged. Duration controls retain their per-zone idle-state guard. Refresh status is now only on Diagnostics.

The local RainPointClient 1.1.0-preview.1 adds optional suppression of routine status reads after the current MQTT connection has a successful catch-up read. Existing client consumers retain periodic polling by default. This driver opts in, keeps known reported state available during a synchronized live connection, and uses the 30-second polling fallback when live updates are unavailable. Manual refresh still reads immediately. Neither connection health nor a status read proves physical valve position.

This preview references the sibling client source project. Both repositories remain local and unpublished; the client package must be published and the driver returned to a public NuGet reference before their coordinated release.

Validation:

- Client library/dashboard offline suite: 1,357 cases passed on net472 and 1,357 on net10.0. New tests cover optional polling suppression, default behavior, explicit refresh, disconnected fallback, reconnect catch-up and failed catch-up.
- Driver portable suite: 70 cases passed on each target; SDK suite: 23 cases passed; merged-package smoke suite: two cases passed.
- Production package built without warnings or errors; SHA-256 CDFB681F0A7656E32AC6FC0B6FD22D80D009A6806698B20FB1E695BFD1904DDA.
- Installed on the preview processor without reboot. Four read-only samples from 13:11:50 to 13:13:36 UTC showed PushConnected, online/ready and all zones idle, retaining the unchanged 13:04:46 timer report timestamp.
- Processor UI definition verified exactly one action button on each zone page, duration enable bindings, and Refresh status only on Diagnostics. All three live action labels were Start timed watering. All 33 pre-existing device identities were preserved and the deployment reservation released.

No watering commands were sent during this update. Active/idle button transitions have offline coverage; visual review of the new compact layout remains with the user.
## 28 September 2026 — programmable interface

Development preview 1.0.0.6 adds 26 read-only programmable properties, ten commands and 18 events on each timer. The original seven explicit start/stop commands retain their IDs; three new commands accept an explicit 1–120 minute duration without changing the UI selection. Per-zone properties expose known/unknown activity, device-derived seconds remaining, historical litres, alarm state/details, command feedback and selected duration. Timer properties expose activity/count, availability, connection and battery status.

Events follow consecutive known device reports: each zone's start/stop and alarm transitions, aggregate activity/all-idle, battery low/normal, and status availability loss/restoration. Initial and recovered readings establish baselines. Acknowledgements, repeated reports, unknown data and countdown expiry cannot invent watering transitions. Typed alarm and battery values are carried through the driver adapter rather than parsed from display text. No additional RainPointClient changes were required for this increment.

Validation: 77 portable NUnit tests passed on net472 and 77 on net10.0; 36 SDK tests and two merged-package smoke tests passed. The package built without warnings or errors. SHA-256: B21B4821F2F47E01D72F4C51C78910163969E916C9C9556D66C2986615B4D7E5.

The preview was updated on .244 without reboot or watering commands. At 13:29:35 UTC the Office RainPoint Garden child was online/ready with PushConnected, all zones idle, zero remaining seconds and clear alarms. New property values and all ten command registrations were verified through the processor. Historical usage was 16.6 L, 0.3 L and 0.0 L for zones 1–3. All 33 pre-existing device identities were preserved and the deployment reservation released. Events and programmable metadata have SDK-test coverage; execution of user-created Home automations has not yet been exercised on the processor.

See docs/PROGRAMMING.md for IDs, value semantics and event baselines. Client and driver remain unpublished.
## 29 September 2026 — NUnit 5 and idle Stop-all control

Both driver test projects now use released NUnit 5.0.0, retaining NUnit3TestAdapter 6.3.0 and NUnit.Analyzers 4.15.0. No asynchronous assertion migrations were needed in these fixtures. Portable tests passed 77 cases on net472 and 77 on net10.0. The SDK suite passed 37 cases and the merged-package suite passed two.

Preview 1.0.0.7 disables the overview Stop all button only when all three zones are confirmed idle with no pending commands. It remains available for active, pending or uncertain state while a session is available. A regression test covers idle, active, pending, stale, unknown and expired-authentication states.

Installed on .244 without reboot or watering commands. At 02:26:35 UTC the timer was online/ready with PushConnected, all zones idle, all per-zone buttons labelled Start timed watering and canStop false. All 33 pre-existing device identities and the ten programming command registrations were preserved; the deployment reservation was released. Package SHA-256: 62BB3658927BCA331CB071E1D63F80FE540B73143E2BE01A4B366371FF41215E.

Actual Crestron Home sequence/event execution is still being investigated and is not covered by these passing SDK tests. The existing released test workflow does not itself provide a Home sequence-programming client. Read-only inspection found the processor's advertised programming commands; no Home programming has been changed.
## 29 September 2026 — actual Home automation execution

Added an explicit .NET 10 Windows integration project using NUnit 5.0.0, NUnit3TestAdapter 6.3.0 and CrestronHomeDevTools 1.21.0. The project is included in the solution. Private settings select the installed driver and saved credentials/pins; normal offline discovery does not run either live fixture.

Read-only catalog inspection found all ten commands, all 26 driver properties (plus Home's built-in ready property), and all 18 custom events. Home also exposes property-change events. The execution fixture temporarily programmed the previously unused zone1MinutesChanged event: if zone1State equals Idle, invoke stopZone1. Changing the selected duration through extension:setPropertyValue triggered Home's sequence and the driver reported Sending stop at 02:57:41 UTC. The NUnit test passed. It removed its own sequence, restored event mode None and the original five-minute duration, verified all zones idle, and released its processor reservation at 02:57:43 UTC. No Start commands were sent.

Earlier attempts are retained as failures: a transient offline preflight, rejected direct/generic command routes, an immediate read before asynchronous property completion, and an observation window that missed brief action feedback. Each attempt restored its temporary programming and released its reservation. An HTTP acknowledgement alone is not execution proof. The passing run observes feedback while the trigger is processed.

This establishes a representative real Home event/condition/action path, not live execution of all custom watering, alarm and battery transitions. Production code and preview 1.0.0.7 were unchanged by these integration-test corrections. Client and driver remain unpublished.
## 29 September 2026 — dedicated account and plans-page cleanup

The installed development instance was moved from the personal account to the existing shared development account. That account has two homes, so its first connection with blank selection fields failed. Explicit selection of the existing shared home and hub restored PushConnected, online/ready status and idle readings. The timer instance and room assignment were preserved, and configuration reservations were released. The user initially reported an unresponsive touch-panel tile after reconfiguration, then confirmed that it opened again without a navigation-code change or panel/processor reboot. A stale panel binding is a possibility, not an established cause. The earlier hub-name change has not been established as the cause of the connection interruption, and metadata-change MQTT handling remains unverified.

Local preview 1.0.0.8 removes the redundant Refresh plans button and its unused translation. Opening Plans still invokes refreshPlans. The existing SDK/UI suite passed 37 tests; the package built with no warnings/errors and passed both merged-package tests. Package SHA-256: 24BA2C3381F272FBB547B73010355BE9F75E0CC15978B824604B88DE77FF5563. This preview has not been deployed; the working processor remains on 1.0.0.7. No watering commands were sent during this investigation.

## 29 September 2026 — app names and configuration notifications

Preview 1.0.0.8 now also removes Refresh status. App-assigned zone names are decoded from portDescribe in the client and bound to the zone rows, page titles and plan headings, with numbered fallback for blank names. The overview shows the hub and timer names. Existing navigation targets are retained; the user confirmed the intermittent zone-row navigation recovered before these changes.

The client exposes a typed home-configuration change event for MQTT command 04, scoped to the authenticated account and monitored home, with duplicate/older revision suppression. The driver queues coalesced configuration reads after these notifications and after MQTT reconnects, updates names and plans without replacing commissioned timer identities, and retains the last display on read failure. Background work is cancelled and awaited during shutdown. Configuration events do not refresh valve-state freshness or raise watering events. A late configuration callback after monitor shutdown is ignored.

Validation: 1,373 client/library/dashboard offline cases passed on each of net472 and net10.0; 83 portable driver cases passed on each runtime; 38 desktop SDK cases and two merged-package tests passed. The new tests cover scope/revision ordering, default and merged serialization, subscriber exceptions, shutdown, name fallbacks/immutability, stale-session callbacks, metadata-read failure, burst coalescing, transport-identity preservation, and unchanged watering controls. Build: zero warnings/errors. Package SHA-256: 68338CE085875F8FD047328658ED623817C86C51067EFC43F0536A73C0D00ACB.

Installed on .244 using the saved support account and explicit shared-home/hub selection. All 35 pre-existing device identities and room assignments were preserved, and the processor reservation was released. Read-back verified v1.0.000.0008, UI version 3.0, room-only tile navigation, named zone bindings, and absence of both refresh buttons. The first readiness check failed because MQTT was still Reconnecting, although fallback status was online. A subsequent read-only check confirmed PushConnected, online/ready, all zones idle, and No command sent for every zone; no extra login or reconfiguration was issued. The initial failed readiness result remains retained. No watering commands or processor/panel reboots were sent.

Displayed names were Feolin Greenhouse, HTV345FRF, Greenhouse right, Greenhouse left and the fallback Zone 3. This verifies discovery/read-back of assigned names. An actual app rename delivered as a configuration notification to the shared account still requires observation; offline protocol and driver refresh tests are not claimed as that live proof. Client and driver remain unpublished.

## 29 September 2026 — assigned-name page heading

At the user's request, preview 1.0.0.9 uses the app-assigned hub name for the main detail-page title and HTV345FRF for its subtitle. Zone pages retain their assigned zone-name titles with the model subtitle. The SDK suite passed 38 cases and both merged-package tests passed. Package SHA-256: 6BCC5BEDF7313ECFCB1E2A0A24768B8D2C25FDB75C06C716BECD28EA4CFEF535.

The update to .244 completed and preserved the saved account and device identities. An immediate heading assertion failed; a subsequent read-only inspection verified the actual MainPage title binding to hubLabel, the TimerModel subtitles and PushConnected/online/ready status with all zones idle. No command was sent. The failed check is retained separately from that successful later observation.

## 29 September 2026 — dated per-zone usage

Preview 1.0.0.10 adds a Last recorded usage section to each zone page. Volume and date are selected from the same latest available watering/water-usage history record, never joined to a live timer report or a countdown timestamp. Home-local time and its supplied timezone are preserved; missing local time falls back to an explicitly labelled cloud UTC timestamp. Zero litres remains a valid reading. Empty history does not invent a zero or date. The display retains an older dated record when a read fails and labels that failure.

History requests are read-only, serialized and coalesced per zone. Connection/reconnection reads history; a fresh reported active-to-idle transition queues an immediate read and two retries 30 seconds apart. There is no periodic history polling and no added refresh button. Existing status-and-navigation rows remain unchanged; they provide no page-open command callback. Each query is bounded to the latest 50 returned events. Background reads are cancelled/awaited at shutdown; old-session callbacks are ignored and early history is held until the child catalog is published. The existing programmable last-reported-usage properties retain their original semantics; dated history is currently a UI display.

Validation: 91 portable NUnit 5 cases passed on net472 and 91 on net10.0; 41 SDK cases and two merged-package tests passed. New tests cover date/volume association, zero usage, incomplete/unrelated records, cloud-time fallback, all-zone completion refresh, duplicate reports, bounded retries, cancellation, serialized reads, early callbacks and session replacement. Build: zero warnings/errors. Package SHA-256: E2AE89BD16F92BAB55A1664DB7B14E1B35BE46C86B0287B2E3A70C363968D5EE.

Installed on .244 using the saved support account. Read-back verified preview 1.0.000.0010, assigned-name/model headers, dated-usage controls, online/ready and PushConnected. All 35 existing device identities/room assignments were preserved and the processor reservation released. History returned 16.6 L at 28 September 2026 13:59:45 for Greenhouse right, 0.3 L at 28 September 2026 14:18:04 for Greenhouse left, and 0 L at 27 September 2026 10:56:37 for Zone 3; each record reports GMT+01:00. All zones were idle, with no valve commands or reboot issued. This verifies history retrieval/display through the deployed session; completion-triggered history retries have offline coverage and were not exercised by a new watering run. Live app-rename notification delivery remains a separate observation. Nothing was published to GitHub or NuGet.

## 29 September 2026 — shared-account notifications and release validation

The support-account observer now registers its private account MQTT identity. A real zone rename arrived as command 04 with an empty middle description field; the client decoder incorrectly rejected that field. Preview 1.0.0.15 includes the corrected validation and removes all temporary payload capture code. The offline client tests cover both envelope forms, routing and revision deduplication. The first installed read-back showed Garden Faucet from startup discovery; a rename while this corrected preview stays connected remains pending.

Preview package SHA-256: FF97E74C23CBE55FA3D961D2E11B7DDE2A88E1D1E99796354FDB0DB3782704F4. Deployment preserved all 35 existing device identities, parent/room assignments and the support account. The platform and timer returned online/ready with PushConnected; all zones were idle and no command was replayed. No processor reboot was used.

The one-minute zone-1 Home automation fixture passed on preview 1.0.0.14. It observed a decreasing device-deadline countdown, correct button/control states, natural completion, execution of the custom stopped event, and a new dated 3.1 L usage record. Its temporary programming was removed and selected durations restored. Zones 2 and 3 received no watering commands. This was device-reported validation, without independent physical observation. See tests/README.md for the retained failed first command-dispatch attempt and successful corrected workflow.

Final local results so far: 91 portable driver cases per framework, 44 SDK cases and two package cases passed. The separate client processor workflow passed 1,380 processor cases plus 2,760 desktop cases, removed its test instance and released its reservation. Its first attempt stopped before tests because the local certificate pin was inadvertently altered while updating the expected case count; the pin was restored against independent TLS and trusted-worker evidence. The processor certificate was unchanged. Nothing has been committed, tagged, pushed or published for this update.

The subsequent app rename from Garden Faucet back to Greenhouse Faucet appeared in preview 1.0.0.15 without a driver reload, manual refresh, or new explicit sign-in. Read-only inspection confirmed the updated zone-3 name, online/ready and PushConnected, with all zones idle and every command still No command sent. This completes the real shared-account rename check. The disabled-plan update check remains pending. Temporary processor trace capture was removed after preserving private diagnostic evidence; the installed build contains no capture code.

The disabled zone-1 plan check also passed: the driver's summary updated automatically to Off 08:00 IntervalDays / Irrigation / 5 min. No watering command or manual refresh was issued; all zones remained idle. Both shared-account configuration checks are now verified. The temporary plan remains disabled pending user removal and read-back.

The owner subsequently deleted the temporary plan in the app. Read-only inspection confirmed that all three zone summaries returned to No saved plans / No rain delay configured without a reload or refresh command. The driver remained online/ready with PushConnected, Greenhouse Faucet remained assigned to zone 3, all zones were idle, and no command had been sent. Temporary test state is restored.

## Public client package and final installed validation — 29 September 2026

RainPointClient 1.1.0 is published on GitHub and NuGet. A fresh-cache consumer restored the public package from NuGet.org and ran on net472 and net10.0. All 13 GitHub asset checksums matched; the public package payload matched the release asset apart from the NuGet signature. The driver now references that public package rather than the sibling source project, and its packaged dependency notices were refreshed from the released package.

Against the public package, 91 portable cases passed on each runtime, 44 SDK cases and two merged-package cases passed. The package build completed without warnings or errors. Preview 1.0.000.0016 was installed on .244 without a processor reboot. Package SHA-256: DBE77BD0FAC57F6FC97DCABD45FE8FF2F7670BCADCB2DBFA0D401FBAED2B63C3.

Deployment preserved all 35 existing device identities and their parent/room assignments, and retained the support account. The read-only installed NUnit programming catalog test passed on this exact version. Final read-back confirmed online/ready, PushConnected, the assigned hub and zone names, all three zones idle, no saved plans, and No command sent for every zone. No additional watering was performed. The earlier successful one-minute run remains the live execution evidence; the app rename and plan creation/deletion checks are complete.

The driver remains a local preview with no GitHub repository or published driver release. The shared CrestronLibraryTests changes remain local and have not been pushed. Private deployment and installed-test evidence is retained on the trusted worker under rainpoint-publicclient-20260929a and rainpoint-automation-public-catalog-20260929a.

## 29 September 2026 — v1.0.0 publication preparation

Prepared the initial release README, changelog, publication notes, installation instructions and release guide. Release version 1.0.0 uses manifest build 1.0.000.0017 so installations can move forward from preview build 16. Production behavior is unchanged. The NuGet distribution CrestronHomeDriver.RainPoint.Irrigation contains the validated installable package and notices, without application assembly or runtime dependency exports.

The SDK test harness now restores its compatibility assembly from the public ManifestUtil 29.0.10 package. Its SHA-256 matches the previously used private local copy. All 91 portable cases on each runtime and 44 SDK cases passed using that public reference; two merged-package cases and 40 release-policy scenarios passed. Packaging completed without warnings or errors. The hosted Tests workflow reproduces these checks and retains candidate assets. Release Package uses exact-commit validation and NuGet Trusted Publishing. Publication remains a separate tag-triggered action.
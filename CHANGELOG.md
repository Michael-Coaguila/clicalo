# Changelog

All notable changes to Clícalo are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the product
follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html), starting at 2.0.0 to continue the
numbering of Macro Quick Access. This file is technical and written in English; user-facing release notes in
Spanish and English are published with each release.

## [Unreleased]

Milestone M0 · Foundations and harness, milestone M1 · blocking spikes S1, S3 and S4 (closed by the maintainer's
decision on real evidence, see `docs/testing/spikes/M1-closure.md`), and milestone M2 · Walking skeleton (in
progress).

### Added

- Solution skeleton (`Clicalo.slnx` and the `Core.slnf` filter): the eight main-process assemblies (Domain,
  Application, Presentation, UI.Wpf, Platform.Core, Platform.Windows, Infrastructure and App), the Native AOT
  hosts `Clicalo.Sentinel` and `Clicalo.Launcher`, the Roslyn generator and analyzer projects, the test
  projects, the `InputProbe` and `Clicalo.DevCli` tools and the `build/` project behind `cl`.
- Shared build settings: C# 14, nullable reference types, warnings as errors, `latest-recommended` .NET
  analyzers, Meziantou.Analyzer and the banned API analyzer, code style enforced at build time,
  deterministic builds and a single `artifacts/` output folder.
- Central Package Management with exact versions and transitive pinning, NuGet lock files (locked restore
  in CI, kept in CRLF) and NuGet auditing of transitive dependencies.
- A minimal JSON reader that reports line and column, and shared helpers for data-driven incremental source
  generators.
- `RepoPaths` in `Clicalo.TestKit` to locate the repository root, `data/` and the design handoff from tests.
- The original design handoff (read-only), the architecture blueprint 1.1, the technology decision record
  and the requirements catalog.
- Architecture decision records ADR-0001 to ADR-0017 with an index and a template (MADR 4).
- Architecture overview (arc42 with C4 diagrams), testing strategy, tooling guide and the register of
  deviations from the blueprint.
- Threat model and privacy documentation, developer setup and voice-and-touch workflow guides, a note on
  what is binding in the design handoff, and a guide to reading the requirements catalog.
- Project governance files: bilingual README, CONTRIBUTING, SECURITY, SUPPORT, PRIVACY and
  CODE_SIGNING_POLICY, a code of conduct adopting the Contributor Covenant 2.1, and AGENTS.md for coding
  agents.
- Data catalogs with JSON schemas (`data/catalogs`: keys and their Win32 mapping, timings, sizes and touch
  presets) and `CatalogGenerator`, which turns them into typed constants of `Clicalo.Domain` (`KeyIds`,
  `Timings`) and reports data errors as `CLCC` compiler errors.
- The i18n pipeline: `data/i18n` imported from the design handoff with a hand-reviewed recipe (669 handoff
  keys, CLDR plurals, named placeholders), `LocalizationGenerator` (`MessageKey`, `L`, `MessageCatalog`,
  `CLCI` errors), the localizer with CLDR plural rules, the `i18n-check` and `i18n-import` developer commands
  and a hand-reviewed golden of every handoff text with placeholders.
- Design tokens (`data/tokens`) and `TokenGenerator`: OKLCH to sRGB with CSS Color 4 gamut mapping, the
  generated theme palettes, and every contrast pair measured against WCAG as `CLCT` compiler errors.
- Product-rule analyzers `CLC0001`, `CLC0003`, `CLC0004`, `CLC0006` and `CLC0010`.
- Automated architecture enforcement: layer rules with ArchUnitNET, allowed project dependencies as data,
  banned API lists per layer with per-file exceptions, and `sensitive-paths.json` with the `adr-check`
  command (`CLCA010`).
- `tools/InputProbe` and `Clicalo.TestKit.Windows`: the probe session, a guarded test keyboard injector,
  render snapshots, and desktop tests (`Requires=Desktop`) for virtual-key, scan-code and Unicode injection.
- `cl` (`cl.cmd`, `cl.ps1`), the build orchestrator with the verbs `setup`, `build`, `fast`, `test`, `desk`,
  `fix`, `check` and `clean`, plus `i18n-check`, `i18n-import` and `adr-check`; one final line for Narrator
  and a Markdown failure report.
- GitHub workflows: `pr.yml` (`verify` = `cl check`, `adr`, `dco`, and ARM64, CodeQL and Scorecard for the
  public repository), `pr-title.yml` (Conventional Commits titles) and `s0.yml` (spike S0, desktop tests on
  hosted runners); Renovate, CODEOWNERS, issue forms, a pull request template and VS Code tasks.
- Milestone M1 contracts for the blocking spikes S1, S3 and S4: `Clicalo.Domain.Geometry` (physical points and
  rectangles), `Clicalo.Domain.Touch` (pointer frames, touch settings and targets, `GestureRecognizer`,
  `TouchFilter`), the foreground leases of `Clicalo.Application.Foreground`, the foreground and surface ports of
  `Clicalo.Application.Ports`, `NonActivatingWindow` and its registry, guard and integrity check in
  `Clicalo.UI.Wpf.Windowing`, the pointer layer setup in `Clicalo.UI.Wpf.Pointer`, the accessible `ShortcutTile`
  and `LiveAnnouncer` in `Clicalo.UI.Wpf.Automation`, and the SysEvents, foreground and tray adapters of
  `Clicalo.Platform.Windows`; the `Clicalo.Windowing.IntegrationTests` project and the `tools/SpikeLab`
  laboratory; the spike scripts and the package ownership map in `docs/testing/spikes/`.
- Milestone M1 windowing (spike S1): non-activatable surfaces (`NonActivatingWindow` with `ShowPassive`,
  `HidePassive` and `MovePassive`, the common surface hook, a thread-scoped `ActivationVeto` around the WPF show and
  the forwarded `WM_DPICHANGED`, and the hidden `OwnerAnchor`), `ActivationGuard` (one REG-01 violation per
  activation, `reg01.violations`, probable cause, and a deferred judgment of an activation message that the
  foreground does not confirm yet, and an open violation that ends when the foreground is seen outside the
  process, with the `Windowing.ActivationRecheck` timing), the thread-safe `SurfaceRegistry` and `SurfaceIntegrityCheck`;
  the guarded `SyntheticPointer` (finger, pen and mouse only into the test process's own windows) and the S1
  headless and desktop tests.
- Milestone M1 touch: the TAC-002 `TouchFilter` and an allocation-free `GestureRecognizer` (tap, long press, hold,
  swipe, palm, per-target debounce; REG-02, EJE-004, EJE-006, CUA-005, CUA-014), `PointerInputSource` (touch, pen
  and mouse frames in physical pixels, timestamped from `PerformanceCount`), `GestureHost`,
  `PointerSetup.EnableMouseInPointer` and `IsTouchFeedbackDisabled`, and the `Touch.PalmContactMinPx` and
  `Touch.PointerStampMaxAge` timings; preset tables, CsCheck properties and desktop tests with a tap-to-gesture
  latency budget.
- Milestone M1 UI Automation (spike S3): the accessible `ShortcutTile` and its peer (one pattern, help text, item
  status, asynchronous Invoke), `LiveAnnouncer` with the workaround for the WPF notification string defect,
  `ThemeScope` for system high contrast, the focus ring and the 44 × 44 touch target; a UIA rule verifier, the S3
  desktop tests, an out-of-process Axe scan, an out-of-process UI Automation client for UIA009 (UIA3, and the
  managed client of .NET as an explicit test that reproduces its activation of a surface) and the
  `Upstream/WpfNotificationBstrTests` watch test.
- Milestone M1 foreground (spike S4): `ForegroundOrchestrator` (typed leases, the rights ladder per origin, verified
  restoration, and the arbiter that `ActivationGuard` reports to) and the Platform.Windows adapters
  `SysEventsThread`, `ForegroundControl`, `ForegroundMonitor`, `InternalRightsHotkey`, `TouchKeyboard`, `TrayIcon`
  and `TrayMenuHost`; application tests with fake ports and desktop tests against InputProbe, and the S4 desktop
  tests on the real surfaces (the search opened by touch and by the global shortcut, a denied lease, the Control
  Center of CCM-004 and Win+H only for a focused own field).
- `tools/SpikeLab`: the laboratory that composes the real M1 pieces for the maintainer's touch-and-voice runs of
  S1, S3 and S4, with a guided script engine, automatic checks and JSON and Markdown reports, and its unit tests.
- `InputProbeSession.RequestForegroundAsync`, and the `Injects=ReservedKeys` trait: `cl desk` runs the desktop tests
  that inject right Ctrl or AltGr only in continuous integration, and the test keyboard injector refuses those keys
  anywhere else.
- `DesktopSessionLock` (`Global\Clicalo.DesktopTests`): one desktop test run or SpikeLab session at a time; the helper
  processes a desktop test starts run inside its session. The final line of `cl` counts the `total` of `dotnet test`
  (skipped tests said apart), read from one `<AssemblyName>.trx` per test module.
- Milestone M2 contracts (bodies still `NotImplementedException`, split among five packages in
  `docs/testing/spikes/M2-ownership.md`): the Domain core (`Primitives`, `Errors` with `Result<T>`, `Privacy` with
  `SecretText`, normalized `KeyChord` and `CanonicalChord`, the `Library` model with every action kind and the
  `ShortcutLibrary` aggregate, `UserSettings` with the ranges of docs/02, `UserDocument` with its invariants and undo
  slices, the `Commands` module, `KeySafety`, `Execution` with the two-lane `EngineEvent`, `EngineEffect`,
  `EngineReducer` and `ActivationPolicy`, and `Migration.V1`); Application (`DocumentStore` with undo,
  `PersistenceScheduler`, `EngineHost` and its mailbox, `TwoStepConfirm` and `ConfirmationToken`, and the engine and
  persistence ports); Platform.Core (the ledger v2 layout, `InjectionGate`, `LowLevelInjector` and the Sentinel start
  contract); Infrastructure (the `major.minor` envelope, `AtomicFile`, quarantine, repositories, backups, the v1
  importer and `SafeZipReader`); and Sentinel's guardian loop. ADR-0018 (proposed) fixes the Sentinel start contract,
  the ledger v2 layout and the document envelope 1.0.
- Test projects `Clicalo.Infrastructure.Tests`, `Clicalo.Sentinel.Tests` and `Clicalo.Performance`.
- Milestone M2 implementation, integrated from the domain, engine, persistence, migration and app packages: the
  library invariants, settings schema, frequents, duplicates, profile resolution, the 27 document commands and
  `DocumentStore` with undo by slices and single-use confirmation tokens; the pure engine reducer and planners,
  `EngineHost`, the ledger section, `InjectionGate` with the only `SendInput`, Sentinel and its supervisor, the
  emergency release and the release on lock and suspend; atomic writes, the load and recovery chain, DPAPI texts,
  backups, autosave and the redacting log; the v1 tokenizer, converter, reader and `SafeZipReader` with anonymized
  real fixtures and the `anonymize-v1` DevCli verb; and `Clicalo.exe` with single instance, the minimal panel with its
  panic strip, the tray, the seed or v1 migration on a first run, and the `cl run`, `cl note` and `cl perf` verbs.
  Spikes S5, S7, S9 and S11 are tests; the chaos and performance runs are CI-only.
- `Clicalo.App.Tests`, headless tests of the composition root, and 35 new texts in Spanish and English pending
  ratification (catalog §6.1, R-11).
- Corrections from the M2 verification. Engine (D-22, ADR-0019 proposed): the heartbeat and the engine's marks go
  through the generation fence, so a zombie engine stops on its first turn; releases refused by the secure desktop are
  sent again by «Release all», every terminal event and the return of the input desktop (UAC, Ctrl+Alt+Del); chords,
  texts and clicks that `SendInput` takes only in part are balanced under the fence; the internal chords are sent by the
  engine with its generation; without a running guardian the emergency never ends the process, and the panel says
  that the key protection is off once Sentinel is no longer restarted; a key pressed again while its release is
  pending is freed by its holder's release; suspending flushes the document, the usage and the queued copies, and a
  flush its limit cuts leaves them pending for the autosave and the exit. Persistence and IPC (D-21): one persistence consumer that writes the copy
  before a destructive change ahead of the document, the startup reads off the UI thread, retried seed and migration
  saves, verified single-instance clients (session, user SID and integrity) and invariant D13 watched. Exit criteria
  (D-23): `data/catalogs/budgets.json` with its schema and a required `perf (x64)` job that enforces the touch to
  `SendInput` p95 of 50 ms, the manual `lab.yml` workflow, the tray test with the real Notepad in CI, a test that
  rejects double-encoded text in every text file, and reduced counterexamples of the engine properties kept as
  regressions.
- Starter kit (user decision D2, ADR-0021): `data/content/starter.json` with its schema offers «Basics» (the
  universal shortcuts of General and Always visible) marked by default and the nine templates unmarked;
  `StarterLibrary` and the `FirstDocument` use case build the first document from a selection (nothing marked starts
  empty, «Skip» applies the default), and `StarterContentFiles` loads and validates the kit, the seed and the
  templates at run time. Two new texts in Spanish and English, `kitBasics` and `kitBasicsD` (the description awaits
  ratification, R-15 in §6.1 of the catalog).
- `docs/architecture/contracts.md`: the command-line contracts between `Clicalo.exe` and Sentinel (protocol 2, the
  relaunch and Sentinel's exit codes).

### Changed

- A first start installs the default starter kit («Basics» only) with new ids for every shortcut, instead of the raw
  seed with its catalog ids; the `content` folder next to `Clicalo.exe` now also holds `starter.json` and the
  templates. `Infrastructure.Content.SeedDocument` is replaced by `Infrastructure.Catalogs`.
- Templates bind several processes (PQ-45 decided): Browser binds Chrome, Edge, Firefox, Brave and Opera and Mail
  binds classic Outlook and the new Outlook (`olk.exe`); templates install the variant of the programs language. After
  checking each program's official documentation, Browser reloads with Ctrl+R, Mail creates with Ctrl+N and sends with
  Ctrl+Enter, and «Video call» is renamed «Zoom», since its shortcuts are Zoom's.
- Sentinel resends a key release refused by the secure desktop every heartbeat until the session is unlocked, and
  relaunches Clícalo only afterwards; other refusals are retried for at most `Timings.Guardian.RefusedReleaseWait`
  (30 s). A resend never separates the menu mask from its Alt or Win key. The Sentinel start-up contract moves to
  protocol 2 with a seventh argument, `--refused-release-wait-ms` (ADR-0018, user decision D3 of 2026-10-03).

### Removed

- The import from Macro Quick Access v1, by the user's decision D1 of 2026-10-03 (ADR-0020, catalog §6.2): Clícalo
  no longer reads `profiles.json` v1, its language backups or its `.zip`. Removed the `Clicalo.Domain.Migration.V1`
  module, `V1Reader`, `V1Importer` and `SafeZipReader`, the `--migrate-v1` option and the `migration-v1.pending`
  mark, the `v1-original` backup kind, the `anonymize-v1` DevCli verb, the v1 tests and anonymized fixtures, the
  `Timings.Import.Zip*`, `Timings.Import.V1Max*` and `Timings.Backups.MigrationCardVisibility` limits, and the texts
  `migT` and `migD` (now `retired` in `data/i18n/handoff-import.json`) with `migTProfiles`, `migTShortcuts`,
  `migFailT`, `migFailD`, `migRetry` and `migReportT`. The import and export of Clícalo's own format and the
  migrations between versions of its own schema are unchanged; requirements MIG-001 to MIG-009, BIE-002, COP-001 and
  EC-MIG-01 to EC-MIG-05 are retired and REG-08 no longer covers a v1 migration.

### Fixed

- REG-01: a forced activation of a surface could get past `ActivationGuard` when Windows gave the foreground back
  without deactivating the panel, and the panel kept the foreground. A violation now ends with its burst, a repeated
  activation message or a deactivation, and the WA_INACTIVE of a leased activation reaches WPF whole (D-15).
  `ForegroundOrchestrator` confirms an attempt at the first look that finds the window (every new
  `Timings.Foreground.RestoreVerifyInterval`, 5 ms) or at the monitor's report, never retries over an app the user
  switched to, and a queued violation restore does nothing once the violation is over (D-17).
- Test harness: the `Clicalo.Application.Tests` hang (the fake clock moved before a delay was armed), the rights
  hotkey wait measured on the timer's own clock, the high contrast tree read after the window template is applied
  again, the panel tap latency measured with one synthetic device per kind and a checked warm-up tap (D-24), and COM
  diagnostics in the out-of-process UIA client.

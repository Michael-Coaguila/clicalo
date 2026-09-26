# Changelog

All notable changes to Clícalo are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the product
follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html), starting at 2.0.0 to continue the
numbering of Macro Quick Access. This file is technical and written in English; user-facing release notes in
Spanish and English are published with each release.

## [Unreleased]

Milestone M0 · Foundations and harness (in progress).

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

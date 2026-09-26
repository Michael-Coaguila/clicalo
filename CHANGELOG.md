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

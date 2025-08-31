# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2025-09-01
### Changed
- Updated `release.yml` workflow to automatically generate release notes from `CHANGELOG.md`.
- Release notes now include a link to the full changelog in the tagged revision.

### Fixed
- Improved CI/CD reliability by handling package restore and publish steps more consistently.
- Reduced race conditions in publish step for Windows runtime by adjusting single-file options.

[Full changelog](https://github.com/ikyslyi/GCColorer/blob/v0.1.1/CHANGELOG.md)

## [0.1.0] - 2025-08-31
### Added
- Initial release of **GCColorer**.
- CLI tool built on .NET 10 with Google Calendar API integration.
- Support for recoloring calendar events based on rules (`ColorRules`).
- Support for deleting events by rules (`DeleteRules`).
- Support for copying events from one date range to another (`--copyTo`).
- Configuration via `appsettings.json` with OAuth2 credentials and rules.
- CI/CD with GitHub Actions:
  - Continuous integration build & test workflow.
  - Release workflow that publishes self-contained binaries for Windows, Linux, macOS.
- Example configuration file (`appsettings.json.example`).
- MIT license, README with usage instructions, and initial documentation.

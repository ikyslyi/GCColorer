# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

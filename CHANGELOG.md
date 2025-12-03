# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.1] - 2025-12-03
### Changed
- Renamed `MaintenanceBypassController` to `AccessController` to better represent its future role as an API key–based access system. Routes remain unchanged.
- Updated `SOTClient.js` to automatically resolve API paths based on the actual site root, enabling OOS Core to run reliably in domain subdirectories.

### Fixed
- Restored missing confirmation modal partial in the admin interface. Its removal during refactoring caused delete actions to appear unresponsive.
- Fixed a logic error where deleting a product category would delete the corresponding ingredient category instead. Deletion now targets the correct entity.

# Contributing Guidelines

Thank you for helping improve the NMS Save Editor! 

## Branching Strategy
* `main`: Production-ready, stable releases.
* `develop`: Active integration branch for upcoming features.
* `feature/*`: Specific feature branches carved off `develop`.

## Code Style Requirements
* Follow official Microsoft .NET coding conventions.
* Use meaningful, strongly-typed naming conventions for game state properties.
* All asynchronous methods must append the `Async` suffix and accept `CancellationToken` where appropriate.

## Pull Request Process
1. Fork the repo and create your branch from `develop`.
2. Ensure all local tests pass (Phase 5 framework).
3. Submit a PR targeting `develop` with a clear breakdown of changes.
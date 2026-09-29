# dddlib

A domain driven design library for .NET designed to take the pain out of coding domain models, with a persistence
companion for in-memory and SQL Server storage of aggregate roots as mementos or event streams.

- [Documentation](docs/README.md), starting with the [quickstart](docs/quickstart.md)
- [Migrating from v1](docs/migrating-from-v1.md)
- [Port plan](PLAN.md) for the v2 work in progress

## Building

Requires the .NET 10 SDK and Docker (the SQL Server tests run in a Testcontainers container).

```shell
dotnet build
dotnet test
```

## Packages

Until the packages are on nuget.org they are published to this repository's GitHub Packages feed: prereleases
from every push to `main` (`2.0.0-preview.0.N`) and releases from `v*` tags. The feed needs a GitHub personal
access token with the `read:packages` scope. Add the source once:

```shell
dotnet nuget add source "https://nuget.pkg.github.com/dddlib/index.json" --name dddlib-github --username <github-user> --password <token> --store-password-in-clear-text
```

Then reference the packages as usual, allowing prereleases:

```shell
dotnet add package dddlib --prerelease
dotnet add package dddlib.Persistence --prerelease
dotnet add package dddlib.TestFramework --prerelease
```

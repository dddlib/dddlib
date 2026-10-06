# dddlib

A domain driven design library for .NET designed to take the pain out of coding domain models, with a persistence
companion for in-memory and SQL Server storage of aggregate roots as mementos or event streams, an event dispatcher
and projections for read models.

- [Documentation](docs/README.md), starting with the [quickstart](docs/quickstart.md)
- [Migrating from v1](docs/migrating-from-v1.md)
- [Port plan](PLAN.md) for the v2 work in progress

## Building

Requires the .NET 10 SDK and Docker (the SQL Server tests run in a Testcontainers container).

```shell
dotnet build
dotnet test
```

Each SQL Server test project starts its own container, and `dotnet test` runs the projects in parallel. To run them
all against one server instead, start it and set `DDDLIB_TEST_SQLSERVER` to a connection string for its master
database (use `127.0.0.1` rather than `localhost` for a port Docker publishes):

```shell
docker run -d --name dddlib-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=<password> -p 14333:1433 mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04
DDDLIB_TEST_SQLSERVER="Server=127.0.0.1,14333;User Id=sa;Password=<password>;TrustServerCertificate=True" dotnet test
```

Every test class creates and drops a database of its own, so projects can share the server.

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
dotnet add package dddlib.Persistence.SqlServer --prerelease
dotnet add package dddlib.TestFramework --prerelease
dotnet add package dddlib.Persistence.EventDispatcher --prerelease
dotnet add package dddlib.Persistence.EventDispatcher.SqlServer --prerelease
dotnet add package dddlib.Persistence.Projections --prerelease
dotnet add package dddlib.Persistence.Projections.SqlServer --prerelease
```

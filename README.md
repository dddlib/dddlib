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

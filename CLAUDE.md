# dddlib v2

Ground-up port of dddlib (a domain driven design library for .NET) to modern .NET.

- Read `PLAN.md` first. It holds the scope, decisions, phase order, test conventions and the scenario inventory that defines done.
- Legacy reference source: `C:\Users\cameronfletcher\Development\code\git\dddlib\dddlibv1` (branch `dev`). Read it for behaviour and error text; do not copy its build or serialization code.
- Value objects are C# records. There is no `ValueObject<T>` base class.
- Tests use TUnit only. Assertions use TUnit's `Assert.That`. SQL Server tests use Testcontainers and need Docker.
- Follow the working rules in section 9 of the plan: red, green, commit; never delete a scenario.

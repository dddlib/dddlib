; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DDDLIB001 | dddlib | Error | Entity has more than one natural key
DDDLIB002 | dddlib | Warning | Event handler takes a value type
DDDLIB003 | dddlib | Warning | Event handler is public
DDDLIB004 | dddlib | Warning | Value object has no public properties
DDDLIB005 | dddlib | Error | Assembly has more than one bootstrapper
DDDLIB006 | dddlib | Error | Bootstrapper has no default constructor
DDDLIB007 | dddlib | Info | Domain type could be partial
DDDLIB008 | dddlib | Warning | Applied event has no handler
DDDLIB009 | dddlib | Warning | Event handler takes an abstract class or an interface
DDDLIB010 | dddlib | Warning | Event handler applies an event
DDDLIB011 | dddlib | Warning | Event handler throws
DDDLIB012 | dddlib | Warning | Property is saved but never loaded
DDDLIB013 | dddlib | Warning | Aggregate root overrides only one of GetState and SetState
DDDLIB014 | dddlib | Warning | Aggregate root cannot be reconstituted
DDDLIB015 | dddlib | Warning | Aggregate root has no natural key
DDDLIB016 | dddlib | Error | Natural key attribute has no effect
DDDLIB017 | dddlib | Warning | Natural key does not round-trip
DDDLIB018 | dddlib | Error | Value object does not derive from ValueObject of itself
DDDLIB019 | dddlib | Warning | Value object property is compared by reference
DDDLIB020 | dddlib | Warning | Mapping is not configured
DDDLIB021 | dddlib | Error | Bootstrapper selects a different natural key
DDDLIB022 | dddlib | Error | Natural key selector is not a property of the entity
DDDLIB023 | dddlib | Warning | Event or memento cannot be loaded
DDDLIB024 | dddlib | Warning | Mapping does not fit how it is used
DDDLIB025 | dddlib | Warning | Entity is sealed
DDDLIB027 | dddlib | Warning | Reconstitution constructor is not accessible to derived types

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

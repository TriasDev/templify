; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TBG001 | TemplifyBuilder | Warning | TypedPathGenerator: a data property is not generated because the receiver type already has a member of that name
TBG002 | TemplifyBuilder | Error | TypedPathGenerator: the generated extension properties need C# 14

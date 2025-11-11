```

BenchmarkDotNet v0.15.6, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 9.0.0 (9.0.0, 9.0.24.52809), Arm64 RyuJIT armv8.0-a
  Job-YFEFPZ : .NET 9.0.0 (9.0.0, 9.0.24.52809), Arm64 RyuJIT armv8.0-a

IterationCount=10  WarmupCount=3  

```
| Method                                  | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|---------------------------------------- |------------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| PlaceholderReplacement_10_Placeholders  |    77.82 μs |  1.995 μs |  1.187 μs |  1.00 |    0.02 |  18.5547 |   1.4648 |  114.38 KB |        1.00 |
| PlaceholderReplacement_50_Placeholders  |   245.21 μs | 28.466 μs | 18.829 μs |  3.15 |    0.24 |  46.8750 |   7.8125 |  288.37 KB |        2.52 |
| PlaceholderReplacement_100_Placeholders |   439.49 μs | 41.119 μs | 27.198 μs |  5.65 |    0.34 |  82.0313 |  19.5313 |  503.09 KB |        4.40 |
| PlaceholderReplacement_500_Placeholders | 2,130.44 μs | 32.951 μs | 21.795 μs | 27.38 |    0.48 | 414.0625 | 164.0625 | 2558.75 KB |       22.37 |

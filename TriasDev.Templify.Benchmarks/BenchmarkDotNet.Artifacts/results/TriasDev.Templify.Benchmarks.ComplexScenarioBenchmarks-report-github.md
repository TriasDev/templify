```

BenchmarkDotNet v0.15.6, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 9.0.0 (9.0.0, 9.0.24.52809), Arm64 RyuJIT armv8.0-a
  Job-YFEFPZ : .NET 9.0.0 (9.0.0, 9.0.24.52809), Arm64 RyuJIT armv8.0-a

IterationCount=10  WarmupCount=3  

```
| Method                            | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|---------------------------------- |-----------:|---------:|---------:|------:|--------:|---------:|---------:|-----------:|------------:|
| ComplexScenario_Small_5Sections   |   279.8 μs | 11.84 μs |  6.19 μs |  1.00 |    0.03 |  70.3125 |   9.7656 |     433 KB |        1.00 |
| ComplexScenario_Medium_10Sections |   656.3 μs | 11.61 μs |  6.91 μs |  2.35 |    0.05 | 175.7813 |  42.9688 | 1085.92 KB |        2.51 |
| ComplexScenario_Large_20Sections  | 1,834.8 μs | 35.14 μs | 23.24 μs |  6.56 |    0.16 | 460.9375 | 152.3438 | 2842.68 KB |        6.57 |

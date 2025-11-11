```

BenchmarkDotNet v0.15.6, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 9.0.0 (9.0.0, 9.0.24.52809), Arm64 RyuJIT armv8.0-a
  Job-YFEFPZ : .NET 9.0.0 (9.0.0, 9.0.24.52809), Arm64 RyuJIT armv8.0-a

IterationCount=10  WarmupCount=3  

```
| Method                   | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------- |-----------:|---------:|---------:|------:|--------:|---------:|--------:|----------:|------------:|
| Conditional_10_AllTrue   |   142.5 μs |  5.35 μs |  2.80 μs |  1.00 |    0.03 |  26.3672 |  2.9297 | 161.78 KB |        1.00 |
| Conditional_10_AllFalse  |   141.9 μs |  4.55 μs |  2.38 μs |  1.00 |    0.02 |  26.3672 |  2.9297 | 161.78 KB |        1.00 |
| Conditional_50_AllTrue   |   552.2 μs |  9.53 μs |  6.30 μs |  3.88 |    0.08 |  83.9844 | 23.4375 | 515.28 KB |        3.18 |
| Conditional_50_AllFalse  |   564.4 μs |  9.52 μs |  4.98 μs |  3.96 |    0.08 |  82.0313 | 19.5313 | 515.31 KB |        3.19 |
| Conditional_100_AllTrue  | 1,100.9 μs | 14.60 μs |  9.66 μs |  7.73 |    0.16 | 152.3438 | 50.7813 | 950.81 KB |        5.88 |
| Conditional_100_AllFalse | 1,113.7 μs | 62.33 μs | 41.23 μs |  7.82 |    0.31 | 148.4375 | 46.8750 | 950.81 KB |        5.88 |

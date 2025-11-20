```

BenchmarkDotNet v0.15.6, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 9.0.0 (9.0.0, 9.0.24.52809), Arm64 RyuJIT armv8.0-a
  Job-YFEFPZ : .NET 9.0.0 (9.0.0, 9.0.24.52809), Arm64 RyuJIT armv8.0-a

IterationCount=10  WarmupCount=3  

```
| Method                         | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------------- |----------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|
| Loop_Small_10Items             |  92.78 μs |  2.178 μs |  1.441 μs |  1.00 |    0.02 |  24.4141 |  2.4414 | 150.87 KB |        1.00 |
| Loop_Medium_5Loops_20ItemsEach | 521.13 μs | 14.433 μs |  7.549 μs |  5.62 |    0.11 | 136.7188 | 31.2500 |    849 KB |        5.63 |
| Loop_Large_100Items            | 495.44 μs |  4.973 μs |  3.289 μs |  5.34 |    0.09 | 132.8125 | 29.2969 | 823.77 KB |        5.46 |
| Loop_Nested_10x5               | 306.71 μs | 36.436 μs | 21.683 μs |  3.31 |    0.23 |  76.1719 | 13.6719 | 476.46 KB |        3.16 |

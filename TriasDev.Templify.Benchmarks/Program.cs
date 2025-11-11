using BenchmarkDotNet.Running;
using System.Reflection;

// Run benchmarks using BenchmarkSwitcher for command-line support
Console.WriteLine("TriasDev.Templify Performance Benchmarks");
Console.WriteLine("=========================================");
Console.WriteLine();

// Use BenchmarkSwitcher to allow filtering via command-line args
BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);

Console.WriteLine();
Console.WriteLine("Benchmarking complete! Results saved to BenchmarkDotNet.Artifacts/results/");

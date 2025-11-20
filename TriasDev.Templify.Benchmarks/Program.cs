// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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

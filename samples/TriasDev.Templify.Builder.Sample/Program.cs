// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Builds the sample order confirmation in German and English, as DOCX and ODT, verifies it, fills it with the sample
// data and writes everything to the folder given as the first argument (default: ./output).
using TriasDev.Templify.Builder.Sample;

// "phase3 <dir>": the JSON round trip (export, import, build, fill) instead.
bool phase3 = args.Length > 0 && args[0] == "phase3";
string output = phase3 ? (args.Length > 1 ? args[1] : "output-phase3") : args.Length > 0 ? args[0] : "output";
IReadOnlyList<string> files = phase3 ? SampleRunner.RunPhase3(output, Console.Out) : SampleRunner.Run(output, Console.Out);
Console.WriteLine($"{files.Count} files written to {Path.GetFullPath(output)}");

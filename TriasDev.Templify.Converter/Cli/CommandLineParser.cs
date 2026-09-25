// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Converter.Cli;

/// <summary>
/// A successfully parsed command line.
/// </summary>
public sealed class ParsedCommandLine
{
    /// <summary>The command name (analyze, convert, validate, clean, help).</summary>
    public required string Command { get; init; }

    /// <summary>The input document (null only for help).</summary>
    public string? InputPath { get; init; }

    /// <summary>The value of <c>--output</c>/<c>-o</c>, if given.</summary>
    public string? OutputPath { get; init; }

    /// <summary>True if <c>--verbose</c>/<c>-v</c> was given.</summary>
    public bool Verbose { get; init; }

    /// <summary>True if <c>--unwrap-all-controls</c> was given (convert only).</summary>
    public bool UnwrapAllControls { get; init; }
}

/// <summary>
/// The outcome of parsing: either a <see cref="ParsedCommandLine"/> or an error message.
/// </summary>
public sealed class CommandLineParseResult
{
    /// <summary>The parsed command line, or null on error.</summary>
    public ParsedCommandLine? CommandLine { get; init; }

    /// <summary>The error message, or null on success.</summary>
    public string? Error { get; init; }

    /// <summary>True if parsing succeeded.</summary>
    public bool IsSuccess => CommandLine != null;
}

/// <summary>
/// Small, strict command-line parser for the converter CLI.
/// </summary>
/// <remarks>
/// Rules: the first argument is the command; options may appear anywhere after it (also before the
/// input path); <c>--output value</c> and <c>--output=value</c> are both accepted; unknown options,
/// a missing option value, repeated options and extra positional arguments are errors; <c>--</c> ends
/// option parsing.
/// </remarks>
public static class CommandLineParser
{
    private static readonly string[] _helpNames = { "help", "--help", "-h", "-?" };

    private static readonly Dictionary<string, CommandSpec> _commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["analyze"] = new CommandSpec(AllowsOutput: true, AllowsUnwrapAll: false),
        ["convert"] = new CommandSpec(AllowsOutput: true, AllowsUnwrapAll: true),
        ["validate"] = new CommandSpec(AllowsOutput: false, AllowsUnwrapAll: false),
        ["clean"] = new CommandSpec(AllowsOutput: true, AllowsUnwrapAll: false),
    };

    /// <summary>The names of all supported commands.</summary>
    public static IReadOnlyCollection<string> Commands => _commands.Keys;

    /// <summary>
    /// Parse the command-line arguments.
    /// </summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return Fail("No command given.");
        }

        string command = args[0];
        if (_helpNames.Contains(command, StringComparer.OrdinalIgnoreCase))
        {
            return Success(new ParsedCommandLine { Command = "help" });
        }

        if (!_commands.TryGetValue(command, out CommandSpec? spec))
        {
            return Fail($"Unknown command '{command}'.");
        }

        command = command.ToLowerInvariant();
        string? input = null;
        string? output = null;
        bool verbose = false;
        bool unwrapAll = false;
        bool optionsEnded = false;

        for (int i = 1; i < args.Count; i++)
        {
            string arg = args[i];

            if (!optionsEnded && arg == "--")
            {
                optionsEnded = true;
                continue;
            }

            if (!optionsEnded && arg.Length > 1 && arg[0] == '-')
            {
                string name = arg;
                string? inlineValue = null;
                int equals = arg.IndexOf('=');
                if (arg.StartsWith("--", StringComparison.Ordinal) && equals > 0)
                {
                    name = arg.Substring(0, equals);
                    inlineValue = arg.Substring(equals + 1);
                }

                switch (name)
                {
                    case "--help":
                    case "-h":
                    case "-?":
                        return Success(new ParsedCommandLine { Command = "help" });

                    case "--output":
                    case "-o":
                        if (!spec.AllowsOutput)
                        {
                            return Fail($"Option '{name}' is not supported by '{command}'.");
                        }

                        if (output != null)
                        {
                            return Fail($"Option '{name}' was given more than once.");
                        }

                        if (inlineValue != null)
                        {
                            output = inlineValue;
                        }
                        else if (i + 1 < args.Count && !IsOptionLike(args[i + 1]))
                        {
                            output = args[++i];
                        }
                        else
                        {
                            return Fail($"Option '{name}' requires a path.");
                        }

                        if (output.Length == 0)
                        {
                            return Fail($"Option '{name}' requires a path.");
                        }

                        continue;

                    case "--verbose":
                    case "-v":
                        if (inlineValue != null)
                        {
                            return Fail($"Option '{name}' does not take a value.");
                        }

                        verbose = true;
                        continue;

                    case "--unwrap-all-controls":
                        if (!spec.AllowsUnwrapAll)
                        {
                            return Fail($"Option '{name}' is not supported by '{command}'.");
                        }

                        if (inlineValue != null)
                        {
                            return Fail($"Option '{name}' does not take a value.");
                        }

                        unwrapAll = true;
                        continue;

                    default:
                        return Fail($"Unknown option '{name}' for '{command}'.");
                }
            }

            if (input != null)
            {
                return Fail($"Unexpected argument '{arg}' (input already given: '{input}').");
            }

            input = arg;
        }

        if (input == null)
        {
            return Fail($"'{command}' requires a document path.");
        }

        return Success(new ParsedCommandLine
        {
            Command = command,
            InputPath = input,
            OutputPath = output,
            Verbose = verbose,
            UnwrapAllControls = unwrapAll,
        });
    }

    private static bool IsOptionLike(string arg)
    {
        return arg.Length > 1 && arg[0] == '-';
    }

    private static CommandLineParseResult Success(ParsedCommandLine commandLine) => new() { CommandLine = commandLine };

    private static CommandLineParseResult Fail(string error) => new() { Error = error };

    private sealed record CommandSpec(bool AllowsOutput, bool AllowsUnwrapAll);
}

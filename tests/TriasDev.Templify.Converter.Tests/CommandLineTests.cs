// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using TriasDev.Templify.Converter.Cli;
using static TriasDev.Templify.Converter.Tests.TestDocuments;

namespace TriasDev.Templify.Converter.Tests;

public class CommandLineTests
{
    [Theory]
    [InlineData(new[] { "convert", "in.docx" }, "in.docx", null)]
    [InlineData(new[] { "convert", "in.docx", "--output", "out.docx" }, "in.docx", "out.docx")]
    [InlineData(new[] { "convert", "--output", "out.docx", "in.docx" }, "in.docx", "out.docx")]
    [InlineData(new[] { "convert", "-o", "out.docx", "in.docx" }, "in.docx", "out.docx")]
    [InlineData(new[] { "convert", "in.docx", "--output=out.docx" }, "in.docx", "out.docx")]
    [InlineData(new[] { "CONVERT", "in.docx" }, "in.docx", null)]
    [InlineData(new[] { "clean", "--", "-weird.docx" }, "-weird.docx", null)]
    [InlineData(new[] { "analyze", "t.docx", "-o", "r.md" }, "t.docx", "r.md")]
    [InlineData(new[] { "validate", "t.docx" }, "t.docx", null)]
    public void Parse_ValidArguments(string[] args, string input, string? output)
    {
        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(input, result.CommandLine!.InputPath);
        Assert.Equal(output, result.CommandLine.OutputPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("frobnicate x.docx")]
    [InlineData("convert")]
    [InlineData("convert in.docx --output")]
    [InlineData("convert in.docx --output --verbose")]
    [InlineData("convert in.docx --output=")]
    [InlineData("convert in.docx --outptu x.docx")]
    [InlineData("convert in.docx extra.docx")]
    [InlineData("convert in.docx -o a.docx -o b.docx")]
    [InlineData("validate in.docx --output x.docx")]
    [InlineData("clean in.docx --unwrap-all-controls")]
    public void Parse_InvalidArguments_ReturnsError(string commandLine)
    {
        string[] args = commandLine.Length == 0 ? Array.Empty<string>() : commandLine.Split(' ');
        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_Help(string arg)
    {
        Assert.Equal("help", CommandLineParser.Parse(new[] { arg }).CommandLine!.Command);
    }

    [Fact]
    public void Parse_Flags()
    {
        ParsedCommandLine commandLine = CommandLineParser.Parse(new[] { "convert", "-v", "in.docx", "--unwrap-all-controls" }).CommandLine!;

        Assert.True(commandLine.Verbose);
        Assert.True(commandLine.UnwrapAllControls);
    }

    [Fact]
    public void Run_InvalidArguments_ExitCode2_ErrorOnStderr()
    {
        StringWriter stdout = new();
        StringWriter stderr = new();

        int exit = Program.Run(new[] { "convert", "in.docx", "--bogus" }, stdout, stderr);

        Assert.Equal(Program.ExitUsage, exit);
        Assert.Contains("--bogus", stderr.ToString());
    }

    [Fact]
    public void Run_MissingFile_ExitCode1_NoStackTraceUnlessVerbose()
    {
        using TempDirectory dir = new();
        string missing = dir.File("missing.docx");

        StringWriter stderr = new();
        int exit = Program.Run(new[] { "convert", missing }, new StringWriter(), stderr);
        Assert.Equal(Program.ExitFailure, exit);
        Assert.Contains("not found", stderr.ToString());
        Assert.DoesNotContain("   at ", stderr.ToString());

        StringWriter verboseErr = new();
        Program.Run(new[] { "convert", missing, "--verbose" }, new StringWriter(), verboseErr);
        Assert.Contains("   at ", verboseErr.ToString());
    }

    [Fact]
    public void Run_Convert_ExitCodeReflectsFailedControls()
    {
        using TempDirectory dir = new();
        string good = dir.File("good.docx");
        string bad = dir.File("bad.docx");
        Create(good, new OpenXmlElement[] { Para(InlineVariable("name")) });
        Create(bad, new OpenXmlElement[] { BlockControl("conditionalRemove_a_or", Para("x")) });

        StringWriter badErr = new();
        Assert.Equal(Program.ExitSuccess, Program.Run(new[] { "convert", "--output", dir.File("good-out.docx"), good }, new StringWriter(), new StringWriter()));
        Assert.Equal(Program.ExitFailure, Program.Run(new[] { "convert", bad }, new StringWriter(), badErr));
        Assert.Contains("conditionalRemove_a_or", badErr.ToString());
        Assert.True(File.Exists(dir.File("good-out.docx")));
        Assert.True(File.Exists(dir.File("bad-templify.docx")));
    }

    [Fact]
    public void Run_CleanAndValidate()
    {
        using TempDirectory dir = new();
        string path = dir.File("doc.docx");
        Create(path, new OpenXmlElement[] { BlockControl("x", Para("content")) });

        Assert.Equal(Program.ExitSuccess, Program.Run(new[] { "clean", path }, new StringWriter(), new StringWriter()));
        Assert.Equal(Program.ExitSuccess, Program.Run(new[] { "validate", path }, new StringWriter(), new StringWriter()));

        string notDocx = dir.File("garbage.docx");
        File.WriteAllText(notDocx, "not a zip");
        StringWriter stderr = new();
        Assert.Equal(Program.ExitFailure, Program.Run(new[] { "validate", notDocx }, new StringWriter(), stderr));
        Assert.Contains("Failed to open", stderr.ToString());
    }
}

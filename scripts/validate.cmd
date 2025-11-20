@echo off
REM Templify Converter - Validate Command
REM Validates that a Word document is well-formed

dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- validate %*

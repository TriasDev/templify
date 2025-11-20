@echo off
REM Templify Converter - Analyze Command
REM Analyzes an OpenXMLTemplates document and generates a detailed report

dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- analyze %*

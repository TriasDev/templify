@echo off
REM Templify Converter - Convert Command
REM Converts an OpenXMLTemplates document to Templify format

dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- convert %*

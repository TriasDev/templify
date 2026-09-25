@echo off
REM Templify Converter - Analyze Command
REM Analyzes an OpenXMLTemplates document and generates a detailed report
REM
REM Works from any directory: the converter project is located relative to this script (%~dp0),
REM while file arguments stay relative to your current directory.

dotnet run --project "%~dp0..\TriasDev.Templify.Converter\TriasDev.Templify.Converter.csproj" -- analyze %*
exit /b %ERRORLEVEL%

@echo off
REM Templify Converter - Convert Command
REM Converts an OpenXMLTemplates document to Templify format
REM
REM Works from any directory: the converter project is located relative to this script (%~dp0),
REM while file arguments stay relative to your current directory.

dotnet run --project "%~dp0..\TriasDev.Templify.Converter\TriasDev.Templify.Converter.csproj" -- convert %*
exit /b %ERRORLEVEL%

@echo off
REM Templify Converter - Validate Command
REM Validates that a Word document is well-formed
REM
REM Works from any directory: the converter project is located relative to this script (%~dp0),
REM while file arguments stay relative to your current directory.

dotnet run --project "%~dp0..\TriasDev.Templify.Converter\TriasDev.Templify.Converter.csproj" -- validate %*
exit /b %ERRORLEVEL%

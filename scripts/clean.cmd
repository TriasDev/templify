@echo off
REM Templify Converter - Clean Command
REM Removes all Structured Document Tag (SDT) elements from a document
REM
REM Works from any directory: the converter project is located relative to this script (%~dp0),
REM while file arguments stay relative to your current directory.

dotnet run --project "%~dp0..\TriasDev.Templify.Converter\TriasDev.Templify.Converter.csproj" -- clean %*
exit /b %ERRORLEVEL%

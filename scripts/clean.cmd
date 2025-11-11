@echo off
REM Templify Converter - Clean Command
REM Removes all Structured Document Tag (SDT) elements from a document

dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- clean %*

#!/bin/bash
# Templify Converter - Clean Command
# Removes all Structured Document Tag (SDT) elements from a document

dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- clean "$@"

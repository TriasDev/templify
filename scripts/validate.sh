#!/bin/bash
# Templify Converter - Validate Command
# Validates that a Word document is well-formed

dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- validate "$@"

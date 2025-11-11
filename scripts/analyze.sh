#!/bin/bash
# Templify Converter - Analyze Command
# Analyzes an OpenXMLTemplates document and generates a detailed report

dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- analyze "$@"

#!/usr/bin/env bash
# Templify Converter - Analyze Command
# Analyzes an OpenXMLTemplates document and generates a detailed report
#
# Works from any directory: the converter project is located relative to this script,
# while file arguments stay relative to your current directory.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run --project "$SCRIPT_DIR/../TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj" -- analyze "$@"

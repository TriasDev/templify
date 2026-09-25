#!/usr/bin/env bash
# Templify Converter - Clean Command
# Removes all Structured Document Tag (SDT) elements from a document
#
# Works from any directory: the converter project is located relative to this script,
# while file arguments stay relative to your current directory.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run --project "$SCRIPT_DIR/../TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj" -- clean "$@"

#!/bin/bash
# Templify Converter - Convert Command
# Converts an OpenXMLTemplates document to Templify format

dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- convert "$@"

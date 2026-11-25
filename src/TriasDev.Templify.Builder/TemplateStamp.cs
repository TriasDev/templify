// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Where a template came from: written into every generated template's document properties (DOCX custom
/// properties, ODT <c>meta:user-defined</c>) and read back by <see cref="TemplateInspector"/>.
/// </summary>
/// <param name="TemplateId">The definition's <see cref="TemplateIdentity.Id"/>.</param>
/// <param name="Version">The definition's <see cref="TemplateIdentity.Version"/>.</param>
/// <param name="ContractHash">The <see cref="DataContract.Hash"/> of the build.</param>
/// <param name="Language">The document language, for example <c>de-DE</c>.</param>
/// <param name="BuilderVersion">The version of TriasDev.Templify.Builder that built the template.</param>
/// <param name="MinimumEngineVersion">The version of TriasDev.Templify the builder was built against; fill with this or later.</param>
/// <remarks>
/// The stamp tells where a copy came from; it does not decide whether the copy is usable. Other editors may drop
/// custom properties, so a missing stamp means "unknown origin". Filled documents keep the stamp;
/// <see cref="TemplateStampRemover"/> removes it from documents that go to external recipients.
/// </remarks>
public sealed record TemplateStamp(
    string TemplateId,
    int Version,
    string ContractHash,
    string Language,
    string BuilderVersion,
    string MinimumEngineVersion)
{
    /// <summary>The prefix of the builder's document property names.</summary>
    internal const string PropertyPrefix = "Templify.";

    private const string TemplateIdName = PropertyPrefix + "TemplateId";
    private const string VersionName = PropertyPrefix + "TemplateVersion";
    private const string ContractHashName = PropertyPrefix + "ContractHash";
    private const string LanguageName = PropertyPrefix + "Language";
    private const string BuilderVersionName = PropertyPrefix + "BuilderVersion";
    private const string EngineVersionName = PropertyPrefix + "MinimumEngineVersion";

    /// <summary>The stamp of a build.</summary>
    internal static TemplateStamp For(TemplateIdentity identity, DataContract contract, CultureInfo language) =>
        new TemplateStamp(
            identity.Id,
            identity.Version,
            contract.Hash,
            language.Name,
            VersionOf(typeof(TemplateBuilder).Assembly),
            VersionOf(typeof(TemplateProcessor).Assembly));

    /// <summary>The stamp as document properties (name, value), in a fixed order.</summary>
    internal IEnumerable<(string Name, string Value)> ToProperties()
    {
        yield return (TemplateIdName, TemplateId);
        yield return (VersionName, Version.ToString(CultureInfo.InvariantCulture));
        yield return (ContractHashName, ContractHash);
        yield return (LanguageName, Language);
        yield return (BuilderVersionName, BuilderVersion);
        yield return (EngineVersionName, MinimumEngineVersion);
    }

    /// <summary>Reads a stamp from document properties; null unless every stamp property is present and valid.</summary>
    internal static TemplateStamp? FromProperties(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue(TemplateIdName, out string? id)
            && properties.TryGetValue(VersionName, out string? versionText)
            && int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version)
            && properties.TryGetValue(ContractHashName, out string? hash)
            && properties.TryGetValue(LanguageName, out string? language)
            && properties.TryGetValue(BuilderVersionName, out string? builderVersion)
            && properties.TryGetValue(EngineVersionName, out string? engineVersion))
        {
            return new TemplateStamp(id, version, hash, language, builderVersion, engineVersion);
        }

        return null;
    }

    /// <summary>The informational version without build metadata (<c>0.1.0+abc</c> → <c>0.1.0</c>).</summary>
    private static string VersionOf(Assembly assembly)
    {
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();
        if (version == null)
        {
            return "0.0.0";
        }

        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}

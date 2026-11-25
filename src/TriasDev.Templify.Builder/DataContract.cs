// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>How a template uses a data path.</summary>
public enum ContractUsage
{
    /// <summary>The value is printed: <c>{{path}}</c>.</summary>
    Value = 0,

    /// <summary>The path is a collection a loop runs over: <c>{{#foreach x in path}}</c>.</summary>
    Collection = 1,

    /// <summary>The value is tested in a condition: <c>{{#if path …}}</c>.</summary>
    Condition = 2,
}

/// <summary>One data path a template uses, in canonical form.</summary>
/// <param name="Path">
/// The canonical path: from the data's root, with <c>[]</c> for "every item of a collection", for example
/// <c>order.lines[].description</c>. Loop variable names do not appear in it.
/// </param>
/// <param name="Usage">How the template uses the path.</param>
public sealed record ContractEntry(string Path, ContractUsage Usage);

/// <summary>
/// The data a template needs: every path it prints, tests or loops over, in canonical form. Built with the template
/// (<see cref="GeneratedTemplate.Contract"/>).
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Hash"/> depends only on the entries, not on the language, the format or the theme, so it is the
/// same for every build of a definition that reads the same data, which makes it a cheap parity check between
/// languages.
/// </para>
/// <para>
/// <see cref="CreateWitness"/> builds data in which every path exists. Validating an uploaded copy of a template
/// against it (<c>TemplateProcessor.ValidateTemplate(stream, witness)</c>) reports the paths the copy uses that
/// the current data no longer provides.
/// </para>
/// </remarks>
public sealed class DataContract
{
    private const int JsonVersion = 1;

    internal DataContract(IEnumerable<ContractEntry> entries)
    {
        Entries = entries
            .Distinct()
            .OrderBy(e => e.Path, StringComparer.Ordinal)
            .ThenBy(e => e.Usage)
            .ToList();
        Hash = ComputeHash(Entries);
    }

    /// <summary>Gets the entries, sorted by path (ordinal) and usage.</summary>
    public IReadOnlyList<ContractEntry> Entries { get; }

    /// <summary>Gets the SHA-256 of the entries as lower-case hex.</summary>
    public string Hash { get; }

    /// <summary>Reads a contract written by <see cref="ToJson"/>.</summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The contract.</returns>
    /// <exception cref="FormatException">The JSON is not a data contract.</exception>
    public static DataContract FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            JsonNode? root = JsonNode.Parse(json);
            if (root?["version"]?.GetValue<int>() != JsonVersion || root["entries"] is not JsonArray entries)
            {
                throw new FormatException($"Not a data contract of version {JsonVersion}.");
            }

            List<ContractEntry> result = new List<ContractEntry>();
            foreach (JsonNode? entry in entries)
            {
                string path = entry?["path"]?.GetValue<string>() ?? throw new FormatException("An entry has no path.");
                string usage = entry["usage"]?.GetValue<string>() ?? throw new FormatException("An entry has no usage.");
                if (path.Length == 0 || !Enum.TryParse(usage, ignoreCase: true, out ContractUsage parsed) || !Enum.IsDefined(parsed))
                {
                    throw new FormatException($"Invalid entry: '{path}' ({usage}).");
                }

                result.Add(new ContractEntry(path, parsed));
            }

            return new DataContract(result);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new FormatException("Not a data contract: " + exception.Message, exception);
        }
    }

    /// <summary>Writes the contract as JSON: a version, the hash and the entries.</summary>
    /// <returns>The JSON, indented.</returns>
    public string ToJson()
    {
        JsonObject root = new JsonObject
        {
            ["version"] = JsonVersion,
            ["hash"] = Hash,
            ["entries"] = new JsonArray(Entries
                .Select(e => (JsonNode)new JsonObject { ["path"] = e.Path, ["usage"] = e.Usage.ToString().ToLowerInvariant() })
                .ToArray()),
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Builds data in which every path of the contract exists: each collection holds one item, each value is the text
    /// <c>"x"</c>.
    /// </summary>
    /// <returns>The data, as nested dictionaries and lists.</returns>
    public IReadOnlyDictionary<string, object?> CreateWitness()
    {
        WitnessNode root = new WitnessNode();
        foreach (ContractEntry entry in Entries)
        {
            WitnessNode node = root;
            foreach (string segment in entry.Path.Split('.'))
            {
                bool isCollection = segment.EndsWith("[]", StringComparison.Ordinal);
                string name = isCollection ? segment[..^2] : segment;
                node = node.Child(name);
                if (isCollection)
                {
                    node = node.Item();
                }
            }

            if (entry.Usage == ContractUsage.Collection)
            {
                node.Item();
            }
        }

        return root.ToData() as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static string ComputeHash(IEnumerable<ContractEntry> entries)
    {
        StringBuilder text = new StringBuilder();
        foreach (ContractEntry entry in entries)
        {
            text.Append(entry.Usage.ToString().ToLowerInvariant()).Append(' ').Append(entry.Path).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>
    /// Collects the contract of a composed body: values, conditions and loop collections, with loop variables
    /// resolved to canonical paths.
    /// </summary>
    internal static DataContract Collect(IEnumerable<Block> body)
    {
        List<ContractEntry> entries = new List<ContractEntry>();
        Walk(body, entries);
        return new DataContract(entries);
    }

    private static void Walk(IEnumerable<Block> blocks, List<ContractEntry> entries)
    {
        foreach (Block block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    foreach (ValueInline value in paragraph.Inlines.OfType<ValueInline>())
                    {
                        Add(entries, value.Path, ContractUsage.Value);
                    }

                    break;

                case ConditionalBlock conditional:
                    foreach (ConditionalBranch branch in conditional.Branches)
                    {
                        foreach (TemplatePath path in branch.Condition.Paths)
                        {
                            Add(entries, path, ContractUsage.Condition);
                        }

                        Walk(branch.Body, entries);
                    }

                    if (conditional.Else is { } otherwise)
                    {
                        Walk(otherwise, entries);
                    }

                    break;

                case LoopBlock loop:
                    Add(entries, loop.Collection, ContractUsage.Collection);
                    Walk(loop.Body, entries);
                    break;

                case ComponentBlock component:
                    Walk(component.Body, entries);
                    break;

                case TableBlock table:
                    foreach (TableCellModel cell in table.AllRows().SelectMany(r => r.Cells))
                    {
                        Walk(cell.Content, entries);
                    }

                    break;
            }
        }
    }

    private static void Add(List<ContractEntry> entries, TemplatePath path, ContractUsage usage)
    {
        if (!path.IsRoot && !path.IsMetadata)
        {
            entries.Add(new ContractEntry(Canonical(path), usage));
        }
    }

    /// <summary><c>line.description</c> in a loop over <c>order.lines</c> → <c>order.lines[].description</c>.</summary>
    internal static string Canonical(TemplatePath path)
    {
        if (path.Scope is not { } scope)
        {
            return path.ToString();
        }

        string text = path.ToString();
        string rest = text.Length > scope.ItemName.Length ? text[scope.ItemName.Length..] : string.Empty;
        return Canonical(scope.Collection) + "[]" + rest;
    }

    /// <summary>A node of the witness tree: a dictionary, a one-item list, or a leaf.</summary>
    private sealed class WitnessNode
    {
        private readonly Dictionary<string, WitnessNode> _children = new Dictionary<string, WitnessNode>(StringComparer.Ordinal);
        private WitnessNode? _item;

        public WitnessNode Child(string name)
        {
            if (!_children.TryGetValue(name, out WitnessNode? child))
            {
                child = new WitnessNode();
                _children[name] = child;
            }

            return child;
        }

        public WitnessNode Item() => _item ??= new WitnessNode();

        public object? ToData()
        {
            if (_item != null)
            {
                return new List<object?> { _item.ToData() };
            }

            if (_children.Count > 0)
            {
                return _children.ToDictionary(c => c.Key, c => c.Value.ToData(), StringComparer.Ordinal);
            }

            return "x";
        }
    }
}

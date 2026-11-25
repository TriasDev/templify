// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Builder;

/// <summary>
/// The current item of a <see cref="BlockComposer.ForEach"/> loop. It exists only inside the loop's lambda; paths
/// from it start with the loop's iteration variable (<c>{{line.amount}}</c>) and are checked to be used inside that loop.
/// </summary>
public sealed class LoopItem
{
    private readonly LoopScope _scope;

    internal LoopItem(LoopScope scope)
    {
        _scope = scope;
    }

    /// <summary>Gets the item itself: <c>{{line}}</c>.</summary>
    public TemplatePath Self => TemplatePath.ForItem(_scope);

    /// <summary>Gets a path from the item: <c>item["amount"]</c> is <c>{{line.amount}}</c>.</summary>
    /// <param name="relative">Dot-separated segments.</param>
    public TemplatePath this[string relative] => Self[relative];

    /// <summary>Gets the 0-based position of the item: <c>{{@index}}</c>. Only in this loop's own body, not in nested loops.</summary>
    public TemplatePath Index => TemplatePath.ForMetadata(_scope, "index");

    /// <summary>Gets the 1-based position of the item: <c>{{@number}}</c>. Only in this loop's own body.</summary>
    public TemplatePath Number => TemplatePath.ForMetadata(_scope, "number");

    /// <summary>Gets the number of items: <c>{{@count}}</c>. Only in this loop's own body.</summary>
    public TemplatePath Count => TemplatePath.ForMetadata(_scope, "count");

    /// <summary>Gets a condition that holds for the first item: <c>@first</c>. Only in this loop's own body.</summary>
    public Condition IsFirst => TemplatePath.ForMetadata(_scope, "first");

    /// <summary>Gets a condition that holds for the last item: <c>@last</c>. Only in this loop's own body.</summary>
    public Condition IsLast => TemplatePath.ForMetadata(_scope, "last");
}

/// <summary>
/// A reusable block of template content, the counterpart of QuestPDF's <c>IComponent</c>. Use it for content shared
/// by several templates or built through dependency injection; a plain extension method on
/// <see cref="BlockComposer"/> is enough for simple blocks.
/// </summary>
/// <remarks>
/// The template keeps a component as a unit, not only the content it expands to, so that later versions can export
/// a definition with its components (see the builder's roadmap).
/// </remarks>
public interface ITemplateComponent
{
    /// <summary>Describes the component's content.</summary>
    /// <param name="content">The composer to describe the content with; valid only during this call.</param>
    void Compose(BlockComposer content);
}

/// <summary>One loop while the template is composed: its iteration variable and the loop around it.</summary>
internal sealed partial class LoopScope(string itemName, TemplatePath collection, LoopScope? parent)
{
    public string ItemName { get; } = itemName;

    public TemplatePath Collection { get; } = collection;

    public LoopScope? Parent { get; } = parent;

    /// <summary>This scope and the scopes around it, innermost first.</summary>
    public static IEnumerable<LoopScope> Chain(LoopScope? scope)
    {
        for (LoopScope? current = scope; current != null; current = current.Parent)
        {
            yield return current;
        }
    }

    /// <summary>Checks an iteration variable name where it is written.</summary>
    public static string ValidateItemName(string itemName, LoopScope? enclosing)
    {
        ArgumentNullException.ThrowIfNull(itemName);
        if (!NamePattern().IsMatch(itemName))
        {
            throw new ArgumentException(
                $"'{itemName}' is not a valid loop variable name: use letters, digits and '_', starting with a letter or '_'.",
                nameof(itemName));
        }

        if (_reserved.Contains(itemName))
        {
            throw new ArgumentException($"'{itemName}' cannot name a loop variable: it is a Templify keyword.", nameof(itemName));
        }

        if (Chain(enclosing).Any(s => string.Equals(s.ItemName, itemName, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"The loop variable '{itemName}' is already used by an enclosing loop; choose another name so both stay reachable.",
                nameof(itemName));
        }

        return itemName;
    }

    /// <summary>
    /// Reports a path used where it cannot work: an iteration variable outside its loop, metadata of an outer loop, or
    /// a path from the data's root whose first segment a loop variable hides.
    /// </summary>
    public static void CheckUse(BuildContext context, LoopScope? current, TemplatePath path)
    {
        if (path.IsRoot)
        {
            return;
        }

        if (path.Scope is { } owner && !Chain(current).Contains(owner))
        {
            context.Error(
                DiagnosticCodes.LoopVariableOutOfScope,
                $"'{path}' uses the loop variable '{owner.ItemName}' outside its loop (foreach {owner.ItemName} in {owner.Collection}).");
            return;
        }

        if (path.IsMetadata && path.Scope != current)
        {
            context.Error(
                DiagnosticCodes.OuterLoopMetadata,
                $"'{path}' of the loop over '{path.Scope!.Collection}' is used inside a nested loop; Templify's loop metadata always refers to the innermost loop.");
            return;
        }

        if (path.Scope is null)
        {
            string first = path.ToString().Split('.')[0];
            LoopScope? hiding = Chain(current).FirstOrDefault(s => string.Equals(s.ItemName, first, StringComparison.Ordinal));
            if (hiding != null)
            {
                context.Error(
                    DiagnosticCodes.RootPathHidden,
                    $"'{path}' starts at the data's root, but inside the loop over '{hiding.Collection}' the name '{first}' is the loop variable. Rename the loop variable.");
            }
        }
    }

    private static readonly HashSet<string> _reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "in", "this", "and", "or", "not", "is", "empty", "exists", "contains", "startswith", "endswith", "true", "false", "null",
    };

    [GeneratedRegex(@"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}

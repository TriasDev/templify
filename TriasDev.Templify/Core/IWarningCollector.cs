// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// Interface for collecting warnings during template processing.
/// </summary>
internal interface IWarningCollector
{
    /// <summary>
    /// Adds a warning to the collection.
    /// </summary>
    /// <param name="warning">The warning to add.</param>
    void AddWarning(ProcessingWarning warning);

    /// <summary>
    /// Gets all collected warnings.
    /// </summary>
    /// <returns>A read-only list of warnings.</returns>
    IReadOnlyList<ProcessingWarning> GetWarnings();
}

/// <summary>
/// Default implementation of <see cref="IWarningCollector"/> that collects warnings in a list.
/// </summary>
internal sealed class WarningCollector : IWarningCollector
{
    private readonly List<ProcessingWarning> _warnings = new();

    /// <inheritdoc/>
    public void AddWarning(ProcessingWarning warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        _warnings.Add(warning);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ProcessingWarning> GetWarnings()
    {
        return _warnings.AsReadOnly();
    }
}

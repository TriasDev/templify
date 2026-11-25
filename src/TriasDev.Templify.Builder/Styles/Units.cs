// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// Extension methods that write lengths the way a design reads: <c>12.Pt()</c>, <c>10.5.Pt()</c>, <c>25.Mm()</c>.
/// </summary>
public static class Units
{
    /// <summary>A length in points.</summary>
    /// <param name="points">The length in points.</param>
    /// <returns>The length.</returns>
    public static Length Pt(this int points) => Length.FromPoints(points);

    /// <summary>A length in points.</summary>
    /// <param name="points">The length in points.</param>
    /// <returns>The length.</returns>
    public static Length Pt(this double points) => Length.FromPoints(points);

    /// <summary>A length in millimeters.</summary>
    /// <param name="millimeters">The length in millimeters.</param>
    /// <returns>The length.</returns>
    public static Length Mm(this int millimeters) => Length.FromMillimeters(millimeters);

    /// <summary>A length in millimeters.</summary>
    /// <param name="millimeters">The length in millimeters.</param>
    /// <returns>The length.</returns>
    public static Length Mm(this double millimeters) => Length.FromMillimeters(millimeters);
}

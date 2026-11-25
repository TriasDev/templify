// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder.Sample;

/// <summary>The data of an order confirmation, as C# types: the shape of <c>sample-data.json</c>.</summary>
public sealed record OrderData(Order Order);

/// <summary>An order.</summary>
public sealed record Order(
    string Number,
    DateTime Date,
    string Status,
    bool IsExpress,
    decimal Total,
    decimal Discount,
    string? Notes,
    Customer Customer,
    IReadOnlyList<OrderLine> Lines,
    IReadOnlyList<Check> Checks);

/// <summary>The ordering customer.</summary>
public sealed record Customer(string Name, string? Contact, bool IsNew, Address Address);

/// <summary>A postal address.</summary>
public sealed record Address(string Street, string PostalCode, string City, string Country);

/// <summary>An order line.</summary>
public sealed record OrderLine(string Description, int Quantity, decimal Amount, bool InStock, IReadOnlyList<string> Tags);

/// <summary>A check run on the order.</summary>
public sealed record Check(string Title, bool Passed, string? Note);

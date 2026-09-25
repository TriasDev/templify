// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Demo;

// Data classes
public class Customer
{
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Address? Address { get; set; }
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class LineItem
{
    public int Position { get; set; }
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ProductInfo
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool Available { get; set; }
}

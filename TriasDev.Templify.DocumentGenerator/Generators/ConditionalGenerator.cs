using DocumentFormat.OpenXml.Packaging;

namespace TriasDev.Templify.DocumentGenerator.Generators;

/// <summary>
/// Generates examples demonstrating conditional logic (if/else)
/// </summary>
public class ConditionalGenerator : BaseExampleGenerator
{
    public override string Name => "conditionals";

    public override string Description => "Conditional blocks with if/else logic and boolean expressions";

    public override string GenerateTemplate(string outputDirectory)
    {
        var templatePath = Path.Combine(outputDirectory, $"{Name}-template.docx");

        using (var doc = CreateDocument(templatePath))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            // Title
            AddParagraph(body, "Order Confirmation", isBold: true);
            AddEmptyParagraph(body);

            // Order Information
            AddParagraph(body, "Order #: {{OrderNumber}}");
            AddParagraph(body, "Date: {{OrderDate}}");
            AddParagraph(body, "Customer: {{CustomerName}}");
            AddEmptyParagraph(body);

            // Conditional: Premium Customer
            AddParagraph(body, "{{#if IsPremiumCustomer}}");
            AddParagraph(body, "🌟 Thank you for being a Premium Member!");
            AddParagraph(body, "You enjoy free shipping and priority support.");
            AddParagraph(body, "{{else}}");
            AddParagraph(body, "Upgrade to Premium for exclusive benefits!");
            AddParagraph(body, "{{/if}}");
            AddEmptyParagraph(body);

            // Order Status
            AddParagraph(body, "Order Status:", isBold: true);
            AddParagraph(body, "{{#if Status = \"Shipped\"}}");
            AddParagraph(body, "✓ Your order has been shipped!");
            AddParagraph(body, "Tracking Number: {{TrackingNumber}}");
            AddParagraph(body, "{{else}}");
            AddParagraph(body, "{{#if Status = \"Processing\"}}");
            AddParagraph(body, "⏳ Your order is being processed.");
            AddParagraph(body, "Expected ship date: {{ExpectedShipDate}}");
            AddParagraph(body, "{{else}}");
            AddParagraph(body, "📦 Order Status: {{Status}}");
            AddParagraph(body, "{{/if}}");
            AddParagraph(body, "{{/if}}");
            AddEmptyParagraph(body);

            // Delivery Information
            AddParagraph(body, "Delivery:", isBold: true);
            AddParagraph(body, "{{#if ExpressDelivery}}");
            AddParagraph(body, "⚡ Express Delivery (1-2 business days)");
            AddParagraph(body, "{{else}}");
            AddParagraph(body, "📮 Standard Delivery (3-5 business days)");
            AddParagraph(body, "{{/if}}");
            AddEmptyParagraph(body);

            // Total and Discounts
            AddParagraph(body, "Order Total: {{OrderTotal}}");
            AddParagraph(body, "{{#if HasDiscount}}");
            AddParagraph(body, "Discount Applied: -{{DiscountAmount}} ({{DiscountCode}})");
            AddParagraph(body, "Final Total: {{FinalTotal}}", isBold: true);
            AddParagraph(body, "{{else}}");
            AddParagraph(body, "No discounts applied.");
            AddParagraph(body, "{{/if}}");
            AddEmptyParagraph(body);

            // Payment Method
            AddParagraph(body, "{{#if PaymentMethod = \"CreditCard\"}}");
            AddParagraph(body, "Paid via Credit Card ending in {{CardLastFour}}");
            AddParagraph(body, "{{else}}");
            AddParagraph(body, "{{#if PaymentMethod = \"PayPal\"}}");
            AddParagraph(body, "Paid via PayPal ({{PayPalEmail}})");
            AddParagraph(body, "{{else}}");
            AddParagraph(body, "Payment Method: {{PaymentMethod}}");
            AddParagraph(body, "{{/if}}");
            AddParagraph(body, "{{/if}}");
            AddEmptyParagraph(body);

            // Gift Message
            AddParagraph(body, "{{#if IsGift}}");
            AddParagraph(body, "🎁 This is a gift order!", isBold: true);
            AddParagraph(body, "Gift Message: {{GiftMessage}}");
            AddParagraph(body, "Gift Wrap: {{#if GiftWrap}}Yes{{else}}No{{/if}}");
            AddParagraph(body, "{{/if}}");

            doc.Save();
        }

        return templatePath;
    }

    public override Dictionary<string, object> GetSampleData()
    {
        return new Dictionary<string, object>
        {
            ["OrderNumber"] = "ORD-2025-12345",
            ["OrderDate"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["CustomerName"] = "Jane Smith",

            // Premium customer status
            ["IsPremiumCustomer"] = true,

            // Order status
            ["Status"] = "Shipped",
            ["TrackingNumber"] = "1Z999AA10123456784",
            ["ExpectedShipDate"] = DateTime.Now.AddDays(2).ToString("yyyy-MM-dd"),

            // Delivery options
            ["ExpressDelivery"] = true,

            // Pricing
            ["OrderTotal"] = "$299.99",
            ["HasDiscount"] = true,
            ["DiscountAmount"] = "$30.00",
            ["DiscountCode"] = "PREMIUM10",
            ["FinalTotal"] = "$269.99",

            // Payment
            ["PaymentMethod"] = "CreditCard",
            ["CardLastFour"] = "4242",
            ["PayPalEmail"] = "jane@example.com",

            // Gift options
            ["IsGift"] = true,
            ["GiftMessage"] = "Happy Birthday! Enjoy your new gadget. Love, John",
            ["GiftWrap"] = true
        };
    }
}

// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder.Sample;

/// <summary>
/// The template texts in German and English. A real product would keep them in .resx files
/// (<see cref="TextCatalog.FromResourceManager"/>); a dictionary keeps the sample in one place.
/// </summary>
public static class SampleTexts
{
    private static readonly Dictionary<string, (string De, string En)> _texts = new()
    {
        ["title"] = ("Auftragsbestätigung", "Order confirmation"),
        ["orderNo"] = ("Auftrag", "Order"),
        ["dateFormat"] = ("d. MMMM yyyy", "d MMMM yyyy"),
        ["page"] = ("Seite", "Page"),
        ["footer"] = ("Example Trading GmbH · Sample Street 1 · 80331 München", "Example Trading GmbH · Sample Street 1 · 80331 Munich"),
        ["customer"] = ("Kunde", "Customer"),
        ["newCustomer"] = ("Neukunde", "new customer"),
        ["attn"] = ("z. Hd.", "Attn."),
        ["items"] = ("Positionen", "Items"),
        ["description"] = ("Beschreibung", "Description"),
        ["quantity"] = ("Menge", "Qty"),
        ["amount"] = ("Betrag", "Amount"),
        ["inStock"] = ("auf Lager", "in stock"),
        ["backOrdered"] = ("wird nachgeliefert", "back-ordered"),
        ["discount"] = ("Rabatt", "Discount"),
        ["total"] = ("Gesamtbetrag", "Total"),
        ["freeExpress"] = ("Kostenloser Expressversand:", "Free express shipping:"),
        ["statusPaid"] = ("Bezahlt – vielen Dank!", "Paid – thank you!"),
        ["statusOverdue"] = ("Zahlung überfällig. Bitte begleichen Sie den Betrag umgehend.", "Payment overdue. Please settle the amount promptly."),
        ["statusOpen"] = ("Zahlbar innerhalb von 14 Tagen.", "Payable within 14 days."),
        ["expressFree"] = ("Expressversand – für Sie kostenlos.", "Express shipping – free of charge for you."),
        ["express"] = ("Expressversand (zzgl. 15,00 €).", "Express shipping (plus €15.00)."),
        ["standard"] = ("Standardversand.", "Standard shipping."),
        ["checks"] = ("Prüfungen", "Checks"),
        ["notes"] = ("Hinweise", "Notes"),
        ["terms"] = ("Allgemeine Geschäftsbedingungen", "Terms and conditions"),
        ["termsText"] = (
            "Es gelten unsere Allgemeinen Geschäftsbedingungen in der zum Zeitpunkt der Bestellung gültigen Fassung. Lieferzeiten sind unverbindlich, sofern nicht ausdrücklich anders vereinbart. Die Ware bleibt bis zur vollständigen Bezahlung unser Eigentum.",
            "Our terms and conditions apply in the version valid at the time of the order. Delivery times are non-binding unless expressly agreed otherwise. The goods remain our property until paid in full."),
        ["printedOn"] = ("Gedruckt am", "Printed on"),
    };

    /// <summary>Gets the catalog.</summary>
    public static ITextCatalog Catalog { get; } = TextCatalog.FromDictionary(_texts.ToDictionary(
        e => e.Key,
        e => (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["de"] = e.Value.De, ["en"] = e.Value.En }));
}

// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Formatting;

namespace TriasDev.Templify.Tests.Formatting;

public class BooleanFormatterRegistryTests
{
    [Fact]
    public void Constructor_WithNullCulture_InitializesWithEnglishDefaults()
    {
        // Arrange & Act
        var registry = new BooleanFormatterRegistry(null);

        // Assert
        Assert.True(registry.TryFormat(true, "yesno", out string? result));
        Assert.Equal("Yes", result);
    }

    [Fact]
    public void Constructor_WithoutCulture_InitializesWithEnglishDefaults()
    {
        // Arrange & Act
        var registry = new BooleanFormatterRegistry();

        // Assert
        Assert.True(registry.TryFormat(true, "yesno", out string? result));
        Assert.Equal("Yes", result);
    }

    #region Built-in Formatter Tests

    [Theory]
    [InlineData(true, "checkbox", "☑")]
    [InlineData(false, "checkbox", "☐")]
    public void TryFormat_WithCheckboxFormat_ReturnsCheckboxSymbol(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "checkmark", "✓")]
    [InlineData(false, "checkmark", "✗")]
    [InlineData(true, "check", "✓")]
    [InlineData(false, "check", "✗")]
    public void TryFormat_WithCheckmarkFormat_ReturnsCheckmarkSymbol(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "yesno", "Yes")]
    [InlineData(false, "yesno", "No")]
    public void TryFormat_WithYesNoFormat_ReturnsYesNo(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "truefalse", "True")]
    [InlineData(false, "truefalse", "False")]
    public void TryFormat_WithTrueFalseFormat_ReturnsTrueFalse(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "onoff", "On")]
    [InlineData(false, "onoff", "Off")]
    public void TryFormat_WithOnOffFormat_ReturnsOnOff(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "enabled", "Enabled")]
    [InlineData(false, "enabled", "Disabled")]
    public void TryFormat_WithEnabledFormat_ReturnsEnabledDisabled(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "active", "Active")]
    [InlineData(false, "active", "Inactive")]
    public void TryFormat_WithActiveFormat_ReturnsActiveInactive(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Internationalization Tests

    [Theory]
    [InlineData("de", true, "yesno", "Ja")]
    [InlineData("de", false, "yesno", "Nein")]
    [InlineData("fr", true, "yesno", "Oui")]
    [InlineData("fr", false, "yesno", "Non")]
    [InlineData("es", true, "yesno", "Sí")]
    [InlineData("es", false, "yesno", "No")]
    [InlineData("it", true, "yesno", "Sì")]
    [InlineData("it", false, "yesno", "No")]
    [InlineData("pt", true, "yesno", "Sim")]
    [InlineData("pt", false, "yesno", "Não")]
    [InlineData("nl", true, "yesno", "Ja")]
    [InlineData("nl", false, "yesno", "Nee")]
    [InlineData("pl", true, "yesno", "Tak")]
    [InlineData("pl", false, "yesno", "Nie")]
    [InlineData("ru", true, "yesno", "Да")]
    [InlineData("ru", false, "yesno", "Нет")]
    [InlineData("ja", true, "yesno", "はい")]
    [InlineData("ja", false, "yesno", "いいえ")]
    [InlineData("zh", true, "yesno", "是")]
    [InlineData("zh", false, "yesno", "否")]
    public void TryFormat_WithLocalizedYesNo_ReturnsLocalizedValue(string cultureName, bool value, string format, string expected)
    {
        // Arrange
        var culture = new CultureInfo(cultureName);
        var registry = new BooleanFormatterRegistry(culture);

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("de", true, "truefalse", "Wahr")]
    [InlineData("de", false, "truefalse", "Falsch")]
    [InlineData("fr", true, "truefalse", "Vrai")]
    [InlineData("fr", false, "truefalse", "Faux")]
    [InlineData("es", true, "truefalse", "Verdadero")]
    [InlineData("es", false, "truefalse", "Falso")]
    [InlineData("it", true, "truefalse", "Vero")]
    [InlineData("it", false, "truefalse", "Falso")]
    [InlineData("pt", true, "truefalse", "Verdadeiro")]
    [InlineData("pt", false, "truefalse", "Falso")]
    [InlineData("nl", true, "truefalse", "Waar")]
    [InlineData("nl", false, "truefalse", "Onwaar")]
    [InlineData("pl", true, "truefalse", "Prawda")]
    [InlineData("pl", false, "truefalse", "Fałsz")]
    [InlineData("ru", true, "truefalse", "Истина")]
    [InlineData("ru", false, "truefalse", "Ложь")]
    public void TryFormat_WithLocalizedTrueFalse_ReturnsLocalizedValue(string cultureName, bool value, string format, string expected)
    {
        // Arrange
        var culture = new CultureInfo(cultureName);
        var registry = new BooleanFormatterRegistry(culture);

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("de", true, "onoff", "Ein")]
    [InlineData("de", false, "onoff", "Aus")]
    [InlineData("fr", true, "onoff", "Activé")]
    [InlineData("fr", false, "onoff", "Désactivé")]
    [InlineData("es", true, "onoff", "Encendido")]
    [InlineData("es", false, "onoff", "Apagado")]
    [InlineData("it", true, "onoff", "Acceso")]
    [InlineData("it", false, "onoff", "Spento")]
    [InlineData("pt", true, "onoff", "Ligado")]
    [InlineData("pt", false, "onoff", "Desligado")]
    [InlineData("nl", true, "onoff", "Aan")]
    [InlineData("nl", false, "onoff", "Uit")]
    [InlineData("pl", true, "onoff", "Włączone")]
    [InlineData("pl", false, "onoff", "Wyłączone")]
    [InlineData("ru", true, "onoff", "Вкл")]
    [InlineData("ru", false, "onoff", "Выкл")]
    public void TryFormat_WithLocalizedOnOff_ReturnsLocalizedValue(string cultureName, bool value, string format, string expected)
    {
        // Arrange
        var culture = new CultureInfo(cultureName);
        var registry = new BooleanFormatterRegistry(culture);

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("de", true, "enabled", "Aktiviert")]
    [InlineData("de", false, "enabled", "Deaktiviert")]
    [InlineData("fr", true, "enabled", "Activé")]
    [InlineData("fr", false, "enabled", "Désactivé")]
    [InlineData("es", true, "enabled", "Habilitado")]
    [InlineData("es", false, "enabled", "Deshabilitado")]
    [InlineData("it", true, "enabled", "Abilitato")]
    [InlineData("it", false, "enabled", "Disabilitato")]
    [InlineData("pt", true, "enabled", "Ativado")]
    [InlineData("pt", false, "enabled", "Desativado")]
    [InlineData("nl", true, "enabled", "Ingeschakeld")]
    [InlineData("nl", false, "enabled", "Uitgeschakeld")]
    [InlineData("pl", true, "enabled", "Włączone")]
    [InlineData("pl", false, "enabled", "Wyłączone")]
    [InlineData("ru", true, "enabled", "Включено")]
    [InlineData("ru", false, "enabled", "Отключено")]
    public void TryFormat_WithLocalizedEnabled_ReturnsLocalizedValue(string cultureName, bool value, string format, string expected)
    {
        // Arrange
        var culture = new CultureInfo(cultureName);
        var registry = new BooleanFormatterRegistry(culture);

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("de", true, "active", "Aktiv")]
    [InlineData("de", false, "active", "Inaktiv")]
    [InlineData("fr", true, "active", "Actif")]
    [InlineData("fr", false, "active", "Inactif")]
    [InlineData("es", true, "active", "Activo")]
    [InlineData("es", false, "active", "Inactivo")]
    [InlineData("it", true, "active", "Attivo")]
    [InlineData("it", false, "active", "Inattivo")]
    [InlineData("pt", true, "active", "Ativo")]
    [InlineData("pt", false, "active", "Inativo")]
    [InlineData("nl", true, "active", "Actief")]
    [InlineData("nl", false, "active", "Inactief")]
    [InlineData("pl", true, "active", "Aktywny")]
    [InlineData("pl", false, "active", "Nieaktywny")]
    [InlineData("ru", true, "active", "Активно")]
    [InlineData("ru", false, "active", "Неактивно")]
    public void TryFormat_WithLocalizedActive_ReturnsLocalizedValue(string cultureName, bool value, string format, string expected)
    {
        // Arrange
        var culture = new CultureInfo(cultureName);
        var registry = new BooleanFormatterRegistry(culture);

        // Act
        bool success = registry.TryFormat(value, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("de", "☑")]
    [InlineData("fr", "☑")]
    [InlineData("es", "☑")]
    [InlineData("ja", "☑")]
    [InlineData("zh", "☑")]
    public void TryFormat_WithSymbolFormats_IgnoresCulture(string cultureName, string expected)
    {
        // Arrange - Symbol formatters should be universal, not localized
        var culture = new CultureInfo(cultureName);
        var registry = new BooleanFormatterRegistry(culture);

        // Act
        bool success = registry.TryFormat(true, "checkbox", out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormat_WithUnsupportedCulture_FallsBackToEnglish()
    {
        // Arrange
        var culture = new CultureInfo("ko"); // Korean - not in supported list
        var registry = new BooleanFormatterRegistry(culture);

        // Act
        bool success = registry.TryFormat(true, "yesno", out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal("Yes", result);
    }

    #endregion

    #region Custom Formatter Tests

    [Fact]
    public void Register_WithValidFormatter_AddsFormatter()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();
        var customFormatter = new BooleanFormatter("👍", "👎");

        // Act
        registry.Register("thumbs", customFormatter);
        bool success = registry.TryFormat(true, "thumbs", out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal("👍", result);
    }

    [Fact]
    public void Register_WithNullName_ThrowsArgumentException()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();
        var formatter = new BooleanFormatter("Yes", "No");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.Register(null!, formatter));
    }

    [Fact]
    public void Register_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();
        var formatter = new BooleanFormatter("Yes", "No");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.Register(string.Empty, formatter));
    }

    [Fact]
    public void Register_WithWhitespaceName_ThrowsArgumentException()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();
        var formatter = new BooleanFormatter("Yes", "No");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.Register("   ", formatter));
    }

    [Fact]
    public void Register_WithNullFormatter_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Register("custom", null!));
    }

    [Fact]
    public void Register_WithExistingName_OverwritesFormatter()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();
        var formatter1 = new BooleanFormatter("Yes", "No");
        var formatter2 = new BooleanFormatter("Yep", "Nope");

        // Act
        registry.Register("custom", formatter1);
        registry.Register("custom", formatter2);
        bool success = registry.TryFormat(true, "custom", out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal("Yep", result);
    }

    [Fact]
    public void Register_CanOverrideBuiltInFormatter()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();
        var customCheckbox = new BooleanFormatter("✅", "❌");

        // Act
        registry.Register("checkbox", customCheckbox);
        bool success = registry.TryFormat(true, "checkbox", out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal("✅", result);
    }

    #endregion

    #region TryGetFormatter Tests

    [Fact]
    public void TryGetFormatter_WithExistingFormatter_ReturnsTrue()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryGetFormatter("checkbox", out BooleanFormatter? formatter);

        // Assert
        Assert.True(success);
        Assert.NotNull(formatter);
    }

    [Fact]
    public void TryGetFormatter_WithNonExistingFormatter_ReturnsFalse()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryGetFormatter("nonexistent", out BooleanFormatter? formatter);

        // Assert
        Assert.False(success);
        Assert.Null(formatter);
    }

    [Fact]
    public void TryGetFormatter_WithCustomFormatter_ReturnsCustomFormatter()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();
        var customFormatter = new BooleanFormatter("Custom True", "Custom False");
        registry.Register("custom", customFormatter);

        // Act
        bool success = registry.TryGetFormatter("custom", out BooleanFormatter? formatter);

        // Assert
        Assert.True(success);
        Assert.NotNull(formatter);
        Assert.Equal("Custom True", formatter!.TrueValue);
    }

    #endregion

    #region Case Insensitivity Tests

    [Theory]
    [InlineData("CHECKBOX")]
    [InlineData("CheckBox")]
    [InlineData("checkbox")]
    [InlineData("ChEcKbOx")]
    public void TryFormat_IsCaseInsensitive(string format)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryFormat(true, format, out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal("☑", result);
    }

    [Fact]
    public void Register_IsCaseInsensitive()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();
        var formatter = new BooleanFormatter("Yes", "No");

        // Act
        registry.Register("CUSTOM", formatter);
        bool success = registry.TryFormat(true, "custom", out string? result);

        // Assert
        Assert.True(success);
        Assert.Equal("Yes", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TryFormat_WithUnknownFormat_ReturnsFalse()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        bool success = registry.TryFormat(true, "unknown", out string? result);

        // Assert
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryFormat_MultipleCalls_WorksCorrectly()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act & Assert
        Assert.True(registry.TryFormat(true, "checkbox", out string? result1));
        Assert.Equal("☑", result1);

        Assert.True(registry.TryFormat(false, "yesno", out string? result2));
        Assert.Equal("No", result2);

        Assert.True(registry.TryFormat(true, "checkmark", out string? result3));
        Assert.Equal("✓", result3);
    }

    #endregion
}

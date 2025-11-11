using System.Globalization;

namespace TriasDev.Templify.Formatting;

/// <summary>
/// Registry for boolean formatters, including built-in and custom formatters.
/// </summary>
public sealed class BooleanFormatterRegistry
{
    private readonly Dictionary<string, BooleanFormatter> _formatters;
    private readonly CultureInfo? _culture;

    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanFormatterRegistry"/> class
    /// with default built-in formatters.
    /// </summary>
    /// <param name="culture">Optional culture for localized formatters. If null, uses English.</param>
    public BooleanFormatterRegistry(CultureInfo? culture = null)
    {
        _culture = culture;
        _formatters = new Dictionary<string, BooleanFormatter>(StringComparer.OrdinalIgnoreCase);
        RegisterBuiltInFormatters();
    }

    /// <summary>
    /// Registers a custom formatter.
    /// </summary>
    /// <param name="name">The format name (e.g., "yesno", "checkbox").</param>
    /// <param name="formatter">The formatter instance.</param>
    public void Register(string name, BooleanFormatter formatter)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Format name cannot be null or empty.", nameof(name));
        }

        _formatters[name] = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    /// <summary>
    /// Gets a formatter by name.
    /// </summary>
    /// <param name="name">The format name.</param>
    /// <param name="formatter">The formatter if found; otherwise, null.</param>
    /// <returns>True if the formatter was found; otherwise, false.</returns>
    public bool TryGetFormatter(string name, out BooleanFormatter? formatter)
    {
        return _formatters.TryGetValue(name, out formatter);
    }

    /// <summary>
    /// Formats a boolean value using the specified format name.
    /// </summary>
    /// <param name="value">The boolean value to format.</param>
    /// <param name="formatName">The format name to use.</param>
    /// <param name="result">The formatted string if successful; otherwise, null.</param>
    /// <returns>True if formatting was successful; otherwise, false.</returns>
    public bool TryFormat(bool value, string formatName, out string? result)
    {
        if (TryGetFormatter(formatName, out var formatter))
        {
            result = formatter!.Format(value);
            return true;
        }

        result = null;
        return false;
    }

    private void RegisterBuiltInFormatters()
    {
        // Determine strings based on culture
        var (yesStr, noStr) = GetLocalizedYesNo();
        var (trueStr, falseStr) = GetLocalizedTrueFalse();
        var (onStr, offStr) = GetLocalizedOnOff();
        var (enabledStr, disabledStr) = GetLocalizedEnabledDisabled();
        var (activeStr, inactiveStr) = GetLocalizedActiveInactive();

        // Register culture-aware formatters
        _formatters["yesno"] = new BooleanFormatter(yesStr, noStr);
        _formatters["truefalse"] = new BooleanFormatter(trueStr, falseStr);
        _formatters["onoff"] = new BooleanFormatter(onStr, offStr);
        _formatters["enabled"] = new BooleanFormatter(enabledStr, disabledStr);
        _formatters["active"] = new BooleanFormatter(activeStr, inactiveStr);

        // Register symbol-based formatters (universal, no localization needed)
        _formatters["checkbox"] = new BooleanFormatter("☑", "☐");
        _formatters["checkmark"] = new BooleanFormatter("✓", "✗");
        _formatters["check"] = new BooleanFormatter("✓", "✗");  // Alias
    }

    private (string yes, string no) GetLocalizedYesNo()
    {
        if (_culture == null)
        {
            return ("Yes", "No");
        }

        return _culture.TwoLetterISOLanguageName switch
        {
            "de" => ("Ja", "Nein"),
            "fr" => ("Oui", "Non"),
            "es" => ("Sí", "No"),
            "it" => ("Sì", "No"),
            "pt" => ("Sim", "Não"),
            "nl" => ("Ja", "Nee"),
            "pl" => ("Tak", "Nie"),
            "ru" => ("Да", "Нет"),
            "ja" => ("はい", "いいえ"),
            "zh" => ("是", "否"),
            _ => ("Yes", "No")  // Default to English
        };
    }

    private (string trueVal, string falseVal) GetLocalizedTrueFalse()
    {
        if (_culture == null)
        {
            return ("True", "False");
        }

        return _culture.TwoLetterISOLanguageName switch
        {
            "de" => ("Wahr", "Falsch"),
            "fr" => ("Vrai", "Faux"),
            "es" => ("Verdadero", "Falso"),
            "it" => ("Vero", "Falso"),
            "pt" => ("Verdadeiro", "Falso"),
            "nl" => ("Waar", "Onwaar"),
            "pl" => ("Prawda", "Fałsz"),
            "ru" => ("Истина", "Ложь"),
            _ => ("True", "False")
        };
    }

    private (string on, string off) GetLocalizedOnOff()
    {
        if (_culture == null)
        {
            return ("On", "Off");
        }

        return _culture.TwoLetterISOLanguageName switch
        {
            "de" => ("Ein", "Aus"),
            "fr" => ("Activé", "Désactivé"),
            "es" => ("Encendido", "Apagado"),
            "it" => ("Acceso", "Spento"),
            "pt" => ("Ligado", "Desligado"),
            "nl" => ("Aan", "Uit"),
            "pl" => ("Włączone", "Wyłączone"),
            "ru" => ("Вкл", "Выкл"),
            _ => ("On", "Off")
        };
    }

    private (string enabled, string disabled) GetLocalizedEnabledDisabled()
    {
        if (_culture == null)
        {
            return ("Enabled", "Disabled");
        }

        return _culture.TwoLetterISOLanguageName switch
        {
            "de" => ("Aktiviert", "Deaktiviert"),
            "fr" => ("Activé", "Désactivé"),
            "es" => ("Habilitado", "Deshabilitado"),
            "it" => ("Abilitato", "Disabilitato"),
            "pt" => ("Ativado", "Desativado"),
            "nl" => ("Ingeschakeld", "Uitgeschakeld"),
            "pl" => ("Włączone", "Wyłączone"),
            "ru" => ("Включено", "Отключено"),
            _ => ("Enabled", "Disabled")
        };
    }

    private (string active, string inactive) GetLocalizedActiveInactive()
    {
        if (_culture == null)
        {
            return ("Active", "Inactive");
        }

        return _culture.TwoLetterISOLanguageName switch
        {
            "de" => ("Aktiv", "Inaktiv"),
            "fr" => ("Actif", "Inactif"),
            "es" => ("Activo", "Inactivo"),
            "it" => ("Attivo", "Inattivo"),
            "pt" => ("Ativo", "Inativo"),
            "nl" => ("Actief", "Inactief"),
            "pl" => ("Aktywny", "Nieaktywny"),
            "ru" => ("Активно", "Неактивно"),
            _ => ("Active", "Inactive")
        };
    }
}

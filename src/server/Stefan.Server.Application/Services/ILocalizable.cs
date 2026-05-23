using System.Globalization;

namespace Stefan.Server.Application.Services;

// TODO: should I change it to abstract class??
public interface ILocalizable
{
    IReadOnlyCollection<CultureInfo> SupportedLanguages { get; }

    CultureInfo CurrentLanguage { get; }

    void SetLanguage(CultureInfo culture);
}

public abstract class LocalizableBase : ILocalizable
{
    public abstract IReadOnlyCollection<CultureInfo> SupportedLanguages { get; }

    public CultureInfo CurrentLanguage { get; private set; } = CultureInfo.InvariantCulture;

    public void SetLanguage(CultureInfo culture)
    {
        if (!SupportedLanguages.Contains(culture))
        {
            throw new NotSupportedException($"Language {culture} is not supported.");
        }

        CurrentLanguage = culture;
    }
} 
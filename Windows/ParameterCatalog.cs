namespace FSGolfPL;

/// <summary>Stable source-label to Polish-label mapping used by the desktop overlay.</summary>
public sealed record ParameterDefinition(string EnglishLabel, string PolishLabel);

/// <summary>
/// Raw screen text is retained by future value readers so numeric values and units
/// can be displayed unchanged while only the associated label is localized.
/// </summary>
public sealed record ParameterReading(string EnglishLabel, string RawValue);

/// <summary>Extension point for a future offline OCR or local screen-text reader.</summary>
public interface IParameterValueProvider
{
    bool TryRead(string englishLabel, out ParameterReading? reading);
}

public static class ParameterCatalog
{
    public static IReadOnlyList<ParameterDefinition> All { get; } = Array.AsReadOnly(new ParameterDefinition[]
    {
        new("Lateral", "Odchylenie boczne"),
        new("Club Speed", "Prędkość kija"),
        new("Ball Speed", "Prędkość piłki"),
        new("Spin", "Obroty"),
        new("Spin Loft", "Loft dynamiczny"),
        new("Smash", "Współczynnik uderzenia"),
        new("Launch V", "Kąt startu pionowy"),
        new("Launch H", "Kąt startu poziomy"),
        new("AOA", "Kąt natarcia"),
        new("Height", "Wysokość"),
        new("Flight Time", "Czas lotu"),
        new("Carry", "Lot"),
        new("Roll", "Toczenie"),
        new("Total", "Dystans całkowity")
    });
}

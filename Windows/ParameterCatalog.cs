namespace FSGolfPL;

/// <summary>Stable English labels and their Polish UI translations.</summary>
public sealed record ParameterDefinition(string EnglishLabel, string PolishLabel);

/// <summary>Raw on-screen value kept verbatim when a local value reader is added.</summary>
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
        new("Carry", "Lot"),
        new("Roll", "Toczenie"),
        new("Total", "Dystans całkowity"),
        new("Lateral", "Odchylenie boczne"),
        new("Club Speed", "Prędkość kija"),
        new("Ball Speed", "Prędkość piłki"),
        new("Spin", "Obroty"),
        new("Spin Axis", "Oś obrotu"),
        new("Launch V", "Kąt startu pionowy"),
        new("Launch H", "Kąt startu poziomy")
    });
}

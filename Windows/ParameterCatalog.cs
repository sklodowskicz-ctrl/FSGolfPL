namespace FSGolfPL;

/// <summary>English parameter names and Polish labels shown in the overlay.</summary>
public sealed record ParameterDefinition(string EnglishLabel, string PolishLabel);

/// <summary>Raw reading values remain separate from translated labels.</summary>
public sealed record ParameterReading(string EnglishLabel, string RawValue);

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
        new("Spin Loft", "Loft dynamiczny"),
        new("Smash", "Współczynnik uderzenia"),
        new("Launch V", "Kąt startu pionowy"),
        new("Launch H", "Kąt startu poziomy"),
        new("AOA", "Kąt natarcia")
    });
}

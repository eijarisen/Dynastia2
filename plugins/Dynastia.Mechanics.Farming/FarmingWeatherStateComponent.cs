using Dynastia.Contracts;

namespace Dynastia.Mechanics.Farming;

[PersistedComponentId("farming.weather")]
public sealed class FarmingWeatherStateComponent
{
    public int ResolvedGameYear { get; set; } = int.MinValue;
    public int WeatherYear { get; set; }
    public double WinterTemperature { get; set; }
    public double SpringTemperature { get; set; }
    public double SummerPrecipitation { get; set; }
    public double AutumnPrecipitation { get; set; }
    public decimal RawYieldMultiplier { get; set; } = 1m;
    public bool NewsPublished { get; set; }
}

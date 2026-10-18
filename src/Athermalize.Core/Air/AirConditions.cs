namespace Athermalize.Core.Air;

/// <summary>
/// The air a lens sits in: its temperature, its absolute pressure and, for the air models that use them,
/// its humidity and CO₂ content.
/// </summary>
/// <param name="TemperatureC">Air temperature in °C.</param>
/// <param name="PressurePa">Absolute pressure in pascals. 0 is vacuum.</param>
/// <param name="RelativeHumidity">Relative humidity, 0 to 1. Only <see cref="AirModel.Ciddor1996"/> uses it.</param>
/// <param name="Co2Ppm">CO₂ mole fraction in µmol/mol. Only <see cref="AirModel.Ciddor1996"/> uses it.</param>
public sealed record AirConditions(
    double TemperatureC,
    double PressurePa = AirConditions.StandardPressurePa,
    double RelativeHumidity = 0.0,
    double Co2Ppm = 450.0)
{
    /// <summary>One standard atmosphere, 101 325 Pa.</summary>
    public const double StandardPressurePa = 101325.0;

    /// <summary>20 °C and one atmosphere of dry air: the conditions glass catalogs are written for.</summary>
    public static AirConditions Standard { get; } = new(20.0);

    /// <summary>Vacuum at <paramref name="temperatureC"/>.</summary>
    public static AirConditions Vacuum(double temperatureC) => new(temperatureC, 0.0);

    /// <summary>Dry air at <paramref name="temperatureC"/> and <paramref name="atmospheres"/> times one standard atmosphere.</summary>
    public static AirConditions AtAtmospheres(double temperatureC, double atmospheres) =>
        new(temperatureC, atmospheres * StandardPressurePa);

    /// <summary>The pressure in standard atmospheres, the "relative pressure" P/P₀ of the TIE-19 air formula.</summary>
    public double Atmospheres => PressurePa / StandardPressurePa;

    public bool IsVacuum => PressurePa <= 0.0;
}

using Athermalize.Core.Air;
using Xunit;

namespace Athermalize.Tests;

/// <summary>
/// The air models against NIST's reference values: J. A. Stone and J. H. Zimmerman, "Refractive index of air
/// calculator," NIST Engineering Metrology Toolbox, Table 1 (https://emtoolbox.nist.gov/wavelength/Documentation.asp),
/// computed for 450 µmol/mol of CO₂.
/// </summary>
public class AirTests
{
    // T °C, RH %, p kPa, vacuum λ nm, n by Ciddor's equation, n by the modified Edlén equation.
    public static readonly TheoryData<double, double, double, double, double, double> Nist = new()
    {
        { 20, 0, 101.325, 633, 1.000271800, 1.000271799 },
        { 20, 0, 60, 633, 1.000160924, 1.000160920 },
        { 20, 0, 120, 633, 1.000321916, 1.000321918 },
        { 50, 0, 100, 633, 1.000243285, 1.000243270 },
        { 5, 0, 100, 633, 1.000282756, 1.000282750 },
        { -40, 0, 100, 633, 1.000337580, 1.000337471 },
        { 50, 100, 120, 633, 1.000287924, 1.000287864 },
        { 40, 75, 120, 633, 1.000299418, 1.000299406 },
        { 20, 100, 100, 633, 1.000267394, 1.000267394 },
        { 40, 100, 110, 1700, 1.000270247, 1.000270237 },
        { 20, 0, 101.325, 1700, 1.000268479, 1.000268483 },
        { 40, 100, 110, 300, 1.000289000, 1.000288922 },
        { 20, 0, 101.325, 300, 1.000286581, 1.000286579 },
        { -40, 0, 120, 300, 1.000427233, 1.000427072 },
    };

    /// <summary>Ciddor's equation reproduces NIST's, every row, to 10⁻⁹, the last printed digit.</summary>
    [Theory]
    [MemberData(nameof(Nist))]
    public void Ciddor_matches_NIST(double t, double rh, double kPa, double nm, double ciddor, double edlen)
    {
        _ = edlen;
        double n = AirIndex.Ciddor1996(nm / 1000.0, t, kPa * 1000.0, rh / 100.0, 450.0);
        Assert.InRange(n - ciddor, -1e-9, 1e-9);
    }

    /// <summary>
    /// TIE-19's dry-air formula is a different, older model: for dry air from −40 to +50 °C and 60 to 120 kPa it
    /// stays within 10⁻⁷ of NIST's modified Edlén equation (the most, 8.9×10⁻⁸, at −40 °C, 120 kPa and 300 nm).
    /// It has no humidity term, so humid rows are left out.
    /// </summary>
    [Theory]
    [MemberData(nameof(Nist))]
    public void Tie19_is_near_modified_Edlen_for_dry_air(double t, double rh, double kPa, double nm, double ciddor, double edlen)
    {
        _ = ciddor;
        if (rh != 0) return;
        double n = AirIndex.Tie19(nm / 1000.0, t, kPa * 1000.0 / AirConditions.StandardPressurePa);
        Assert.InRange(n - edlen, -1e-7, 1e-7);
    }

    [Fact]
    public void Humidity_lowers_the_index()
    {
        double dry = AirIndex.Ciddor1996(0.633, 20, 100000, 0.0);
        double wet = AirIndex.Ciddor1996(0.633, 20, 100000, 1.0);
        Assert.InRange(dry - wet, 8e-7, 9e-7);
    }

    [Fact]
    public void Vacuum_has_index_one()
    {
        Assert.Equal(1.0, AirIndex.Of(AirModel.Tie19, 0.55, AirConditions.Vacuum(20)));
        Assert.Equal(1.0, AirIndex.Of(AirModel.Ciddor1996, 0.55, AirConditions.Vacuum(20)));
    }

    /// <summary>
    /// Jamieson (1979, eq. 4) gives dn_air/dT = −0.964×10⁻⁶/K at 0.55 µm and 15 °C from a related form of the
    /// same law. TIE-19's formula gives −0.967×10⁻⁶/K there, the difference being his rounder constant.
    /// </summary>
    [Fact]
    public void Tie19_temperature_coefficient_at_15C()
    {
        double d = (AirIndex.Tie19(0.55, 15.5, 1) - AirIndex.Tie19(0.55, 14.5, 1)) / 1.0;
        Assert.InRange(d * 1e6, -0.970, -0.960);
    }
}

using Athermalize.Core.Air;
using Athermalize.Core.Materials;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

/// <summary>
/// The glasses carried from SCHOTT's IRG data sheets against the tables printed on the same sheets, the index at
/// 22 °C to four decimals and the absolute dn/dT at 22 °C to 0.1×10⁻⁶/K. The sheets print the formulas' constants
/// to four or five figures, so the index agrees to within 2×10⁻⁴ (the sheets give the index's tolerance as ±0.001),
/// and dn/dT to within 0.15×10⁻⁶/K or 1 %, whichever is larger: near λtk, where the E₀ term is large (IRG22 at
/// 1 µm, λtk 0.996 µm; IRG27 at 0.6 µm), the three printed figures of λtk and E₀ cost up to 1 %.
/// </summary>
public class DataSheetMaterialTests
{
    private readonly ITestOutputHelper _out;
    public DataSheetMaterialTests(ITestOutputHelper output) => _out = output;

    // Glass, wavelength µm, printed index at 22 °C, printed dn_abs/dT at 22 °C in 10⁻⁶/K.
    public static readonly TheoryData<string, double, double, double> Printed = new()
    {
        { "IRG22", 1.0, 2.5971, 94.8 }, { "IRG22", 3.0, 2.5182, 68.0 }, { "IRG22", 5.0, 2.5104, 67.7 },
        { "IRG22", 8.0, 2.5028, 67.4 }, { "IRG22", 10.0, 2.4968, 67.1 }, { "IRG22", 12.0, 2.4893, 66.9 },
        { "IRG25", 1.0, 2.7284, 79.2 }, { "IRG25", 3.0, 2.6277, 63.3 }, { "IRG25", 5.0, 2.6183, 62.0 },
        { "IRG25", 8.0, 2.6097, 61.4 }, { "IRG25", 10.0, 2.6030, 61.1 }, { "IRG25", 12.0, 2.5949, 60.8 },
        { "IRG26", 1.0, 2.9316, 76.2 }, { "IRG26", 3.0, 2.8015, 36.2 }, { "IRG26", 5.0, 2.7909, 33.4 },
        { "IRG26", 8.0, 2.7833, 32.5 }, { "IRG26", 10.0, 2.7781, 32.2 }, { "IRG26", 12.0, 2.7722, 32.0 },
        { "IRG27", 0.6, 2.6347, 62.2 }, { "IRG27", 1.0, 2.4841, 15.6 }, { "IRG27", 3.0, 2.4218, -1.9 },
        { "IRG27", 5.0, 2.4129, -3.2 }, { "IRG27", 8.0, 2.3997, -3.6 }, { "IRG27", 9.0, 2.3939, -3.6 },
        { "IRG27", 11.0, 2.3795, -3.7 }, { "IRG27", 12.0, 2.3705, -3.7 },
    };

    [Theory]
    [MemberData(nameof(Printed))]
    public void Matches_the_printed_tables(string glass, double lambda, double index, double dndt)
    {
        var m = Catalogs.Find(glass);
        var air = AirConditions.AtAtmospheres(22, 1);
        double n = m.Index(lambda, air, 22.0).Relative;
        double d = (m.Index(lambda, air, 23.0).Absolute - m.Index(lambda, air, 21.0).Absolute) / 2.0 * 1e6;
        _out.WriteLine($"{glass} {lambda} µm: n {n:F5} ({index}), dn/dT {d:F2} ({dndt})");
        Assert.InRange(n - index, -2.5e-4, 2.5e-4);
        double tolerance = Math.Max(0.15, 0.01 * Math.Abs(dndt));
        Assert.InRange(d - dndt, -tolerance, tolerance);
    }

    /// <summary>
    /// IRG27's sheet prints 2.3842 at 10 µm, out of line with 2.3939 at 9 µm and 2.3795 at 11 µm; the formula gives
    /// 2.3873, as SCHOTT's product page does.
    /// </summary>
    [Fact]
    public void IRG27_at_10_um_follows_the_formula_not_the_misprint()
    {
        double n = Catalogs.Find("IRG27").Index(10.0, AirConditions.AtAtmospheres(22, 1), 22.0).Relative;
        _out.WriteLine($"IRG27 10 µm: {n:F5}");
        Assert.InRange(n - 2.3873, -2e-4, 2e-4);
    }

    [Fact]
    public void Glasses_are_found_by_full_and_bare_name()
    {
        Assert.Same(Catalogs.Find("SCHOTT_IRG:IRG26"), Catalogs.Find("IRG26"));
        var g = (Tie19Material)Catalogs.Find("IRG26");
        Assert.Equal(22.0, g.Thermal.ReferenceTemperatureC);
        Assert.Equal(21.4e-6, g.Expansion.Cte, 15);
        Assert.False(g.Index(10.0, AirConditions.AtAtmospheres(20, 1), 90.0).InRange);
    }
}

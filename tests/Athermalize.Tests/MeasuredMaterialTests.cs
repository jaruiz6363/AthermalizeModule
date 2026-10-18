using Athermalize.Core.Air;
using Athermalize.Core.Materials;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

/// <summary>
/// The measured materials against the index tables printed in the same papers, which their authors computed from
/// the Sellmeier models the module carries. The tables are printed to five decimals, so CaF₂ and fused silica agree
/// to within 5×10⁻⁶. Silicon and germanium agree to within 10⁻⁵: their models cancel large terms, so the
/// coefficients' six printed figures cost the sixth decimal. The papers give the models' fit to the measurements
/// as 10⁻⁴ for Si and Ge, and 10⁻⁵ or better for CaF₂ and fused silica.
/// </summary>
public class MeasuredMaterialTests
{
    private readonly ITestOutputHelper _out;
    public MeasuredMaterialTests(ITestOutputHelper output) => _out = output;

    // Material, kelvin, vacuum wavelength µm, printed absolute index.
    public static readonly TheoryData<string, double, double, double> Printed = new()
    {
        // Frey, Leviton and Madison (2006), Tables 2 and 7.
        { "CHARMS:SI", 295, 1.1, 3.54394 }, { "CHARMS:SI", 295, 1.5, 3.48314 }, { "CHARMS:SI", 295, 2.0, 3.45352 },
        { "CHARMS:SI", 295, 3.0, 3.43293 }, { "CHARMS:SI", 295, 4.0, 3.42589 }, { "CHARMS:SI", 295, 5.0, 3.42274 },
        { "CHARMS:SI", 30, 1.1, 3.51113 },
        { "CHARMS:GE", 295, 2.0, 4.10973 }, { "CHARMS:GE", 295, 3.0, 4.04579 }, { "CHARMS:GE", 295, 4.0, 4.02577 },
        { "CHARMS:GE", 295, 5.0, 4.01678 }, { "CHARMS:GE", 295, 5.5, 4.01404 },
        // Leviton, Frey and Madison (2007), CaF₂.
        { "CHARMS:CAF2", 295, 0.4, 1.44221 }, { "CHARMS:CAF2", 295, 0.5, 1.43683 }, { "CHARMS:CAF2", 295, 0.6, 1.43392 },
        { "CHARMS:CAF2", 295, 1.0, 1.42925 }, { "CHARMS:CAF2", 295, 2.0, 1.42422 }, { "CHARMS:CAF2", 295, 3.0, 1.41821 },
        { "CHARMS:CAF2", 295, 4.0, 1.41001 }, { "CHARMS:CAF2", 295, 5.0, 1.39933 },
        // Leviton and Frey (2006), Corning 7980.
        { "CHARMS:FS_7980", 295, 0.4, 1.47054 }, { "CHARMS:FS_7980", 295, 0.5, 1.46275 }, { "CHARMS:FS_7980", 295, 0.6, 1.45846 },
        { "CHARMS:FS_7980", 295, 0.7, 1.45571 }, { "CHARMS:FS_7980", 295, 0.8, 1.45373 }, { "CHARMS:FS_7980", 295, 1.0, 1.45082 },
        { "CHARMS:FS_7980", 295, 1.5, 1.44502 }, { "CHARMS:FS_7980", 295, 2.0, 1.43849 }, { "CHARMS:FS_7980", 295, 2.6, 1.42823 },
        { "CHARMS:FS_7980", 30, 0.6, 1.45704 },
    };

    [Theory]
    [MemberData(nameof(Printed))]
    public void Matches_the_printed_tables(string name, double kelvin, double lambda, double printed)
    {
        var m = (MeasuredMaterial)Catalogs.Find(name);
        double n = m.AbsoluteIndex(lambda, kelvin);
        _out.WriteLine($"{name} {kelvin} K {lambda} µm: {n:F6} ({printed})");
        double tolerance = name is "CHARMS:SI" or "CHARMS:GE" ? 1e-5 : 5.01e-6;
        Assert.InRange(n - printed, -tolerance, tolerance);
    }

    // Material, kelvin, wavelength µm, printed index relative to air.
    public static readonly TheoryData<string, double, double, double> PrintedLi = new()
    {
        // Li (1980), Table 4.
        { "JPCRD:GE", 100, 10.0, 3.9365 }, { "JPCRD:GE", 293, 2.0, 4.1008 }, { "JPCRD:GE", 293, 3.0, 4.0442 },
        { "JPCRD:GE", 293, 10.0, 4.0025 }, { "JPCRD:GE", 293, 18.0, 3.9996 }, { "JPCRD:GE", 400, 10.0, 4.0488 },
        { "JPCRD:GE", 550, 10.0, 4.1223 },
        // Li (1984), Table 3.
        { "JPCRD:ZNS", 93, 10.0, 2.1928 }, { "JPCRD:ZNS", 293, 0.5, 2.4200 }, { "JPCRD:ZNS", 293, 4.0, 2.2520 },
        { "JPCRD:ZNS", 293, 10.0, 2.2002 }, { "JPCRD:ZNS", 293, 14.0, 2.1318 }, { "JPCRD:ZNS", 618, 10.0, 2.2161 },
        // Li (1984), Table 8.
        { "JPCRD:ZNSE", 93, 10.0, 2.3950 }, { "JPCRD:ZNSE", 293, 0.6, 2.6137 }, { "JPCRD:ZNSE", 293, 3.0, 2.4376 },
        { "JPCRD:ZNSE", 293, 10.0, 2.4065 }, { "JPCRD:ZNSE", 293, 18.0, 2.3305 }, { "JPCRD:ZNSE", 618, 10.0, 2.4289 },
    };

    /// <summary>
    /// Li's models against the tables Li computed from them, printed to four decimals: within 10⁻⁴, the rounding and
    /// the coefficients' printed figures.
    /// </summary>
    [Theory]
    [MemberData(nameof(PrintedLi))]
    public void Li_models_match_the_printed_tables(string name, double kelvin, double lambda, double printed)
    {
        var m = (MeasuredMaterial)Catalogs.Find(name);
        double n = m.AbsoluteIndex(lambda, kelvin) / AirIndex.Tie19(lambda, 20, 1);
        _out.WriteLine($"{name} {kelvin} K {lambda} µm: {n:F6} ({printed})");
        Assert.InRange(n - printed, -1e-4, 1e-4);
    }

    /// <summary>dn/dT at 293 K against Li's tables of the models' temperature derivatives.</summary>
    [Theory]
    [InlineData("JPCRD:GE", 3.0, 4.431e-4)]     // Li (1980), Table 5
    [InlineData("JPCRD:GE", 10.0, 4.033e-4)]
    [InlineData("JPCRD:ZNS", 10.0, 4.1e-5)]     // Li (1984), Table 4
    [InlineData("JPCRD:ZNSE", 3.0, 6.3e-5)]     // Li (1984), Table 9
    public void Li_models_give_the_printed_dn_dT(string name, double lambda, double printed)
    {
        var m = (MeasuredMaterial)Catalogs.Find(name);
        double h = 1.0;
        double dndt = (m.AbsoluteIndex(lambda, 293 + h) - m.AbsoluteIndex(lambda, 293 - h)) / (2 * h) / AirIndex.Tie19(lambda, 20, 1);
        _out.WriteLine($"{name} {lambda} µm: {dndt:E4} ({printed:E3})");
        Assert.InRange(dndt / printed - 1, -0.02, 0.02);
    }

    /// <summary>
    /// Li's germanium against the CHARMS measurement, an independent one, where both hold: within Li's stated
    /// uncertainty of 2×10⁻³ in the index and 0.5×10⁻⁴/K in dn/dT. From 2.5 to 5.5 µm the indices differ by less
    /// than 8×10⁻⁴ and dn/dT by about 5 %. At 2 µm, near the absorption edge at 1.87 µm, Li's index is 6.8×10⁻³
    /// below the measurement, beyond its stated uncertainty: below 2.5 µm the CHARMS model is the one to use.
    /// </summary>
    [Theory]
    [InlineData(2.5)]
    [InlineData(3.0)]
    [InlineData(4.0)]
    [InlineData(5.5)]
    public void Li_germanium_agrees_with_CHARMS(double lambda)
    {
        var li = (MeasuredMaterial)Catalogs.Find("JPCRD:GE");
        var charms = (MeasuredMaterial)Catalogs.Find("CHARMS:GE");
        double T = 295;
        double dn = li.AbsoluteIndex(lambda, T) - charms.AbsoluteIndex(lambda, T);
        double dLi = (li.AbsoluteIndex(lambda, T + 5) - li.AbsoluteIndex(lambda, T - 5)) / 10;
        double dCharms = (charms.AbsoluteIndex(lambda, T) - charms.AbsoluteIndex(lambda, T - 10)) / 10;
        _out.WriteLine($"{lambda} µm: Δn {dn:E2}; dn/dT {dLi:E3} against {dCharms:E3}");
        Assert.InRange(dn, -2e-3, 2e-3);
        Assert.InRange(dLi - dCharms, -0.5e-4, 0.5e-4);
    }

    /// <summary>In air, the relative index is the absolute index over the air's, at the light's vacuum wavelength.</summary>
    [Fact]
    public void Relative_index_in_air()
    {
        var m = (MeasuredMaterial)Catalogs.Find("CHARMS:CAF2");
        var air = AirConditions.AtAtmospheres(20, 1);
        var r = m.Index(0.55, air);
        double nAir = AirIndex.Tie19(0.55, 20, 1);
        Assert.Equal(nAir, r.Air);
        Assert.Equal(0.55 * nAir, r.VacuumWavelengthUm, 15);
        Assert.Equal(m.AbsoluteIndex(0.55 * nAir, 293.15) / nAir, r.Relative, 15);
    }

    [Fact]
    public void Range_is_the_measurement_range()
    {
        var si = Catalogs.Find("CHARMS:SI");
        Assert.True(si.Index(2.0, AirConditions.Vacuum(0)).InRange);
        Assert.False(si.Index(2.0, AirConditions.Vacuum(40)).InRange);    // above 300 K
        Assert.False(si.Index(0.9, AirConditions.Vacuum(0)).InRange);
    }

    /// <summary>Each measured material carries an expansion coefficient with its source.</summary>
    [Theory]
    [InlineData("CHARMS:SI", 2.555e-6)]
    [InlineData("CHARMS:GE", 5.67e-6)]
    [InlineData("CHARMS:CAF2", 18.41e-6)]
    [InlineData("CHARMS:INFRASIL_301", 0.51e-6)]
    [InlineData("CHARMS:FS_7980", 0.52e-6)]
    [InlineData("JPCRD:GE", 5.790e-6)]
    [InlineData("JPCRD:ZNS", 6.8e-6)]
    [InlineData("JPCRD:ZNSE", 7.3e-6)]
    public void Measured_materials_carry_a_sourced_expansion_coefficient(string name, double cte)
    {
        var m = (MeasuredMaterial)Catalogs.Find(name);
        Assert.Equal(cte, m.Expansion.Cte, 15);
        Assert.NotEmpty(m.CteSource);
        Assert.False(double.IsNaN(ThermoOptic.At(m, 2.0, 20.0).OptothermalExpansion));
    }
}

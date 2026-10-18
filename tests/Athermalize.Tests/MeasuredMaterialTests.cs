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
}

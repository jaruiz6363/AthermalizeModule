using Athermalize.Core.Air;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;
using Xunit;

namespace Athermalize.Tests;

/// <summary>
/// Expansion over temperature: the curves against the tables and fits they come from, against an independent
/// formula between the table's points, and the lens growing by them.
/// </summary>
public class ExpansionCurveTests
{
    private static ExpansionCurve Curve(string name) => Catalogs.Find(name).Expansion.Curve!;

    private static double C(double kelvin) => kelvin - 273.15;

    /// <summary>The printed rows, back from the curves: strain from 20 °C or 293 K, and α.</summary>
    [Theory]
    // Swenson (1983), Table 2: ΔL/L₂₀ and α', 10⁻⁶.
    [InlineData("CHARMS:SI", 80.0, -231.6, -0.472)]
    [InlineData("CHARMS:SI", 120.0, -244.4, -0.057)]
    [InlineData("CHARMS:SI", 200.0, -189.7, 1.406)]
    [InlineData("CHARMS:SI", 600.0, 1033.0, 3.844)]
    // NBS Monograph 29, Table 2.1: −(L₂₉₃ − L_T)/L₂₉₃, and α, 10⁻⁶.
    [InlineData("CHARMS:GE", 100.0, -874.0, 2.20)]
    [InlineData("CHARMS:GE", 200.0, -487.0, 4.82)]
    [InlineData("CHARMS:GE", 300.0, 40.0, 5.75)]
    public void Tables_give_their_printed_rows(string name, double kelvin, double strainPpm, double alphaPpm)
    {
        var c = Curve(name);
        double reference = name == "CHARMS:SI" ? 20.0 : C(293.0);
        Assert.Equal(strainPpm, (c.Scale(reference, C(kelvin)) - 1.0) * 1e6, 1);
        Assert.Equal(alphaPpm, c.Alpha(C(kelvin)) * 1e6, 6);
    }

    /// <summary>
    /// Between its rows the silicon curve follows Swenson's eq. (1), the smooth relation the table was computed from
    /// above 300 K and that fits Lyon et al.'s data below it: α = a + A·x²eˣ/(eˣ − 1)² + B·(y − 1)²/(1 + by),
    /// x = θ_E/T, y = T/Φ₀ (Table 1). The curve's α between rows comes mostly from the printed lengths, which are
    /// rounded to 10⁻⁶ above 400 K, so it agrees to 3×10⁻⁸/K, about 1 %.
    /// </summary>
    [Theory]
    [InlineData(95.0)]
    [InlineData(150.0)]
    [InlineData(250.0)]
    [InlineData(310.0)]
    [InlineData(425.0)]
    [InlineData(525.0)]
    [InlineData(775.0)]
    public void Silicon_between_rows_follows_Swensons_relation(double kelvin)
    {
        const double a = -0.687e-6, A = 5.000e-6, thetaE = 685.0, B = 0.220e-6, b = 0.316, phi0 = 395.0;
        double x = thetaE / kelvin, y = kelvin / phi0, ex = Math.Exp(x);
        double expected = a + A * x * x * ex / ((ex - 1) * (ex - 1)) + B * (y - 1) * (y - 1) / (1 + b * y);
        Assert.Equal(expected, Curve("CHARMS:SI").Alpha(C(kelvin)), 3e-8);
    }

    /// <summary>Each curve's coefficient at 20 °C is the material's single coefficient.</summary>
    [Theory]
    [InlineData("CHARMS:SI")]
    [InlineData("CHARMS:GE")]
    [InlineData("CHARMS:CAF2")]
    [InlineData("JPCRD:GE")]
    [InlineData("JPCRD:ZNS")]
    [InlineData("JPCRD:ZNSE")]
    public void A_curve_agrees_with_its_single_coefficient(string name)
    {
        var m = Catalogs.Find(name);
        Assert.Equal(m.Expansion.Cte, m.Expansion.Curve!.Alpha(20.0), m.Expansion.Cte * 5e-3);
    }

    /// <summary>
    /// The tables of the coefficient alone (NBS Technical Note 993, Table 3) are linear between the rows and the
    /// length their integral: ZnS from −40 to +80 °C grows by the mean of the rows, by the trapezoid rule.
    /// </summary>
    [Fact]
    public void A_coefficient_table_integrates_to_the_length()
    {
        var zns = Curve("JPCRD:ZNS");
        double[] rows = { 5.9, 6.3, 6.6, 6.8, 7.0, 7.2, 7.3 };   // −40 to +80 °C by 20
        double area = 0.0;
        for (int i = 1; i < rows.Length; i++) area += 0.5 * (rows[i] + rows[i - 1]) * 20.0;
        Assert.Equal(area * 1e-6, zns.Strain(80.0) - zns.Strain(-40.0), 12);
        Assert.Equal(area * 1e-6, zns.Scale(-40.0, 80.0) - 1.0, 6);
        Assert.Equal(5.9e-6, zns.Alpha(-40.0), 12);
        Assert.Equal(0.5 * (6.6e-6 + 6.8e-6), zns.Alpha(10.0), 12);
        Assert.Equal(6.8e-6, zns.MeanAlpha(20.0, 20.0), 12);
    }

    /// <summary>JPCRD:GE grows by the same lattice expansion its index model carries.</summary>
    [Fact]
    public void Li_germanium_grows_by_its_own_lattice()
    {
        var ge = (MeasuredMaterial)Catalogs.Find("JPCRD:GE");
        var lattice = (LiGermaniumModel)ge.Model;
        foreach (double k in new[] { 100.0, 200.0, 293.0, 400.0, 550.0 })
            Assert.Equal(lattice.Expansion(k), ge.Expansion.Curve!.Strain(C(k)), 15);
        Assert.Equal(5.790e-6, ge.Expansion.Curve!.Alpha(C(293.0)), 15);
    }

    /// <summary>
    /// NIST's fit for 6061-T6: the length is that at 293 K at 293 K, shrinks 0.389 % by 77 K and 0.415 % by 4 K, and
    /// α at 293 K is 22.6×10⁻⁶/K, below the 23.6 mean from 20 to 100 °C as an instantaneous value at 20 °C should be.
    /// </summary>
    [Fact]
    public void Cryogenic_aluminum_follows_NISTs_fit()
    {
        var al = MountMaterials.Find("aluminum-6061-t6-cryo")!;
        var c = al.Curve!;
        Assert.Equal(0.0, c.Strain(C(293.0)), 6);
        Assert.Equal(-3.887e-3, c.Strain(C(77.0)), 5);
        Assert.Equal(-415.45e-5, c.Strain(C(4.0)), 7);
        Assert.Equal(22.6e-6, c.Alpha(C(293.0)), 7);
        Assert.Equal(al.Cte, c.Alpha(20.0), 15);
        foreach (var name in new[] { "stainless-304-cryo", "ti-6al-4v-cryo", "invar-36-cryo" })
        {
            var m = MountMaterials.Find(name)!;
            Assert.Equal(0.0, m.Curve!.Strain(C(293.0)), 5);
            Assert.InRange(m.Cte, 0.5e-6, 20e-6);
        }
    }

    /// <summary>Outside a table the coefficient is held above it and falls to zero at 0 K below it.</summary>
    [Fact]
    public void Tables_extrapolate_gently()
    {
        var caf2 = Curve("CHARMS:CAF2");
        Assert.False(caf2.InRange(-248.0));
        Assert.Equal(6.7e-6 * 25.0 / 93.15, caf2.Alpha(C(25.0)), 12);
        Assert.Equal(22.2e-6, caf2.Alpha(300.0), 12);
        // The length is continuous at the table's ends.
        Assert.Equal(caf2.Strain(-180.0 - 1e-9), caf2.Strain(-180.0 + 1e-9), 12);
        Assert.Equal(caf2.Strain(200.0 - 1e-9), caf2.Strain(200.0 + 1e-9), 12);
    }

    /// <summary>One coefficient grows a length by exactly 1 + αΔT, as before curves.</summary>
    [Fact]
    public void One_coefficient_is_linear()
    {
        var c = new ConstantExpansion(7.1e-6);
        Assert.Equal(1.0 + 7.1e-6 * 60.0, c.Scale(20.0, 80.0), 15);
        Assert.Equal(1.0 - 7.1e-6 * 60.0, c.Scale(20.0, -40.0), 15);
    }

    /// <summary>
    /// A silicon singlet in a cryogenic aluminum housing, cooled from 20 °C to 77 K in vacuum: its radius and
    /// thickness shrink by Swenson's −230×10⁻⁶ (between the 75 and 80 K rows), not by α·ΔT = −552×10⁻⁶, and the air
    /// space by the aluminum's −0.389 %.
    /// </summary>
    [Fact]
    public void A_cold_lens_shrinks_by_its_curves()
    {
        var si = Catalogs.Find("CHARMS:SI");
        var system = Lenses.AtInfinity(10, 4.0)
            .Surface(100.0, 5.0, "SI", stop: true)
            .Surface(0.0, 190.0)
            .Image();
        var housing = MountMaterials.Find("aluminum-6061-t6-cryo")!;
        var options = new ThermalOptions(SurfaceMaterials: new Dictionary<int, Material> { [1] = si });
        var model = new ThermalModel(system, Catalogs.Library, new Mount(housing), AirConditions.Vacuum(20.0), options);
        var lens = model.At(AirConditions.Vacuum(C(77.0)));

        double glass = lens.System.Surfaces[1].Thickness / 5.0 - 1.0;
        Assert.InRange(glass, -231.6e-6, -229.3e-6);
        Assert.Equal(1.0 + glass, system.Surfaces[1].Curvature / lens.System.Surfaces[1].Curvature, 12);
        Assert.Equal(housing.Curve!.Scale(20.0, C(77.0)), lens.System.Surfaces[2].Thickness / 190.0, 12);
        Assert.Equal(-0.389e-2, lens.System.Surfaces[2].Thickness / 190.0 - 1.0, 4);
        Assert.Empty(lens.Notes.Where(n => n.Contains("expansion data")));

        var warmer = model.At(AirConditions.Vacuum(60.0));    // above NIST's 300 K
        Assert.Contains(warmer.Notes, n => n.Contains("aluminum-6061-t6-cryo's expansion data cover"));
    }
}

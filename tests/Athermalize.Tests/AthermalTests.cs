using Athermalize.Core.Air;
using Athermalize.Core.Analysis;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

/// <summary>Thin-lens athermalization against the literature and against the full lens model.</summary>
public class AthermalTests
{
    private readonly ITestOutputHelper _out;
    public AthermalTests(ITestOutputHelper output) => _out = output;

    private static MaterialConstants Constants(double omega, double gamma, double n = 1.5) =>
        new(LinearMaterial.Constant("X", n, 0, 0), n, omega, 0.0, gamma, 0.0, true);

    /// <summary>
    /// Jiang and Liu (2024), Table 3: the final eight glasses of their telephoto, by ω, γ, φ and h, in an aluminum
    /// housing of α_h = 22.5×10⁻⁶/K. They print the composite point as ω′ = −0.26×10⁻³ and γ′ = 0.22×10⁻⁶/K; from
    /// their table, rounded as printed, it comes to −0.277×10⁻³ and 0.205×10⁻⁶/K.
    /// </summary>
    [Fact]
    public void Jiang_composite_point()
    {
        (double W, double G, double Phi, double H)[] table =
        {
            (12.253e-3, -25.887e-6, 0.0126, 14.249), (18.515e-3, 5.052e-6, -0.0113, 14.038),
            (17.088e-3, -6.026e-6, -0.0104, 13.813), (16.383e-3, 8.774e-6, 0.0148, 14.021),
            (14.937e-3, 1.030e-6, 0.0119, 10.574), (25.189e-3, 2.028e-6, -0.0137, 10.097),
            (16.447e-3, -0.825e-6, 0.0102, 8.888), (18.278e-3, -0.945e-6, -0.0186, 1.145),
        };
        var r = Athermal.Residuals(table.Select(t => new ThinLens(t.W, t.G, t.Phi, t.H)).ToList(), 22.5e-6);
        _out.WriteLine($"ω′ {r.Omega * 1e3:F3}e-3, γ′ {r.Gamma * 1e6:F3}e-6/K");
        Assert.InRange(r.Omega - -0.26e-3, -0.03e-3, 0.03e-3);
        Assert.InRange(r.Gamma - 0.22e-6, -0.03e-6, 0.03e-6);
    }

    /// <summary>
    /// Jiang and Liu (2024), Table 2: the objective f(s) of their five best glass sets from their normalized
    /// residuals, with τ_ptz = 0.1, the other τ 1 and W_power = 2: 8.55, 8.83, 9.33, 9.36 and 9.93. From the table's
    /// rounded columns the formula gives 8.53, 8.79, 9.36, 9.38 and 9.96.
    /// </summary>
    [Theory]
    [InlineData(0.06, 0.02, 4.81, 0.67, 0.36, 8.55)]
    [InlineData(0.00, 0.15, 4.76, 0.87, 0.38, 8.83)]
    [InlineData(0.08, 0.36, 4.90, 0.37, 0.37, 9.33)]
    [InlineData(0.07, 0.38, 4.91, 0.29, 0.37, 9.36)]
    [InlineData(0.43, 0.18, 5.15, 0.33, 0.36, 9.93)]
    public void Jiang_objective(double thermal, double chromatic, double secondary, double petzval, double power, double f)
    {
        Assert.InRange(new JiangObjective().Evaluate(thermal, chromatic, secondary, petzval, power) - f, -0.05, 0.05);
    }

    /// <summary>The objective from residuals: a system that is exactly achromatic, athermal and flat scores 0.</summary>
    [Fact]
    public void Jiang_objective_of_residuals()
    {
        var o = new JiangObjective();
        Assert.Equal(0.0, o.Evaluate(new ThinResiduals(1.0, 0, 0, 0, 0, 0, 0), 0.3));
        Assert.Equal(1.0, o.Evaluate(new ThinResiduals(2.0, 0, 2 * 7.8e-6, 0, 0, 0, 0), 0.0), 12);
    }

    /// <summary>
    /// Zhang et al. (2012), eqs. 6-8, with γ + α_h: the triplet's powers make it of the given power, achromatic and
    /// athermal; three materials on one line of the map have no triplet.
    /// </summary>
    [Fact]
    public void The_athermal_achromatic_triplet()
    {
        var a = Constants(1.0 / 1000, 126e-6, 4.003);
        var b = Constants(1.0 / 57.9, 36.3e-6, 2.406);
        var c = Constants(1.0 / 22.7, 25.9e-6, 2.200);
        var p = Athermal.Triplet(a, b, c, 0.01, 23e-6)!;
        var r = Athermal.Residuals(new[] { ThinLens.Of(a, p[0]), ThinLens.Of(b, p[1]), ThinLens.Of(c, p[2]) }, 23e-6);
        _out.WriteLine($"powers {p[0]:G5}, {p[1]:G5}, {p[2]:G5}");
        Assert.Equal(0.01, r.Power, 15);
        Assert.InRange(r.Chromatic, -1e-17, 1e-17);
        Assert.InRange(r.Thermal, -1e-20, 1e-20);
        Assert.Null(Athermal.Triplet(Constants(0.01, 1e-6), Constants(0.02, 2e-6), Constants(0.03, 3e-6), 0.01, 0.0));
    }

    /// <summary>
    /// A doublet is achromatic by its powers, and athermal only when its two points lie on a line through the
    /// housing's point (0, −α_h): then the triplet of the two and any third material puts no power in the third.
    /// </summary>
    [Fact]
    public void A_doublet_is_athermal_when_its_line_passes_through_the_housing()
    {
        var a = Constants(0.016, -2e-6);
        var b = Constants(0.036, 18e-6);       // the line through a and b meets ω = 0 at γ = −18×10⁻⁶
        var (pa, pb, r) = Athermal.Doublet(a, b, 0.01, 18e-6)!.Value;
        Assert.Equal(0.01, pa + pb, 15);
        Assert.InRange(r.Chromatic, -1e-17, 1e-17);
        Assert.InRange(r.Thermal, -1e-20, 1e-20);
        var (_, _, warm) = Athermal.Doublet(a, b, 0.01, 23e-6)!.Value;
        Assert.Equal(-(23e-6 - 18e-6) * 0.01 / (0.01 * 0.01), warm.DefocusPerK, 12);
    }

    /// <summary>
    /// Jiang and Liu (2024): a set can be athermal and achromatic only if the origin lies in the convex hull of its
    /// composite points. A positive and a negative lens straddling the origin's line can; two positive lenses can't.
    /// </summary>
    [Fact]
    public void The_feasibility_test()
    {
        var crown = Constants(0.015, -5e-6);
        var flint = Constants(0.035, 10e-6);
        Assert.False(Athermal.CanAthermalize(new[] { (crown, 1), (flint, 1) }, 20e-6));
        // Housing α_h makes the composite points (±ω, ±(γ + α_h)); this pair's line misses the origin, a third fixes it.
        Assert.False(Athermal.CanAthermalize(new[] { (crown, 1), (flint, -1) }, 20e-6));
        var third = Constants(0.020, -40e-6);
        Assert.True(Athermal.CanAthermalize(new[] { (crown, 1), (flint, -1), (third, 1) }, 20e-6));
    }

    /// <summary>
    /// Schuster and Franks (2012), Table 2: γ_T = (dn/dT)/(n − 1) − α at 10 µm for Ge, GaAs, IKS25, GASIR 1, ZnSe
    /// and ZnS from their n, dn/dT and α. Each agrees to the printed 0.1 but ZnSe's, printed 36.6 for 36.3.
    /// </summary>
    [Theory]
    [InlineData("Ge", 4.003, 396, 5.7, 126)]
    [InlineData("GaAs", 3.278, 185, 5, 76.2)]
    [InlineData("IKS25", 2.7675, 46, 20.85, 5.18)]
    [InlineData("GASIR 1", 2.494, 55, 17, 19.9)]
    [InlineData("ZnSe", 2.406, 61, 7.1, 36.3)]
    [InlineData("ZnS", 2.200, 39, 6.6, 25.9)]
    public void Schuster_thermal_glass_constants(string name, double n, double dndt, double alpha, double gamma)
    {
        var m = LinearMaterial.Constant(name, n, dndt * 1e-6, alpha * 1e-6);
        var c = MaterialConstants.Of(m, Band.Lwir);
        Assert.InRange(c.Gamma * 1e6 - gamma, -0.25, 0.25);
    }

    /// <summary>
    /// Schuster and Franks (2012), eq. 4 and Tables 1 and 3: a GASIR 1 lens in a housing of α_H moves its focus by
    /// one depth of focus, 2λN² at 10 µm, in ΔT = 2λN²/(f(α_H + γ_T)). Their thin-lens values, from f and the
    /// working F-number, are 84, 52, 36, 18 and 12 K in aluminum (α_H = 23×10⁻⁶/K), and 203 and 115 K for their
    /// passive mounts of equivalent α_H −9.1 and −6.6×10⁻⁶/K.
    /// </summary>
    [Theory]
    [InlineData(8.5, 1.24, 23.0, 84)]
    [InlineData(13.3, 1.22, 23.0, 52)]
    [InlineData(20.0, 1.24, 23.0, 36)]
    [InlineData(35.0, 1.17, 23.0, 18)]
    [InlineData(60.0, 1.25, 23.0, 12)]
    [InlineData(14.1, 1.24, -9.1, 203)]
    [InlineData(20.0, 1.24, -6.6, 115)]
    public void Schuster_temperature_for_one_depth_of_focus(double f, double rf, double housing, double expected)
    {
        var gasir = LinearMaterial.Constant("GASIR 1", 2.494, 55e-6, 17e-6);
        double r = 2.0 * f * 1.494;
        var system = Lenses.AtInfinity(f / rf, 10.0).Surface(r, 1e-4, "G", stop: true).Surface(-r, f).Image();
        system.Fields.RemoveAt(1);
        var model = new ThermalModel(system, Catalogs.Library, new Mount(MountMaterial.OfCte(housing * 1e-6)),
            options: new ThermalOptions(Substitutions: new Dictionary<string, Material> { ["G"] = gasir }));
        var report = FocusReport.Compute(model, new[] { 20.0 });
        _out.WriteLine($"f {f}: {report.TemperatureForOneDepthK:F1} K against {expected}");
        Assert.InRange(report.TemperatureForOneDepthK - expected, -1.5, 1.5);
    }

    /// <summary>
    /// A thin achromatic doublet from the solver, built as two thin lenses in contact and run through the full
    /// model: its axial color is nil, to 10⁻⁵ mm for lenses 0.1 µm thick, and its focus rate is the thin-lens −R_th/φ².
    /// </summary>
    [Fact]
    public void A_thin_doublet_from_the_solver_behaves_so_in_the_full_model()
    {
        var crown = MaterialConstants.Of(Catalogs.Find("SCHOTT:N-BK7"), Band.Visible);
        var flint = MaterialConstants.Of(Catalogs.Find("SCHOTT:N-SF6"), Band.Visible);
        const double housing = 23.6e-6, power = 0.01;
        var (pa, pb, r) = Athermal.Doublet(crown, flint, power, housing)!.Value;
        double ra = 2.0 * (crown.Index - 1.0) / pa, rb = 2.0 * (flint.Index - 1.0) / pb;
        var system = Lenses.AtInfinity(4, 0.5875618, 0.4861327, 0.6562725)
            .Surface(ra, 1e-4, "N-BK7", stop: true).Surface(-ra, 0.0)
            .Surface(rb, 1e-4, "N-SF6").Surface(-rb, 100.0)
            .Image();
        system.Fields.RemoveAt(1);
        var model = new ThermalModel(system, Catalogs.Library, new Mount(MountMaterial.OfCte(housing)));
        var lens = model.At(20.0);
        double Focus(int w) => AberrationCalculator.Core.RayTrace.ParaxialTrace.Trace(lens.System, lens.Indices[w], 0).ParaxialFocusDistance;
        double color = Focus(1) - Focus(2);
        system.Surfaces[4].Thickness = Focus(0);
        var report = FocusReport.Compute(new ThermalModel(system, Catalogs.Library, new Mount(MountMaterial.OfCte(housing))), new[] { 20.0 });
        _out.WriteLine($"F − C focus {color * 1e3:F4} µm; rate {report.DefocusPerK * 1e3:F4} µm/K against thin lens {r.DefocusPerK * 1e3:F4}");
        Assert.InRange(color, -1e-5, 1e-5);
        Assert.InRange(report.DefocusPerK / r.DefocusPerK - 1.0, -2e-3, 2e-3);
    }
}

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Athermalize.Core.Air;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

/// <summary>
/// The lens at a temperature against closed forms and invariances: the nominal state, a lens that only grows, a
/// mirror, an all-metal telescope, thin and thick singlets, a spacer touching the glass off the axis, a chain of
/// barrel sections, and the stop.
/// </summary>
public class ThermalLensTests
{
    private readonly ITestOutputHelper _out;
    public ThermalLensTests(ITestOutputHelper output) => _out = output;

    private static readonly MountMaterial Aluminum = new("aluminum", 23.6e-6, "Liu et al. 2025");
    private static readonly MountMaterial Titanium = new("titanium", 9.2e-6, "Liu et al. 2025");
    private static readonly MountMaterial Invar = new("Invar", 2.4e-6, "Liu et al. 2025");

    /// <summary>Puts the image plane at the paraxial focus of the primary wavelength, at the nominal conditions.</summary>
    private static OpticalSystem Focused(OpticalSystem system, MaterialLibrary library, ThermalOptions? options = null,
        AirConditions? air = null)
    {
        var model = new ThermalModel(system, library, new Mount(Aluminum), air, options);
        var trace = ParaxialTrace.Trace(system, model.NominalIndices()[0], 0.0);
        system.Surfaces[system.LastOpticalSurface()].Thickness = trace.ParaxialFocusDistance;
        return system;
    }

    private static OpticalSystem Doublet() =>
        Focused(Lenses.AtInfinity(20, 0.5875618, 0.4861327, 0.6562725)
            .Surface(61.07, 6.0, "N-BK7", stop: true, semiDiameter: 11)
            .Surface(-44.17, 2.5, "N-SF6", semiDiameter: 11)
            .Surface(-128.89, 0.0, semiDiameter: 11)
            .Image(), Catalogs.Library);

    [Fact]
    public void At_its_own_temperature_the_lens_is_the_nominal_design()
    {
        var system = Doublet();
        system.GlassCatalogs.Add("SCHOTT");
        var model = new ThermalModel(system, Catalogs.Library, new Mount(Aluminum));
        var lens = model.At(20.0);
        for (int w = 0; w < system.Wavelengths.Count; w++)
        {
            var expected = IndexResolver.Build(system, Catalogs.Library.Glasses, system.Wavelengths[w].Value);
            for (int i = 0; i < expected.Length; i++)
                Assert.Equal(expected[i], lens.Indices[w][i], 12);
            Assert.Equal(system.Wavelengths[w].Value, lens.System.Wavelengths[w].Value, 14);
        }
        for (int i = 0; i < system.Surfaces.Count; i++)
        {
            Assert.Equal(system.Surfaces[i].Curvature, lens.System.Surfaces[i].Curvature);
            Assert.Equal(system.Surfaces[i].Thickness, lens.System.Surfaces[i].Thickness);
        }
        Assert.Equal(system.Aperture.Value, lens.System.Aperture.Value, 12);
    }

    [Fact]
    public void Building_a_temperature_never_changes_the_nominal_design()
    {
        var system = Doublet();
        var radii = system.Surfaces.Select(s => s.Curvature).ToArray();
        var thicknesses = system.Surfaces.Select(s => s.Thickness).ToArray();
        var model = new ThermalModel(system, Catalogs.Library, new Mount(Aluminum));
        var hot = model.At(80.0);
        Assert.Equal(radii, system.Surfaces.Select(s => s.Curvature));
        Assert.Equal(thicknesses, system.Surfaces.Select(s => s.Thickness));
        Assert.Equal(0.5875618, system.Wavelengths[0].Value);
        Assert.NotEqual(radii[1], hot.System.Surfaces[1].Curvature);
    }

    /// <summary>
    /// When every part has the same expansion coefficient and no index changes (model glasses without thermal data,
    /// in vacuum), the lens only grows: every length by 1 + αΔT, the focal length too, and it stays in focus. The
    /// lens has an asphere, a spacer touching the glass off the axis and a part holding the image plane, and they
    /// grow with it.
    /// </summary>
    [Theory]
    [InlineData(-60.0)]
    [InlineData(80.0)]
    public void A_lens_whose_parts_all_expand_alike_stays_in_focus(double t)
    {
        const double alpha = 9.2e-6;
        var options = new ThermalOptions(Substitutions: new Dictionary<string, Material>
        {
            ["G1"] = Tie19Material.Model(1.5168, 64.17, 0.0, null, alpha),
            ["G2"] = Tie19Material.Model(1.7552, 27.58, 0.0, null, alpha),
        });
        var vacuum = AirConditions.Vacuum(20.0);
        var system = Focused(Lenses.AtInfinity(20)
            .Surface(61.07, 6.0, "G1", stop: true, semiDiameter: 11)
            .Surface(-44.17, 2.5, "G2", semiDiameter: 11)
            .Surface(-128.89, 5.0, semiDiameter: 11)
            .Surface(40.0, 4.0, "G1", semiDiameter: 10, conic: -0.6, aspheric: new[] { 0.0, -2e-6, 3e-9, -1e-12 })
            .Surface(200.0, 0.0, semiDiameter: 10)
            .Image(), Catalogs.Library, options, vacuum);

        var mount = new Mount(new MountMaterial("housing", alpha),
            new[] { MountSegment.Spacer(3, new MountMaterial("spacer", alpha), 9.0), new MountSegment(1, 6, new MountMaterial("barrel", alpha)) });
        var model = new ThermalModel(system, Catalogs.Library, mount, vacuum, options);
        var nominal = model.At(vacuum);
        var lens = model.At(AirConditions.Vacuum(t));
        double s = 1.0 + alpha * (t - 20.0);

        for (int i = 1; i < system.Surfaces.Count - 1; i++)
        {
            Assert.Equal(system.Surfaces[i].Curvature / s, lens.System.Surfaces[i].Curvature, 15);
            Assert.Equal(system.Surfaces[i].Thickness * s, lens.System.Surfaces[i].Thickness, 11);
            for (int k = 0; k < 4; k++)
            {
                double a = system.Surfaces[i].AsphericCoefficients[k];
                double expected = a * Math.Pow(s, -(2 * k + 1));
                Assert.InRange(lens.System.Surfaces[i].AsphericCoefficients[k] - expected,
                    -1e-14 * Math.Abs(a), 1e-14 * Math.Abs(a));
            }
        }
        Assert.Equal(nominal.PrimaryIndices, lens.PrimaryIndices);
        Assert.Equal(Lenses.Paraxial(nominal).Efl * s, Lenses.Paraxial(lens).Efl, 10);
        Assert.InRange(Lenses.Defocus(lens), -1e-10, 1e-10);
    }

    /// <summary>An afocal telescope that only grows stays afocal, with the same magnification.</summary>
    [Fact]
    public void An_afocal_telescope_whose_parts_all_expand_alike_stays_afocal()
    {
        const double alpha = 23.6e-6;
        var options = new ThermalOptions(Substitutions: new Dictionary<string, Material>
        {
            ["G"] = Tie19Material.Model(1.5168, 64.17, 0.0, null, alpha),
        });
        var vacuum = AirConditions.Vacuum(20.0);
        OpticalSystem Telescope(double gap) => Lenses.AtInfinity(10)
            .Surface(103.0, 4.0, "G", stop: true).Surface(-103.0, gap)
            .Surface(26.0, 3.0, "G", semiDiameter: 5).Surface(-26.0, 10.0, semiDiameter: 5)
            .Image();

        double ExitSlope(OpticalSystem sys)
        {
            var m = new ThermalModel(sys, Catalogs.Library, new Mount(Aluminum), vacuum, options);
            var tr = ParaxialTrace.Trace(sys, m.NominalIndices()[0], 0.0);
            return tr.U[sys.LastOpticalSurface()];
        }
        // The exit slope is linear in the gap: two traces find the afocal one.
        double u1 = ExitSlope(Telescope(120.0)), u2 = ExitSlope(Telescope(130.0));
        var system = Telescope(120.0 - u1 * 10.0 / (u2 - u1));
        system.IsAfocal = true;

        var model = new ThermalModel(system, Catalogs.Library, new Mount(new MountMaterial("housing", alpha)), vacuum, options);
        var nominal = ParaxialTrace.Trace(system, model.NominalIndices()[0], 1.0);
        Assert.InRange(nominal.U[system.LastOpticalSurface()], -1e-14, 1e-14);
        foreach (double t in new[] { -40.0, 90.0 })
        {
            var lens = model.At(AirConditions.Vacuum(t));
            var trace = ParaxialTrace.Trace(lens.System, lens.PrimaryIndices, 1.0);
            int last = system.LastOpticalSurface();
            Assert.InRange(trace.U[last], -1e-14, 1e-14);
            Assert.Equal(nominal.Ubar[last] / nominal.Ubar[0], trace.Ubar[last] / trace.Ubar[0], 12);
        }
    }

    /// <summary>
    /// A concave mirror on a spacer of its own material stays in focus at every temperature, in air. On a spacer
    /// of another material it defocuses by f·(α_mirror − α_spacer)·ΔT, f = R/2.
    /// </summary>
    [Theory]
    [InlineData(-40.0)]
    [InlineData(70.0)]
    public void A_mirror_on_a_spacer_of_its_own_material_stays_in_focus(double t)
    {
        var system = Lenses.AtInfinity(20).Surface(-200.0, -100.0, "MIRROR", stop: true, semiDiameter: 15).Image();
        var same = new ThermalModel(system, Catalogs.Library,
            new Mount(Aluminum, mirrorSubstrates: new Dictionary<int, MountMaterial> { [1] = Aluminum }));
        Assert.Equal(0.0, Lenses.Defocus(same.At(20.0)), 12);
        Assert.InRange(Lenses.Defocus(same.At(t)), -1e-11, 1e-11);

        var glass = new MountMaterial("low-expansion substrate", 0.05e-6);
        var other = new ThermalModel(system, Catalogs.Library,
            new Mount(Aluminum, mirrorSubstrates: new Dictionary<int, MountMaterial> { [1] = glass }));
        double expected = -100.0 * (glass.Cte - Aluminum.Cte) * (t - 20.0);
        Assert.Equal(expected, Lenses.Defocus(other.At(t)), 11);
    }

    /// <summary>
    /// Xu et al. (2019), Fig. 8: a Ritchey-Chrétien telescope with aluminum mirrors and tube (α = 23×10⁻⁶/K) has a
    /// back focal length of 190.1526, 190.5032 and 190.8537 mm at −60, +20 and +100 °C. The paper does not print
    /// the telescope; any all-aluminum two-mirror telescope with that back focal length grows the same way, so the
    /// test writes one and scales it to 190.5032 mm.
    /// </summary>
    [Fact]
    public void An_all_aluminum_telescope_grows_with_its_aluminum()
    {
        var al = new MountMaterial("AL6061", 23e-6, "Xu et al. 2019");
        var design = Lenses.AtInfinity(100)
            .Surface(-1000.0, -350.0, "MIRROR", stop: true, semiDiameter: 60, conic: -1.08)
            .Surface(-450.0, 450.0, "MIRROR", semiDiameter: 20, conic: -3.2)
            .Image();
        var mirrors = new Dictionary<int, MountMaterial> { [1] = al, [2] = al };
        double bfl0 = Lenses.Paraxial(new ThermalModel(design, Catalogs.Library, new Mount(al, mirrorSubstrates: mirrors)).At(20.0)).Bfl;
        double k = 190.5032 / bfl0;
        foreach (var s in design.Surfaces.Skip(1).SkipLast(1))
        {
            s.Curvature /= k;
            s.Thickness *= k;
            s.SemiDiameter *= k;
        }
        design.Surfaces[2].Thickness = 190.5032;
        design.Aperture = new AberrationCalculator.Core.Models.Aperture(AberrationCalculator.Core.Enums.ApertureType.EPD, 100.0 * k);

        var model = new ThermalModel(design, Catalogs.Library, new Mount(al, mirrorSubstrates: mirrors));
        Assert.Equal(190.5032, Lenses.Paraxial(model.At(20.0)).Bfl, 9);
        var (cold, hot) = (model.At(-60.0), model.At(100.0));
        _out.WriteLine($"BFL {Lenses.Paraxial(cold).Bfl:F6} and {Lenses.Paraxial(hot).Bfl:F6} mm");
        Assert.InRange(Lenses.Paraxial(cold).Bfl - 190.1526, -1e-4, 1e-4);
        Assert.InRange(Lenses.Paraxial(hot).Bfl - 190.8537, -1e-4, 1e-4);
        Assert.Equal(190.5032 * (1 + 23e-6 * 80), Lenses.Paraxial(hot).Bfl, 9);
        Assert.InRange(Lenses.Defocus(hot), -1e-10, 1e-10);
        Assert.InRange(Lenses.Defocus(cold), -1e-10, 1e-10);
    }

    /// <summary>
    /// A thin singlet at infinity in a housing that holds its image plane defocuses by f·(x_f − α_h)·ΔT, x_f being
    /// the glass's thermal coefficient of focal length (Jamieson 1981).
    /// </summary>
    [Fact]
    public void A_thin_singlet_defocuses_by_its_focal_length_times_xf_less_the_housing()
    {
        var m = Catalogs.Find("SCHOTT:N-BK7");
        double nd = m.Index(0.5875618, AirConditions.Standard).Relative;
        double r = 2.0 * 100.0 * (nd - 1.0);
        var system = Focused(Lenses.AtInfinity(10).Surface(r, 0.01, "N-BK7", stop: true).Surface(-r, 0.0).Image(),
            Catalogs.Library);
        var model = new ThermalModel(system, Catalogs.Library, new Mount(Aluminum));
        double f = Lenses.Paraxial(model.At(20.0)).Efl;
        double slope = Lenses.Defocus(model.At(20.5)) - Lenses.Defocus(model.At(19.5));
        double xf = ThermoOptic.At(m, model.VacuumWavelengthsUm[0], 20.0).OptothermalExpansion;
        double expected = f * (xf - Aluminum.Cte);
        _out.WriteLine($"x_f {xf * 1e6:F4}e-6/K; defocus {slope * 1e3:F6} µm/K against f(x_f − α_h) {expected * 1e3:F6} µm/K");
        Assert.InRange(slope / expected - 1.0, -1e-3, 1e-3);
    }

    /// <summary>A thick singlet in air against its closed form: the lensmaker's formula with the grown radii and thickness, and the glass's index at the temperature.</summary>
    [Theory]
    [InlineData(-40.0)]
    [InlineData(60.0)]
    public void A_thick_singlet_matches_its_closed_form(double t)
    {
        var system = Focused(Lenses.AtInfinity(12).Surface(50.0, 8.0, "N-SF6", stop: true).Surface(-80.0, 0.0).Image(),
            Catalogs.Library);
        double image0 = system.Surfaces[2].Thickness;
        var model = new ThermalModel(system, Catalogs.Library, new Mount(Titanium));
        var lens = model.At(t);

        var glass = Catalogs.Find("SCHOTT:N-SF6");
        var air = AirConditions.Standard with { TemperatureC = t };
        double n = glass.IndexAtVacuumWavelength(model.VacuumWavelengthsUm[0], air, t).Relative;
        double s = 1.0 + glass.Expansion.Cte * (t - 20.0);
        double c1 = 1.0 / (50.0 * s), c2 = 1.0 / (-80.0 * s), th = 8.0 * s;
        double power = (n - 1.0) * (c1 - c2) + (n - 1.0) * (n - 1.0) * th * c1 * c2 / n;
        double bfl = (1.0 - (n - 1.0) * th * c1 / n) / power;
        double defocus = bfl - image0 * (1.0 + Titanium.Cte * (t - 20.0));

        Assert.Equal(n, lens.PrimaryIndices[1], 14);
        Assert.Equal(1.0 / power, Lenses.Paraxial(lens).Efl, 9);
        Assert.Equal(defocus, Lenses.Defocus(lens), 9);
    }

    /// <summary>
    /// Shi et al. (2020), eqs. (12)-(17): a spacer touching two surfaces at height H holds them L = D + z₂(H) − z₁(H)
    /// apart; at the temperature L and H grow with the spacer, the surfaces with their glasses, and the air space
    /// becomes D' = L' + z₁'(H') − z₂'(H'). At H = 0 it is the vertex distance grown with the spacer.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(8.0)]
    public void A_spacer_touching_the_glass_off_the_axis(double h)
    {
        var system = Lenses.AtInfinity(16)
            .Surface(70.0, 5.0, "N-BK7", stop: true).Surface(-60.0, 4.0)
            .Surface(-40.0, 3.0, "N-SF6").Surface(-90.0, 80.0)
            .Image();
        var mount = new Mount(Titanium, new[] { MountSegment.Spacer(2, Aluminum, h) });
        var model = new ThermalModel(system, Catalogs.Library, mount);
        const double t = 75.0, dT = t - 20.0;
        var lens = model.At(t);

        static double Sag(double c, double y) => c * y * y / (1.0 + Math.Sqrt(1.0 - c * c * y * y));
        double s1 = 1.0 + Catalogs.Find("SCHOTT:N-BK7").Expansion.Cte * dT;
        double s2 = 1.0 + Catalogs.Find("SCHOTT:N-SF6").Expansion.Cte * dT;
        double sm = 1.0 + Aluminum.Cte * dT;
        double c1 = 1.0 / -60.0, c2 = 1.0 / -40.0;
        double length = 4.0 + Sag(c2, h) - Sag(c1, h);
        double expected = length * sm + Sag(c1 / s1, h * sm) - Sag(c2 / s2, h * sm);

        Assert.Equal(expected, lens.System.Surfaces[2].Thickness, 12);
        if (h == 0.0) Assert.Equal(4.0 * sm, lens.System.Surfaces[2].Thickness, 12);
        _out.WriteLine($"H = {h}: D' − D = {(lens.System.Surfaces[2].Thickness - 4.0) * 1e3:F4} µm; at the vertex {4.0 * Aluminum.Cte * dT * 1e3:F4} µm");
    }

    /// <summary>
    /// Liu et al. (2025), eq. (5): with each element seated in its own section of the barrel, element j moves by
    /// Σᵢ≤ⱼ αᵢ·lᵢ·ΔT, the sections' growths added along the barrel, and so does the image plane the last section holds.
    /// </summary>
    [Fact]
    public void A_chain_of_barrel_sections_moves_each_element_by_their_summed_growth()
    {
        var system = Lenses.AtInfinity(10)
            .Surface(80.0, 4.0, "N-BK7", stop: true).Surface(-200.0, 10.0)
            .Surface(-60.0, 2.5, "N-SF6").Surface(300.0, 12.0)
            .Surface(55.0, 4.0, "N-BK7").Surface(-150.0, 40.0)
            .Image();
        var sections = new[] { (From: 1, To: 3, Material: Aluminum), (From: 3, To: 5, Material: Titanium), (From: 5, To: 7, Material: Invar) };
        var mount = new Mount(Aluminum, sections.Select(p => new MountSegment(p.From, p.To, p.Material)));
        var model = new ThermalModel(system, Catalogs.Library, mount);
        const double dT = -70.0;
        var lens = model.At(20.0 + dT);

        var z0 = Lenses.Vertices(system);
        var z = Lenses.Vertices(lens.System);
        double moved = 0.0;
        foreach (var p in sections)
        {
            moved += p.Material.Cte * (z0[p.To] - z0[p.From]) * dT;
            Assert.Equal(moved, z[p.To] - z0[p.To], 12);
        }
    }

    /// <summary>The stop grows with its surface, so the marginal ray fills the grown stop at every temperature.</summary>
    [Fact]
    public void The_stop_grows_with_its_surface()
    {
        var system = Focused(Lenses.AtInfinity(12)
            .Surface(40.0, 4.0, "N-BK7").Surface(-200.0, 6.0)
            .Surface(-35.0, 2.0, "N-SF6", stop: true).Surface(60.0, 5.0)
            .Surface(90.0, 4.0, "N-BK7").Surface(-45.0, 0.0)
            .Image(), Catalogs.Library);
        var model = new ThermalModel(system, Catalogs.Library, new Mount(Aluminum));
        double y0 = Lenses.Paraxial(model.At(20.0)).Y[3];
        var hot = model.At(85.0);
        double s = 1.0 + Catalogs.Find("SCHOTT:N-SF6").Expansion.Cte * 65.0;
        Assert.Equal(y0 * s, Lenses.Paraxial(hot).Y[3], 12);
        Assert.Equal(hot.Surfaces[3].ShapeScale, s, 15);
    }

    [Fact]
    public void An_even_asphere_grows_into_the_same_shape()
    {
        var surface = new Surface { Curvature = 1 / 30.0, Conic = -0.8 };
        double[] a = { 1e-4, -3e-6, 2e-9, -5e-12, 7e-15 };
        Array.Copy(a, surface.AsphericCoefficients, a.Length);
        var nominal = new Surface { Curvature = surface.Curvature, Conic = surface.Conic };
        Array.Copy(a, nominal.AsphericCoefficients, a.Length);
        const double s = 1.0 + 23e-6 * 90.0;
        ThermalModel.Expand(surface, s);
        foreach (double r in new[] { 2.0, 7.5, 12.0 })
            Assert.Equal(s * nominal.Sag(r), surface.Sag(r * s), 13);
    }

    [Fact]
    public void A_mount_that_cannot_be_built_is_refused()
    {
        var system = Lenses.AtInfinity(10)
            .Surface(80.0, 4.0, "N-BK7", stop: true).Surface(-200.0, 10.0)
            .Surface(-60.0, 2.5, "N-SF6").Surface(300.0, 12.0)
            .Image();
        Assert.Throws<ArgumentException>(() => new ThermalModel(system, Catalogs.Library,
            new Mount(Aluminum, new[] { new MountSegment(1, 3, Titanium), new MountSegment(2, 5, Invar) })));
        Assert.Throws<ArgumentException>(() => new ThermalModel(system, Catalogs.Library,
            new Mount(Aluminum, new[] { new MountSegment(1, 3, Titanium, FreeSpace: 1) })));
        Assert.Throws<ArgumentException>(() => new ThermalModel(system, Catalogs.Library,
            new Mount(Aluminum, new[] { new MountSegment(1, 3, Titanium), new MountSegment(1, 3, Invar) })));
        system.Surfaces[3].Material = "NO-SUCH-GLASS";
        Assert.Throws<InvalidOperationException>(() => new ThermalModel(system, Catalogs.Library, new Mount(Aluminum)));
    }
}

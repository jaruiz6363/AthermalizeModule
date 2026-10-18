using Athermalize.Core.Air;
using Athermalize.Core.Analysis;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

/// <summary>Worked examples from the literature, reproduced end to end.</summary>
public class WorkedExampleTests
{
    private readonly ITestOutputHelper _out;
    public WorkedExampleTests(ITestOutputHelper output) => _out = output;

    private const double Inch = ItekCassegrain.Inch;

    /// <summary>
    /// Fischer, "Optical design for the infrared," Proc. SPIE 531, 82-120 (1985), p. 91: a germanium lens of 8 in
    /// focal length at f/2, n = 4, dn/dT = 0.000396/°C, its expansion left out, warmed by 50 °C, focuses 0.0527 in
    /// closer, 16.7 depths of focus of the nominal lens, ±2λN² at 10 µm. Fischer takes df = 0.333·f·dn, rounding 1/3; the linear
    /// rate the module gives times 50 K is 0.0528 in, and the exact change of a thin lens 0.0524 in.
    /// </summary>
    [Fact]
    public void Fischer_germanium_soak()
    {
        var ge = LinearMaterial.Constant("Ge (Fischer)", 4.0, 396e-6, 0.0, 20.0, "Fischer 1985");
        double f = 8.0 * Inch, r = 2.0 * f * 3.0;
        var system = Lenses.AtInfinity(4.0 * Inch, 10.0).Surface(r, 0.01, "GE", stop: true).Surface(-r, f).Image();
        var model = new ThermalModel(system, Catalogs.Library, new Mount(MountMaterial.OfCte(0.0)),
            options: new ThermalOptions(Substitutions: new Dictionary<string, Material> { ["GE"] = ge }));
        var report = FocusReport.Compute(model, new[] { 20.0, 70.0 });
        var warm = report.States[1];

        double exact = f * (3.0 / (3.0 + 0.0198) - 1.0);
        _out.WriteLine($"rate×50 {report.DefocusPerK * 50 / Inch:F5} in; change {warm.Change / Inch:F5} in (thin lens {exact / Inch:F5}); " +
                       $"depth ±{report.States[0].DepthOfFocus / Inch:F5} in; {Math.Abs(warm.Change / report.States[0].DepthOfFocus):F2} nominal depths");
        Assert.InRange(-report.DefocusPerK * 50 / Inch - 0.0527, 0.0, 0.00015);
        Assert.Equal(exact, warm.Change, 4);
        Assert.Equal(0.00315, report.States[0].DepthOfFocus / Inch, 5);
        Assert.InRange(Math.Abs(warm.Change / report.States[0].DepthOfFocus), 16.6, 16.8);
    }

    /// <summary>
    /// Bardazzi, "Designing a 1.7-5.3 µm athermalized lens," Proc. SPIE 7113, 71130W (2008): germanium's thermal
    /// coefficient of focal length in the 2-5 µm band is −131×10⁻⁶/K, so a germanium lens of 21.7 mm focal length
    /// defocuses optically by −0.142 mm over 50 K, its image plane held still. The paper does not say which
    /// germanium data it used. The module's, from the CHARMS measurements with germanium's expansion coefficient,
    /// over −30 to +20 °C, where the measurements hold, at 4 µm, come within 6 % of it.
    /// </summary>
    [Fact]
    public void Bardazzi_germanium_singlet()
    {
        var ge = Catalogs.Find("CHARMS:GE");
        double xf = ThermoOptic.At(ge, 4.0, -5.0).OptothermalExpansion;
        double n = ge.IndexAtVacuumWavelength(4.0, AirConditions.Standard, 20.0).Relative;
        double f = 21.7, r = 2.0 * f * (n - 1.0);
        var system = Lenses.AtInfinity(f / 2.4, 4.0 / AirIndex.Of(AirModel.Tie19, 4.0, AirConditions.Standard))
            .Surface(r, 0.01, "CHARMS:GE", stop: true).Surface(-r, f).Image();
        var model = new ThermalModel(system, Catalogs.Library, new Mount(MountMaterial.OfCte(0.0)));
        var report = FocusReport.Compute(model, new[] { -30.0, 20.0 });
        double od = report.States[1].Defocus - report.States[0].Defocus;
        _out.WriteLine($"x_f {xf * 1e6:F1}e-6/K against −131; optical defocus over 50 K {od:F4} mm against −0.142");
        Assert.InRange(xf / -131e-6, 0.94, 1.06);
        Assert.InRange(od / -0.142, 0.94, 1.06);
    }

    /// <summary>
    /// Itek's B4 Cassegrain (Itek Corporation for JPL, 1972, Table 2-7), soaked by ±27.5 °C from 7.5 °C in vacuum.
    /// Its first-order data, EFL 39.3046 in and back focus 5.8517 in, check the prescription.
    /// </summary>
    [Fact]
    public void Itek_cassegrain_prescription()
    {
        var system = ItekCassegrain.Lens();
        var vacuum = AirConditions.Vacuum(7.5);
        var p = Lenses.Paraxial(new ThermalModel(system, Catalogs.Library, new Mount(MountMaterial.OfCte(0.0)), vacuum).At(vacuum));
        Assert.InRange(p.Efl / Inch - 39.3046, -5e-4, 5e-4);
        Assert.InRange(p.Bfl / Inch - 5.8517, -1e-4, 1e-4);
    }

    /// <summary>
    /// Itek (1972), Appendix G: the B4 Cassegrain's defocus and the RMS change of its wavefront at 0.55 µm without
    /// refocusing, over fields 0, 0.7 and 1 equally weighted, the central obstruction 0.52, for a soak of ±27.5 °C.
    /// <list type="bullet">
    /// <item>Table G-6, the whole structure of α = 1.5×10⁻⁶/K and ULE mirrors (α ≈ 0.03×10⁻⁶/K, p. 3-13): −0.0023 and
    /// +0.0025 in; 0.142 and 0.151 waves.</item>
    /// <item>Table G-2, the mirrors alone expanding, the cell's α = 0: BK-7 mirrors +0.0111 and −0.0107 in, 0.677 and
    /// 0.654 waves; ULE mirrors +0.0001 and +0.0001 in, 0.003 and 0.007 waves. BK-7's α is taken as N-BK7's
    /// 7.1×10⁻⁶/K, which the report does not print.</item>
    /// </list>
    /// The defocus is at the best focus, by the same RMS. The module's is a linear model, so its warm and cold
    /// values are equal and opposite; the report's differ by up to 0.0004 in, and its RMS changes by up to 0.03
    /// waves, which a linear model of the same lens cannot give. Its ULE mirrors defocus by +0.0001 in both warm
    /// and cold. The warm values agree to 0.0002 in.
    /// </summary>
    [Theory]
    [InlineData("structure", 1.5e-6, 0.03e-6, false, 35.0, -0.0023, 0.142, 0.0002, 0.01)]
    [InlineData("structure", 1.5e-6, 0.03e-6, false, -20.0, 0.0025, 0.151, 0.0002, 0.01)]
    [InlineData("BK-7 mirrors", 0.0, 7.1e-6, true, 35.0, 0.0111, 0.677, 0.0002, 0.04)]
    [InlineData("BK-7 mirrors", 0.0, 7.1e-6, true, -20.0, -0.0107, 0.654, 0.0005, 0.06)]
    [InlineData("ULE mirrors", 0.0, 0.03e-6, true, 35.0, 0.0001, 0.003, 0.0001, 0.005)]
    [InlineData("ULE mirrors", 0.0, 0.03e-6, true, -20.0, 0.0001, 0.007, 0.0002, 0.005)]
    public void Itek_cassegrain_soak(string name, double structure, double mirrors, bool mirrorsOnly, double t,
        double defocusIn, double rmsWaves, double defocusTolerance, double rmsTolerance)
    {
        var system = ItekCassegrain.Lens();
        var vacuum = AirConditions.Vacuum(7.5);
        var substrate = new MountMaterial("mirror", mirrors);
        var mount = new Mount(MountMaterial.OfCte(structure),
            mirrorSubstrates: new Dictionary<int, MountMaterial> { [3] = substrate, [4] = substrate });
        var model = new ThermalModel(system, Catalogs.Library, mount, vacuum);
        Func<ThermalPart, bool>? only = mirrorsOnly ? p => p.Kind == ThermalPartKind.Mirror : null;

        var options = new QualityOptions { Polychromatic = false, Obscuration = ItekCassegrain.Obscuration };
        var nominal = ImageQuality.Model(model.At(vacuum, only));
        var lens = ImageQuality.Model(model.At(AirConditions.Vacuum(t), only));
        double paraxial = FocusReport.Defocus(model.At(AirConditions.Vacuum(t), only)) - FocusReport.Defocus(model.At(vacuum, only));
        double best0 = ImageQuality.BestFocus(nominal, 0.0, 0.1, options).Shift;
        double best = ImageQuality.BestFocus(lens, best0 + paraxial, 0.1, options).Shift - best0;
        double rms = ImageQuality.RmsChange(nominal, lens, 0.0, options);
        _out.WriteLine($"{name} at {t} °C: best focus {best / Inch:F5} in (paraxial {paraxial / Inch:F5}) against {defocusIn}; RMS change {rms:F3} against {rmsWaves}");
        Assert.InRange(best / Inch - defocusIn, -defocusTolerance, defocusTolerance);
        Assert.InRange(rms - rmsWaves, -rmsTolerance, rmsTolerance);
    }
}

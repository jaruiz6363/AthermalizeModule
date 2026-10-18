using AberrationCalculator.Core.Glass;
using Athermalize.Core.Air;
using Athermalize.Core.Materials;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

public class Tie19MaterialTests
{
    private readonly ITestOutputHelper _out;
    public Tie19MaterialTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// At its reference temperature, in air at that temperature and one atmosphere, every catalog glass has its
    /// catalog index exactly, whatever its thermal line says.
    /// </summary>
    [Fact]
    public void Every_glass_has_its_catalog_index_at_the_reference_conditions()
    {
        int checkedGlasses = 0;
        foreach (var m in Catalogs.Library.CatalogGlasses())
        {
            var glass = Catalogs.Library.Glasses.Find(m.Name)!;
            double tRef = m.Thermal.ReferenceTemperatureC;
            foreach (double l in new[] { 0.4861327, 0.5875618, 0.6562725 })
            {
                double expected = glass.IndexAt(l);
                if (double.IsNaN(expected)) continue;
                double n = m.Index(l, new AirConditions(tRef), tRef).Relative;
                Assert.True(Math.Abs(n - expected) < 1e-12, $"{m.Name} at {l} µm: {n} against {expected}");
            }
            checkedGlasses++;
        }
        Assert.True(checkedGlasses > 1800, $"{checkedGlasses} glasses");
    }

    /// <summary>
    /// TIE-19 eqs. (3) to (9), written out again here from the published equations, against the module for
    /// glasses of all kinds over −100 to +140 °C, vacuum to two atmospheres, with the default conventions.
    /// </summary>
    [Fact]
    public void Matches_TIE19_written_out()
    {
        double worst = 0;
        foreach (var name in new[] { "SCHOTT:N-BK7", "SCHOTT:N-SF6", "SCHOTT:N-FK51A", "SCHOTT:SF57", "SCHOTT:N-LASF9",
                     "OHARA:S-BSL7", "HOYA:NBFD27", "HOYA:FCD1", "CDGM:H-K9L" })
        {
            var m = (Tie19Material)Catalogs.Find(name);
            var glass = Catalogs.Library.Glasses.Find(name)!;
            foreach (double t in new[] { -100.0, -40, 0, 20, 45, 80, 140 })
                foreach (double p in new[] { 0.0, 0.5, 1.0, 2.0 })
                    foreach (double l in new[] { 0.4, 0.55, 0.7, 1.0 })
                    {
                        double expected = WrittenOut(glass, m.Thermal, l, t, p);
                        double n = m.Index(l, AirConditions.AtAtmospheres(t, p)).Relative;
                        worst = Math.Max(worst, Math.Abs(n - expected));
                    }
        }
        _out.WriteLine($"worst difference {worst:E2}");
        Assert.True(worst < 1e-13, $"worst {worst:E2}");
    }

    /// <summary>TIE-19's procedure step by step, for a wavelength λ in µm in air at T °C and P atm, the glass at T.</summary>
    private static double WrittenOut(GlassData glass, ThermalData td, double lambda, double T, double P)
    {
        static double Air(double l, double t, double p)
        {
            double s = 1e-8 * (6432.8 + 2949810 * l * l / (146 * l * l - 1) + 25540 * l * l / (41 * l * l - 1));
            return 1 + s * p / (1 + 3.4785e-3 * (t - 15));
        }

        double t0 = td.ReferenceTemperatureC;
        double nSys = Air(lambda, T, P), nRef = Air(lambda, t0, 1);
        double lRef = lambda * nSys / nRef;
        double nRel = glass.IndexAt(lRef);
        double dT = T - t0;
        double sign = td.LambdaTkUm < 0 ? -1 : 1;
        double dn = (nRel * nRel - 1) / (2 * nRel)
                  * (td.D0 * dT + td.D1 * dT * dT + td.D2 * dT * dT * dT
                     + (td.E0 * dT + td.E1 * dT * dT) / (lRef * lRef - sign * td.LambdaTkUm * td.LambdaTkUm));
        return (nRel * nRef + dn) / nSys;
    }

    /// <summary>In vacuum the relative index is the absolute index.</summary>
    [Fact]
    public void Vacuum()
    {
        var r = Catalogs.Find("SCHOTT:N-BK7").Index(0.55, AirConditions.Vacuum(60));
        Assert.Equal(1.0, r.Air);
        Assert.Equal(r.Absolute, r.Relative);
        Assert.Equal(0.55, r.VacuumWavelengthUm);
    }

    /// <summary>
    /// The prefactor switch scales the thermal change by the ratio of the two factors (n² − 1)/(2n),
    /// with the relative and the absolute index at the reference conditions.
    /// </summary>
    [Fact]
    public void Prefactor_switch()
    {
        var m = Catalogs.Find("SCHOTT:N-LASF9");
        var hot = AirConditions.AtAtmospheres(80, 1);
        var relative = m.Index(0.5, hot);
        var absolute = m.Index(0.5, hot, new IndexOptions(Prefactor: Prefactor.Absolute));
        var noChange = m.Index(0.5, hot, 20.0);    // the glass at its reference temperature: no thermal change

        double nRef = AirIndex.Tie19(0.5, 20, 1);
        double nRel0 = noChange.Absolute / nRef, nAbs0 = noChange.Absolute;
        double f(double n) => (n * n - 1) / (2 * n);
        double dRel = relative.Absolute - nAbs0, dAbs = absolute.Absolute - nAbs0;
        Assert.Equal(f(nAbs0) / f(nRel0), dAbs / dRel, 9);
    }

    /// <summary>The AGF sign rule changes only glasses with a negative λtk.</summary>
    [Fact]
    public void LambdaTk_sign_rule()
    {
        var ignore = new IndexOptions(LambdaTkSign: LambdaTkSign.Ignore);
        var hot = AirConditions.AtAtmospheres(60, 1);
        var positive = Catalogs.Find("SCHOTT:N-BK7");
        Assert.Equal(positive.Index(0.45, hot).Relative, positive.Index(0.45, hot, ignore).Relative);
        var negative = Catalogs.Find("HOYA:FCD1");
        Assert.True(((Tie19Material)negative).Thermal.LambdaTkUm < 0);
        Assert.NotEqual(negative.Index(0.45, hot).Relative, negative.Index(0.45, hot, ignore).Relative);
    }

    /// <summary>
    /// Evaluating the air at the vacuum wavelength rather than the input one moves a glass's index by under
    /// 10⁻⁸ from −40 to +80 °C at up to one atmosphere, and by up to 5×10⁻⁸ at two. The wavelength in the E term
    /// moves it more, about 10⁻⁶ at 0.4 µm and −40 °C, since the E term grows steeply toward λtk.
    /// </summary>
    [Fact]
    public void Wavelength_conventions_are_small()
    {
        var airVac = new IndexOptions(AirWavelength: AirWavelength.Vacuum);
        var eVac = new IndexOptions(ThermalWavelength: ThermalWavelength.Vacuum);
        double worstAir = 0, worstAirToOneAtm = 0, worstE = 0;
        foreach (var m in new[] { "SCHOTT:N-BK7", "SCHOTT:SF57", "SCHOTT:N-LASF9" }.Select(Catalogs.Find))
            foreach (double t in new[] { -40.0, 20, 80 })
                foreach (double p in new[] { 0.5, 1, 2 })
                    foreach (double l in new[] { 0.4, 0.55, 0.8 })
                    {
                        var air = AirConditions.AtAtmospheres(t, p);
                        double n = m.Index(l, air).Relative;
                        double d = Math.Abs(m.Index(l, air, airVac).Relative - n);
                        worstAir = Math.Max(worstAir, d);
                        if (p <= 1) worstAirToOneAtm = Math.Max(worstAirToOneAtm, d);
                        worstE = Math.Max(worstE, Math.Abs(m.Index(l, air, eVac).Relative - n));
                    }
        _out.WriteLine($"air at the vacuum wavelength: {worstAirToOneAtm:E2} up to 1 atm, {worstAir:E2} up to 2; E term at the vacuum wavelength: {worstE:E2}");
        Assert.True(worstAirToOneAtm < 1e-8, $"{worstAirToOneAtm:E2}");
        Assert.True(worstAir < 6e-8, $"{worstAir:E2}");
        Assert.True(worstE > 1e-7 && worstE < 2e-6, $"{worstE:E2}");
    }

    /// <summary>
    /// A glass without thermal coefficients, and a model glass, change only with the air:
    /// n = n_catalog(λ_ref)·n_ref/n_sys.
    /// </summary>
    [Theory]
    [InlineData("SCHOTT:SF66")]
    [InlineData("SCHOTT:P-SK57Q1")]
    [InlineData("model")]
    public void Without_thermal_data_only_the_air_changes(string name)
    {
        Material m = name == "model" ? Tie19Material.Model(1.6, 40, 0.002) : Catalogs.Find(name);
        Func<double, double> catalog = name == "model"
            ? l => IndexResolver.ModelIndex(1.6, 40, 0.002, l)
            : Catalogs.Library.Glasses.Find(name)!.IndexAt;
        foreach (var (t, p) in new[] { (30.0, 1.0), (-20.0, 0.8), (60.0, 2.0) })
        {
            double l = 0.55;
            double nSys = AirIndex.Tie19(l, t, p), nRef = AirIndex.Tie19(l, 20, 1);
            double expected = catalog(l * nSys / nRef) * nRef / nSys;
            Assert.Equal(expected, m.Index(l, AirConditions.AtAtmospheres(t, p)).Relative, 14);
        }
        Assert.Equal(catalog(0.55), m.Index(0.55, AirConditions.Standard).Relative, 14);
    }

    [Fact]
    public void Reports_the_range()
    {
        var m = Catalogs.Find("SCHOTT:N-BK7");
        Assert.True(m.Index(0.55, AirConditions.Standard).InRange);
        Assert.False(m.Index(0.55, AirConditions.Standard, 150).InRange);
        Assert.False(m.Index(3.0, AirConditions.Standard).InRange);
    }
}

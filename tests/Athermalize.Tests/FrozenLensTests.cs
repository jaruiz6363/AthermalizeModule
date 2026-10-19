using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Athermalize.Core.Air;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

/// <summary>
/// The frozen lens: written with its glass catalog and read back by a reader that knows nothing of temperature, it
/// is the module's lens at the temperature: the same geometry, the same relative indices, the same focus.
/// </summary>
public class FrozenLensTests
{
    private readonly ITestOutputHelper _out;
    public FrozenLensTests(ITestOutputHelper output) => _out = output;

    private static string Folder()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"atherm-frozen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// The doublet designed in air at 20 °C, written at −40 °C in vacuum, read back with only its own catalog: each
    /// radius and thickness, each relative index at each wavelength to 10⁻⁷, and the paraxial focus are the module's,
    /// in each format whose glasses are named from a catalog beside it.
    /// </summary>
    [Theory]
    [InlineData(".zmx")]
    [InlineData(".lhlt")]
    public void A_frozen_lens_reads_back_as_the_lens_at_its_temperature(string format)
    {
        string dir = Folder();
        try
        {
            var system = LensFile.Read(Path.Combine(AppContext.BaseDirectory, "examples", "doublet.zmx"), Catalogs.Library.Glasses);
            var model = new ThermalModel(system, Catalogs.Library, new Mount(MountMaterials.Find("aluminum-6061-t6")!));
            var air = AirConditions.Vacuum(-40.0);
            var frozen = FrozenLens.Of(model, air);
            var lens = frozen.Lens;
            Assert.Equal(new[] { "N-BK7_M40C_VAC", "N-SF6_M40C_VAC" }, frozen.Materials.Select(m => m.Name));

            string path = Path.Combine(dir, "doublet_cold" + format);
            var (code, text) = CliRun("lens", Path.Combine(AppContext.BaseDirectory, "examples", "doublet.zmx"),
                "--housing", "aluminum-6061-t6", "--temperatures", "-40", "--at-pressure", "0", "--write", path, "--catalogs", Catalogs.Folder);
            Assert.Equal(0, code);
            Assert.Contains("Open it at the program's reference conditions", text);

            var catalog = new GlassCatalog();
            catalog.LoadFile(Path.Combine(dir, "doublet_cold.agf"));
            var read = LensFile.Read(path, catalog);
            Assert.Equal(lens.System.Surfaces.Count, read.Surfaces.Count);
            for (int i = 1; i < read.Surfaces.Count; i++)
            {
                Assert.Equal(lens.System.Surfaces[i].Curvature, read.Surfaces[i].Curvature, 12);
                Assert.Equal(lens.System.Surfaces[i].Thickness, read.Surfaces[i].Thickness, 9);
            }
            for (int w = 0; w < read.Wavelengths.Count; w++)
            {
                Assert.Equal(lens.System.Wavelengths[w].Value, read.Wavelengths[w].Value, 9);
                var n = new double[read.Surfaces.Count];
                for (int i = 0; i < n.Length; i++)
                    n[i] = catalog.Find(read.Surfaces[i].Material)?.IndexAt(read.Wavelengths[w].Value) ?? 1.0;
                for (int i = 1; i < 3; i++) Assert.InRange(n[i] - lens.Indices[w][i], -1e-7, 1e-7);
                double focus = ParaxialTrace.Trace(read, n, 0.0).ParaxialFocusDistance;
                double module = ParaxialTrace.Trace(lens.System, lens.Indices[w], 0.0).ParaxialFocusDistance;
                _out.WriteLine($"λ {read.Wavelengths[w].Value:F6}: back focus {focus:F6} against {module:F6} mm");
                Assert.InRange(focus - module, -1e-4, 1e-4);   // 0.1 µm
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// A program that applies TIE-19 to the frozen glasses at its reference conditions, 20 °C and 1 atm, leaves them
    /// alone, since they carry no thermal data: the module reading the frozen lens there finds the same indices.
    /// </summary>
    [Fact]
    public void Read_at_its_reference_conditions_a_frozen_lens_is_unchanged()
    {
        string dir = Folder();
        try
        {
            var system = LensFile.Read(Path.Combine(AppContext.BaseDirectory, "examples", "doublet.zmx"), Catalogs.Library.Glasses);
            var model = new ThermalModel(system, Catalogs.Library, new Mount(MountMaterials.Find("aluminum-6061-t6")!));
            var frozen = FrozenLens.Of(model, AirConditions.Vacuum(-40.0));
            string path = Path.Combine(dir, "cold.zmx");
            Assert.Equal(0, CliRun("lens", Path.Combine(AppContext.BaseDirectory, "examples", "doublet.zmx"),
                "--housing", "aluminum-6061-t6", "--temperatures", "-40", "--at-pressure", "0", "--write", path, "--catalogs", Catalogs.Folder).Code);

            var library = MaterialLibrary.FromFolder(dir);
            var reread = new ThermalModel(LensFile.Read(path, library.Glasses), library, new Mount(MountMaterial.OfCte(0.0))).At(20.0);
            for (int w = 0; w < reread.Indices.Count; w++)
                for (int i = 1; i < 3; i++)
                    Assert.InRange(reread.Indices[w][i] - frozen.Lens.Indices[w][i], -1e-7, 1e-7);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// In the infrared the Schott formula, made for the visible, misses ZnSe over 7-13 µm by 4×10⁻⁶; the Extended 2
    /// formula's λ⁴ and λ⁶ terms fit it within 10⁻⁷. Germanium fits the Schott formula.
    /// </summary>
    [Fact]
    public void Infrared_materials_fit_within_1e_7()
    {
        var system = LensFile.Read(Path.Combine(AppContext.BaseDirectory, "examples", "fischer-ge-znse.zmx"), Catalogs.Library.Glasses);
        var model = new ThermalModel(system, Catalogs.Library, new Mount(MountMaterials.Find("aluminum-6061-t6")!));
        var frozen = FrozenLens.Of(model, AirConditions.Standard with { TemperatureC = 60.0 });
        foreach (var m in frozen.Materials)
        {
            var (glass, max) = m.Fit();
            _out.WriteLine($"{m.Name}: {glass.Formula}, within {max:E1}");
            Assert.InRange(max, 0.0, 1e-7);
        }
        Assert.Equal(DispersionFormula.Schott, frozen.Materials[0].Fit().Glass.Formula);
        Assert.Equal(DispersionFormula.Extended2, frozen.Materials[1].Fit().Glass.Formula);
    }

    [Fact]
    public void Write_needs_one_temperature_and_a_format_it_can_carry_glasses_in()
    {
        string lens = Path.Combine(AppContext.BaseDirectory, "examples", "doublet.zmx");
        Assert.Equal(1, CliRun("lens", lens, "--housing", "23.6", "--temperatures", "-40,60", "--write", "x.zmx", "--catalogs", Catalogs.Folder).Code);
        Assert.Equal(1, CliRun("lens", lens, "--housing", "23.6", "--temperatures", "-40", "--write", "x.seq", "--catalogs", Catalogs.Folder).Code);
        Assert.Equal(1, CliRun("focus", lens, "--housing", "23.6", "--temperatures", "-40", "--write", "x.zmx", "--catalogs", Catalogs.Folder).Code);
    }

    private static (int Code, string Text) CliRun(params string[] args)
    {
        var w = new StringWriter();
        int code = Athermalize.Cli.Program.Run(args, w);
        return (code, w.ToString());
    }
}

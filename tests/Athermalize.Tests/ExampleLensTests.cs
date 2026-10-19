using AberrationCalculator.Core.IO;
using Athermalize.Core.Air;
using Athermalize.Core.Analysis;
using Athermalize.Core.Lens;
using Xunit;

namespace Athermalize.Tests;

/// <summary>
/// The user guide's two lenses from the literature, as the files in <c>examples</c> hold them with their mount files:
/// each matches its published first-order data, and the Cassegrain its published defocus.
/// </summary>
public class ExampleLensTests
{
    private const double Inch = ItekCassegrain.Inch;

    private static string Example(string name) => Path.Combine(AppContext.BaseDirectory, "examples", name);

    /// <summary>The lens file and its mount file, as atherm reads them.</summary>
    private static (ThermalModel Model, AirConditions Air) Load(string lens)
    {
        var system = LensFile.Read(Example(lens), Catalogs.Library.Glasses);
        var file = MountFile.Read(MountFile.PathFor(Example(lens)));
        var air = AirConditions.AtAtmospheres(file.NominalTemperatureC ?? 20.0, file.PressureAtm ?? 1.0);
        return (new ThermalModel(system, Catalogs.Library, file.ToMount()!, air), air);
    }

    /// <summary>
    /// Itek's B4 Cassegrain (Itek 1972, Table 2-7): EFL 39.3046 in and back focus 5.8517 in, in vacuum at 7.5 °C.
    /// Its mount file is Table G-6's case, so warmed by 27.5 °C its paraxial focus moves the report's −0.0023 in.
    /// </summary>
    [Fact]
    public void The_Cassegrain_is_Iteks()
    {
        var (model, air) = Load("itek-cassegrain.zmx");
        Assert.Equal(0.0, air.PressurePa);
        var p = Lenses.Paraxial(model.At(air));
        Assert.InRange(p.Efl / Inch - 39.3046, -5e-4, 5e-4);
        Assert.InRange(p.Bfl / Inch - 5.8517, -1e-4, 1e-4);
        var report = FocusReport.Compute(model, new[] { 7.5, 35.0 });
        Assert.InRange(report.States[1].Change / Inch - -0.0023, -2e-4, 2e-4);
    }

    /// <summary>
    /// Fischer's (1985) Ge/ZnSe doublet for 8-12 µm: EFL 2.000 in at f/2. In aluminum, nearly all its drift is the
    /// germanium's, f·x_f of the germanium alone.
    /// </summary>
    [Fact]
    public void The_infrared_doublet_is_Fischers()
    {
        var (model, air) = Load("fischer-ge-znse.zmx");
        var p = Lenses.Paraxial(model.At(air));
        Assert.InRange(p.Efl / Inch - 2.000, -1e-3, 1e-3);
        Assert.InRange(p.Bfl / Inch - 1.8234, -1e-3, 1e-3);
        var report = FocusReport.Compute(model, new[] { 20.0, 60.0 });
        Assert.InRange(report.DefocusPerK * 1e3, -8.0, -7.7);
    }
}

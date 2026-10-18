using System.Globalization;
using System.Text;
using Athermalize.Core.Materials;
using Xunit;

namespace Athermalize.Tests;

public class ThermalCatalogTests
{
    private static ThermalCatalog Read(params string[] lines)
    {
        var c = new ThermalCatalog();
        c.Load("TEST", lines);
        return c;
    }

    [Fact]
    public void Reads_every_field()
    {
        var c = Read(
            "NM G1 2 517642 1.5168 64.17 0 1",
            "ED 7.1 8.3 2.51 -0.0009 0",
            "CD 1.03961212 6.00069867E-03 0.231792344 2.00179144E-02 1.01046945 103.560653",
            "TD 1.86E-06 1.31E-08 -1.37E-11 4.34E-07 6.27E-10 0.17 20");
        var t = c.Thermal("TEST", "G1");
        Assert.Equal(DataState.Present, t.State);
        Assert.Equal(new[] { 1.86e-6, 1.31e-8, -1.37e-11, 4.34e-7, 6.27e-10, 0.17, 20 },
            new[] { t.D0, t.D1, t.D2, t.E0, t.E1, t.LambdaTkUm, t.ReferenceTemperatureC });
        var e = c.Expansion("TEST", "G1");
        Assert.Equal(DataState.Present, e.State);
        Assert.Equal(7.1e-6, e.Cte, 15);
        Assert.Equal(8.3e-6, e.CteHigh, 15);
        Assert.False(e.IgnoreExpansion);
    }

    /// <summary>Doubled spaces and tabs separate fields like a single space; they never shift them.</summary>
    [Fact]
    public void Doubled_spaces_and_tabs_do_not_shift_fields()
    {
        var c = Read("NM  G1   2 0 1.5 60", "TD\t1E-06  2E-08\t\t3E-11 4E-07 5E-10   0.2\t25  ");
        var t = c.Thermal("TEST", "G1");
        Assert.Equal(DataState.Present, t.State);
        Assert.Equal(1e-6, t.D0);
        Assert.Equal(0.2, t.LambdaTkUm);
        Assert.Equal(25, t.ReferenceTemperatureC);
    }

    /// <summary>A missing, empty or short TD line never puts the reference temperature at 0 °C.</summary>
    [Fact]
    public void Missing_empty_and_short_lines_keep_20C()
    {
        var c = Read("NM MISSING 2", "NM EMPTY 2", "TD", "NM SHORT 2", "TD 1E-06 0 0 0 0 0.2");
        Assert.Equal(DataState.Missing, c.Thermal("TEST", "MISSING").State);
        Assert.Equal(DataState.Empty, c.Thermal("TEST", "EMPTY").State);
        Assert.Equal(DataState.Incomplete, c.Thermal("TEST", "SHORT").State);
        Assert.Equal(1e-6, c.Thermal("TEST", "SHORT").D0);
        foreach (var name in new[] { "MISSING", "EMPTY", "SHORT" })
            Assert.Equal(20.0, c.Thermal("TEST", name).ReferenceTemperatureC);
        Assert.Equal(DataState.Missing, c.Thermal("TEST", "NOT_THERE").State);
    }

    /// <summary>Numbers are read the same whatever the computer's number format.</summary>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("sv-SE")]
    public void Culture_does_not_change_numbers(string culture)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            var t = Read("NM G1 2", "TD 1.86E-06 1.31E-08 -1.37E-11 4.34E-07 6.27E-10 0.170 20.5").Thermal("TEST", "G1");
            Assert.Equal(1.86e-6, t.D0);
            Assert.Equal(0.17, t.LambdaTkUm);
            Assert.Equal(20.5, t.ReferenceTemperatureC);
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void Reads_UTF16_files()
    {
        string path = Path.Combine(Path.GetTempPath(), $"utf16-{Guid.NewGuid():N}.agf");
        try
        {
            File.WriteAllText(path, "NM G1 2\r\nTD 1E-06 0 0 0 0 0.2 20\r\n", Encoding.Unicode);
            var c = new ThermalCatalog();
            c.LoadFile(path);
            string catalog = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
            Assert.Equal(DataState.Present, c.Thermal(catalog, "G1").State);
            Assert.Equal(1e-6, c.Thermal(catalog, "G1").D0);
        }
        finally { File.Delete(path); }
    }

    /// <summary>The shipped catalogs: the glasses whose thermal lines are known to be missing, empty or zero.</summary>
    [Fact]
    public void Shipped_catalogs()
    {
        var t = Catalogs.Library.Thermal;
        Assert.Equal(DataState.Missing, t.Thermal("SCHOTT", "SF66").State);
        foreach (var name in new[] { "P-SK57Q1", "LF5G15", "REALVIEW_2.0" })
            Assert.Equal(DataState.Empty, t.Thermal("SCHOTT", name).State);
        Assert.Equal(DataState.Present, t.Thermal("CORNING_FS", "HPFS_7980").State);
        Assert.False(t.Thermal("CORNING_FS", "HPFS_7980").HasCoefficients);
        Assert.Equal(DataState.Present, t.Thermal("HOYA", "FDS20-W").State);
        Assert.Equal(DataState.Present, t.Thermal("CDGM", "H-K9L").State);
    }
}

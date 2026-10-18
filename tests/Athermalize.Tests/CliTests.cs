using AberrationCalculator.Core.IO;
using Athermalize.Cli;
using Xunit;

namespace Athermalize.Tests;

public class CliTests
{
    private static (int Code, string Text) Run(params string[] args)
    {
        var w = new StringWriter();
        int code = Program.Run(args, w);
        return (code, w.ToString());
    }

    [Fact]
    public void Materials_prints_each_glass()
    {
        var (code, text) = Run("materials", "SCHOTT:N-BK7", "SCHOTT:SF66", "--catalogs", Catalogs.Folder,
            "--temperatures", "20", "--wavelengths", "0.5875618");
        Assert.Equal(0, code);
        Assert.Contains("SCHOTT:N-BK7: thermal data present, Tref 20 °C", text);
        Assert.Contains("1.5168000", text);
        Assert.Contains("SCHOTT:SF66: thermal data missing; no thermal coefficients, so only the air changes", text);
    }

    [Fact]
    public void Materials_lists_the_measured_materials()
    {
        var (code, text) = Run("materials", "--measured", "--catalogs", Catalogs.Folder);
        Assert.Equal(0, code);
        Assert.Contains("CHARMS:SI", text);
        Assert.Contains("doi:10.1117/12.672850", text);
    }

    [Fact]
    public void Unknown_material_fails()
    {
        Assert.Equal(1, Run("materials", "NO_SUCH_GLASS", "--catalogs", Catalogs.Folder).Code);
    }

    /// <summary>A doublet written to a lens file, focused at 20 °C.</summary>
    private static string DoubletFile()
    {
        var system = Lenses.AtInfinity(20)
            .Surface(61.07, 6.0, "N-BK7", stop: true, semiDiameter: 11)
            .Surface(-44.17, 2.5, "N-SF6", semiDiameter: 11)
            .Surface(-128.89, 96.0, semiDiameter: 11)
            .Image();
        system.Title = "Test doublet";
        system.GlassCatalogs.Add("SCHOTT");
        string path = Path.Combine(Path.GetTempPath(), $"atherm-doublet-{Guid.NewGuid():N}.zmx");
        LensFile.Write(system, path, Catalogs.Library.Glasses);
        return path;
    }

    [Fact]
    public void Lens_prints_the_lens_at_each_temperature()
    {
        string path = DoubletFile();
        try
        {
            var (code, text) = Run("lens", path, "--housing", "23.6", "--temperatures", "-20,20,60",
                "--spacer", "3:9.2", "--catalogs", Catalogs.Folder, "--detail");
            Assert.Equal(0, code);
            Assert.Contains("Test doublet", text);
            Assert.Contains("Mount: housing, α 23.6×10⁻⁶/K; spacer after surface 3, α 9.2×10⁻⁶/K.", text);
            Assert.Contains("SCHOTT:N-BK7", text);
            Assert.Contains("At 60 °C:", text);
            Assert.Contains("mount part 3-4", text);
            var rows = text.Split('\n').Where(l => l.TrimStart().StartsWith("20.0 ")).ToList();
            Assert.Single(rows);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Lens_needs_a_housing()
    {
        string path = DoubletFile();
        try { Assert.Equal(1, Run("lens", path, "--catalogs", Catalogs.Folder).Code); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Lens_takes_a_named_mount_material()
    {
        string path = DoubletFile();
        try
        {
            var (code, text) = Run("lens", path, "--housing", "aluminum-6061-t6", "--catalogs", Catalogs.Folder);
            Assert.Equal(0, code);
            Assert.Contains("housing, aluminum-6061-t6 (α 23.6×10⁻⁶/K)", text);
        }
        finally { File.Delete(path); }
        var (listed, list) = Run("lens", "--mount-materials");
        Assert.Equal(0, listed);
        Assert.Contains("invar-36", list);
    }

    [Fact]
    public void Focus_prints_the_first_order_report()
    {
        string path = DoubletFile();
        try
        {
            var (code, text) = Run("focus", path, "--housing", "aluminum-6061-t6", "--temperatures", "0,20,40",
                "--catalogs", Catalogs.Folder);
            Assert.Equal(0, code);
            Assert.Contains("Each part's share at 20 °C", text);
            Assert.Contains("housing (aluminum-6061-t6)", text);
            Assert.Contains("whole lens", text);
            Assert.Contains("ν_T (K)", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Image_prints_the_wavefront_and_the_MTF()
    {
        string path = DoubletFile();
        try
        {
            var (code, text) = Run("image", path, "--housing", "23.6", "--temperatures", "20,60",
                "--frequencies", "20", "--mtf-limit", "0.2", "--range", "0,60", "--catalogs", Catalogs.Folder);
            Assert.Equal(0, code);
            Assert.Contains("RMS refocused", text);
            Assert.Contains("20 c/mm", text);
            Assert.Contains("refocused:", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Map_lists_the_materials_and_plots_them()
    {
        string svg = Path.Combine(Path.GetTempPath(), $"atherm-map-{Guid.NewGuid():N}.svg");
        try
        {
            var (code, text) = Run("map", "N-BK7", "N-SF6", "--housing", "aluminum-6061-t6", "--svg", svg, "--catalogs", Catalogs.Folder);
            Assert.Equal(0, code);
            Assert.Contains("SCHOTT:N-BK7", text);
            Assert.Contains("housing aluminum-6061-t6", text);
            var doc = System.Xml.Linq.XDocument.Load(svg);
            Assert.Equal(2, doc.Descendants().Count(e => e.Name.LocalName == "circle"));
        }
        finally { File.Delete(svg); }
    }

    [Fact]
    public void Solve_gives_the_doublet_and_the_triplet()
    {
        var (code, text) = Run("solve", "N-BK7", "N-SF6", "--housing", "23.6", "--focal", "100", "--catalogs", Catalogs.Folder);
        Assert.Equal(0, code);
        Assert.Contains("Achromatic doublet: f_a =", text);
        Assert.Contains("Not athermal", text);
        (code, text) = Run("solve", "N-PK51", "N-KZFS5", "N-LAK7", "--housing", "23.6", "--catalogs", Catalogs.Folder);
        Assert.Equal(0, code);
        Assert.Contains("Athermal achromatic triplet: f =", text);
        (code, text) = Run("solve", "N-BK7", "--partners", "--catalog", "SCHOTT", "--housing", "23.6", "--top", "3", "--catalogs", Catalogs.Folder);
        Assert.Equal(0, code);
        Assert.Contains("most nearly athermal first", text);
    }

    [Fact]
    public void Select_ranks_the_combinations()
    {
        string path = DoubletFile();
        try
        {
            var (code, text) = Run("select", path, "--housing", "aluminum-6061-t6", "--near", "2:0.05:4", "--catalog", "SCHOTT",
                "--housing-candidates", "aluminum-6061-t6,ti-6al-4v", "--keep", "3", "--catalogs", Catalogs.Folder);
            Assert.Equal(0, code);
            Assert.Contains("combinations. Score", text);
            Assert.Contains("design", text);
            Assert.Contains("housing ti-6al-4v", text);
        }
        finally { File.Delete(path); }
    }
}

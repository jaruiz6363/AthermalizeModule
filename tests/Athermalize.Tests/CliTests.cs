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
            Assert.Contains("Each part's contribution at 20 °C", text);
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

    /// <summary>focus --svg and image --svg write SVG: the focus curve and the contributions, the RMS and the MTF.</summary>
    [Fact]
    public void Focus_and_image_plot()
    {
        string path = DoubletFile();
        string svg = Path.Combine(Path.GetTempPath(), $"atherm-plot-{Guid.NewGuid():N}.svg");
        try
        {
            var (code, text) = Run("focus", path, "--housing", "aluminum-6061-t6", "--temperatures", "0,20,40", "--svg", svg,
                "--catalogs", Catalogs.Folder);
            Assert.Equal(0, code);
            Assert.Contains("Plot written to", text);
            var doc = System.Xml.Linq.XDocument.Load(svg);
            Assert.Equal(3, doc.Descendants().Count(e => e.Name.LocalName == "polyline"));
            Assert.Equal(4, doc.Descendants().Count(e => e.Name.LocalName == "rect" && e.Attribute("height")?.Value == "18"));
            Assert.Contains(doc.Descendants(), e => e.Value == "housing (aluminum-6061-t6)");

            (code, text) = Run("image", path, "--housing", "23.6", "--temperatures", "20,60", "--frequencies", "20",
                "--mtf-limit", "0.2", "--monochromatic", "--plot-points", "3", "--svg", svg, "--catalogs", Catalogs.Folder);
            Assert.Equal(0, code);
            Assert.Contains("at 3 temperatures", text);   // 20, 40 and 60: the report's and the nominal among them
            doc = System.Xml.Linq.XDocument.Load(svg);
            Assert.Equal(4, doc.Descendants().Count(e => e.Name.LocalName == "polyline"));
            Assert.Contains(doc.Descendants(), e => e.Value == "limit 0.2");
        }
        finally { File.Delete(path); File.Delete(svg); }
        Assert.Equal(1, Run("lens", "x.zmx", "--housing", "1", "--svg", svg).Code);
    }

    // ── The mount file ───────────────────────────────────────────────────────────────

    /// <summary>A run with --save-mount writes the mount beside the lens; a later run without the options reads it and gives the same report.</summary>
    [Fact]
    public void A_saved_mount_is_used_by_the_next_run()
    {
        string path = DoubletFile();
        string mount = Athermalize.Core.Lens.MountFile.PathFor(path);
        try
        {
            string[] options = { "--housing", "aluminum-6061-t6", "--spacer", "3:ti-6al-4v:9", "--cte", "N-BK7=7.2",
                "--nominal", "22", "--temperatures", "-20,22,60", "--catalogs", Catalogs.Folder };
            var (code, first) = Run(new[] { "focus", path }.Concat(options).Append("--save-mount").ToArray());
            Assert.Equal(0, code);
            Assert.True(File.Exists(mount));
            Assert.Contains("Saved the mount to", first);

            var (code2, second) = Run("focus", path, "--temperatures", "-20,22,60", "--catalogs", Catalogs.Folder);
            Assert.Equal(0, code2);
            Assert.Contains("Mount file: " + mount + ".", second);
            Assert.Contains("Designed for 22 °C", second);
            string Report(string text) => string.Join("\n", text.Split('\n')
                .Where(l => !l.StartsWith("Mount file") && !l.StartsWith("Saved") && l.Trim().Length > 0));
            Assert.Equal(Report(first), Report(second));

            // The options given override the file, and say so.
            var (_, third) = Run("lens", path, "--housing", "invar-36", "--catalogs", Catalogs.Folder);
            Assert.Contains("with the options given here over it", third);
            Assert.Contains("Mount: housing, invar-36 (α 1.3×10⁻⁶/K); spacer after surface 3, ti-6al-4v", third);

            // --no-mount ignores it, so the housing is missing.
            Assert.Equal(1, Run("lens", path, "--no-mount", "--catalogs", Catalogs.Folder).Code);
        }
        finally { File.Delete(path); File.Delete(mount); }
    }

    /// <summary>select --save-mount keeps the best combination's housing, not its glasses.</summary>
    [Fact]
    public void Select_saves_the_best_mount()
    {
        string path = DoubletFile();
        string mount = Athermalize.Core.Lens.MountFile.PathFor(path);
        try
        {
            var (code, text) = Run("select", path, "--housing", "aluminum-6061-t6", "--glass", "2:N-SF6,SF6",
                "--housing-candidates", "aluminum-6061-t6,ti-6al-4v", "--keep", "2", "--catalogs", Catalogs.Folder, "--save-mount");
            Assert.Equal(0, code);
            Assert.Contains("The glasses it chose are not saved", text);
            var saved = Athermalize.Core.Lens.MountFile.Read(mount);
            Assert.Equal("ti-6al-4v", saved.Housing);
            Assert.Empty(saved.Use);
        }
        finally { File.Delete(path); File.Delete(mount); }
    }
}

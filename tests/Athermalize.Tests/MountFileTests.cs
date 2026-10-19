using Athermalize.Core.Lens;
using Athermalize.Core.Materials;
using Xunit;

namespace Athermalize.Tests;

/// <summary>The mount file: written and read back, written by hand, and refused when it can't be read.</summary>
public class MountFileTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"atherm-{Guid.NewGuid():N}.mount.json");

    [Fact]
    public void Lives_beside_the_lens()
    {
        Assert.Equal(Path.Combine("lenses", "doublet.mount.json"), MountFile.PathFor(Path.Combine("lenses", "doublet.zmx")));
        Assert.Equal("doublet.mount.json", MountFile.PathFor("doublet.seq"));
    }

    /// <summary>Every part of a mount, written and read back, is the same mount.</summary>
    [Fact]
    public void A_mount_reads_back_as_written()
    {
        var mount = new Mount(MountMaterials.Find("aluminum-6061-t6")!, new[]
        {
            MountSegment.Spacer(3, MountMaterials.Find("ti-6al-4v")!, 9.0),
            new MountSegment(1, 6, MountMaterial.OfCte(1.2e-6), 2.5, 4.0),
        }, new Dictionary<int, MountMaterial> { [4] = MountMaterials.Find("invar-36-cryo")! }, CementedSurface.PrecedingGlass);
        string path = TempFile();
        try
        {
            MountFile.From(mount, -196.0, 0.0, new Dictionary<string, string> { ["GERMANIUM"] = "JPCRD:GE" },
                new Dictionary<string, double> { ["N-BK7"] = 7.2 }).Write(path);
            var f = MountFile.Read(path);
            var back = f.ToMount()!;
            Assert.Equal(-196.0, f.NominalTemperatureC);
            Assert.Equal(0.0, f.PressureAtm);
            Assert.Same(mount.Housing, back.Housing);
            Assert.Equal(CementedSurface.PrecedingGlass, back.Cemented);
            Assert.Equal(2, back.Segments.Count);
            Assert.Equal(mount.Segments[0], back.Segments[0]);
            Assert.Equal((1, 6, 2.5, 4.0), (back.Segments[1].From, back.Segments[1].To, back.Segments[1].FromHeight, back.Segments[1].ToHeight));
            Assert.Equal(1.2e-6, back.Segments[1].Material.Cte, 15);
            Assert.Same(MountMaterials.Find("invar-36-cryo"), back.MirrorSubstrates[4]);
            Assert.Equal("JPCRD:GE", f.Use["germanium"]);
            Assert.Equal(7.2, f.Cte["N-BK7"]);
            Assert.Contains("\"cte\": {\r\n    \"N-BK7\": 7.2", File.ReadAllText(path).Replace("\r\n", "\n").Replace("\n", "\r\n"));
        }
        finally { File.Delete(path); }
    }

    /// <summary>A file written by hand: comments, a trailing comma, materials as names or numbers, fields left out.</summary>
    [Fact]
    public void A_hand_written_file_reads()
    {
        string path = TempFile();
        try
        {
            File.WriteAllText(path, """
                {
                  // the housing and one spacer
                  "housing": 22.5,
                  "spacers": [ { "after": 3, "material": "stainless-416", "height": 9 } ],
                }
                """);
            var f = MountFile.Read(path);
            var m = f.ToMount()!;
            Assert.Equal(22.5e-6, m.Housing.Cte, 15);
            Assert.Equal("stainless-416", m.Segments[0].Material.Name);
            Assert.Null(f.NominalTemperatureC);
            Assert.Equal(CementedSurface.FollowingGlass, m.Cemented);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_file_it_cannot_read_is_refused()
    {
        string path = TempFile();
        try
        {
            File.WriteAllText(path, """{ "version": 2, "housing": "aluminum-6061-t6" }""");
            Assert.Contains("format version 2", Assert.Throws<ArgumentException>(() => MountFile.Read(path)).Message);
            File.WriteAllText(path, """{ "housing": "unobtainium" }""");
            Assert.Contains("neither a number nor a mount material", Assert.Throws<ArgumentException>(() => MountFile.Read(path).ToMount()).Message);
            File.WriteAllText(path, "not json");
            Assert.Contains("not a mount file", Assert.Throws<ArgumentException>(() => MountFile.Read(path)).Message);
        }
        finally { File.Delete(path); }
    }

    /// <summary>A material with a curve of its own can't be written, since the file holds only names and numbers.</summary>
    [Fact]
    public void A_material_of_its_own_curve_is_not_written()
    {
        var own = MountMaterial.OfCurve("my-alloy", new TabulatedExpansion(new[] { 200.0, 300.0 }, new[] { 20e-6, 23e-6 }), "measured");
        Assert.Throws<ArgumentException>(() => MountFile.From(new Mount(own)));
    }

    /// <summary>The example in the user guide reads as it says.</summary>
    [Fact]
    public void The_user_guides_example_reads()
    {
        string path = TempFile();
        try
        {
            File.WriteAllText(path, """
                {
                  "format": "athermalize-mount",
                  "version": 1,
                  "nominalTemperatureC": 20,
                  "pressureAtm": 1,
                  "housing": "ti-6al-4v",
                  "spacers": [ { "after": 3, "material": "invar-36", "height": 9 } ],
                  "parts": [ { "from": 1, "to": 4, "material": 22.5, "fromHeight": 0, "toHeight": 0 } ],
                  "mirrors": { "6": "aluminum-6061-t6" },
                  "cemented": "following",
                  "use": { "GERMANIUM": "JPCRD:GE" },
                  "cte": { "N-BK7": 7.2 }
                }
                """);
            var f = MountFile.Read(path);
            var mount = f.ToMount()!;
            Assert.Equal("ti-6al-4v", mount.Housing.Name);
            Assert.Equal(2, mount.Segments.Count);
            Assert.Equal(22.5e-6, mount.Segments[1].Material.Cte, 15);
            Assert.Equal("aluminum-6061-t6", mount.MirrorSubstrates[6].Name);
            Assert.Equal("JPCRD:GE", f.Use["GERMANIUM"]);
            Assert.Equal(7.2, f.Cte["N-BK7"]);
        }
        finally { File.Delete(path); }
    }
}

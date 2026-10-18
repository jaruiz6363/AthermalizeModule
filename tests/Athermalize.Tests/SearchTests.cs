using Athermalize.Core.Analysis;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

/// <summary>The exhaustive search over glasses and mount materials.</summary>
public class SearchTests
{
    private readonly ITestOutputHelper _out;
    public SearchTests(ITestOutputHelper output) => _out = output;

    private static readonly MountMaterial Aluminum = new("aluminum", 23.6e-6, "Liu et al. 2025");
    private static readonly MountMaterial Titanium = new("titanium", 9.2e-6, "Liu et al. 2025");
    private static readonly MountMaterial Invar = new("Invar", 2.4e-6, "Liu et al. 2025");

    /// <summary>
    /// Liu et al. (2025): each barrel section of aluminum, titanium or Invar, every combination tried. The search's
    /// best is the combination of least defocus over the range among all 27, each also built and scored alone, and
    /// it holds focus better than the all-aluminum barrel.
    /// </summary>
    [Fact]
    public void A_search_over_barrel_sections_finds_the_best_combination()
    {
        var system = Lenses.AtInfinity(10)
            .Surface(80.0, 4.0, "N-BK7", stop: true).Surface(-200.0, 10.0)
            .Surface(-60.0, 2.5, "N-SF6").Surface(300.0, 12.0)
            .Surface(55.0, 4.0, "N-BK7").Surface(-150.0, 40.0)
            .Image();
        var mount = new Mount(Aluminum, new[] { new MountSegment(1, 3, Aluminum), new MountSegment(3, 5, Aluminum), new MountSegment(5, 7, Aluminum) });
        var metals = new[] { Aluminum, Titanium, Invar };
        var candidates = new SearchCandidates
        {
            Parts = new Dictionary<int, IReadOnlyList<MountMaterial>> { [0] = metals, [1] = metals, [2] = metals },
        };
        Assert.Equal(27, candidates.Count(mount));
        var results = MaterialSearch.Run(system, Catalogs.Library, mount, candidates, new SearchOptions { Keep = 27 });
        Assert.Equal(27, results.Count);
        for (int i = 1; i < results.Count; i++) Assert.True(results[i - 1].Score <= results[i].Score);

        double Rate(params MountMaterial[] parts)
        {
            var m = new Mount(Aluminum, mount.Segments.Select((g, k) => g with { Material = parts[k] }));
            return FocusReport.Compute(new ThermalModel(system, Catalogs.Library, m), Array.Empty<double>(), shares: false).DefocusPerK;
        }
        double least = metals.SelectMany(a => metals.SelectMany(b => metals.Select(c => Math.Abs(Rate(a, b, c))))).Min();
        var top = results[0];
        _out.WriteLine($"best {string.Join(", ", top.Parts.Select(p => p.Name))}: {top.DefocusPerK * 1e3:F4} µm/K; all aluminum {Rate(Aluminum, Aluminum, Aluminum) * 1e3:F4} µm/K");
        Assert.Equal(least, Math.Abs(top.DefocusPerK), 12);
        Assert.True(Math.Abs(top.DefocusPerK) < Math.Abs(Rate(Aluminum, Aluminum, Aluminum)));
        Assert.Equal(0.0, top.EflChange, 12);
    }

    /// <summary>A search over glasses near the doublet's flint: the design's own glass is a candidate, so the best scores no worse than it.</summary>
    [Fact]
    public void A_search_over_glasses_scores_no_worse_than_the_design()
    {
        var system = Lenses.AtInfinity(15, 0.5875618, 0.4861327, 0.6562725)
            .Surface(72.129, 6.0, "N-BK7", stop: true).Surface(-79.369, 2.5, "N-SF6").Surface(-170.598, 117.78)
            .Image();
        system.GlassCatalogs.Add("SCHOTT");
        var flint = Catalogs.Find("SCHOTT:N-SF6");
        var near = MaterialSearch.Neighbors(Catalogs.Library, flint, 0.08, 6.0, new[] { "SCHOTT" }, max: 12);
        Assert.Equal("SCHOTT:N-SF6", near[0].Name);
        var mount = new Mount(MountMaterials.Find("aluminum-6061-t6")!);
        var candidates = new SearchCandidates
        {
            Glasses = new Dictionary<int, IReadOnlyList<Material>> { [2] = near },
            Housing = new[] { MountMaterials.Find("aluminum-6061-t6")!, MountMaterials.Find("stainless-303")!, MountMaterials.Find("ti-6al-4v")! },
        };
        var results = MaterialSearch.Run(system, Catalogs.Library, mount, candidates, new SearchOptions { Keep = 5 });
        foreach (var r in results)
            _out.WriteLine($"{r.Glasses[2].Name,-18} {r.Housing.Name,-18} {r.DefocusPerK * 1e3,8:F3} µm/K  color {r.AxialColor * 1e3,8:F2} µm  ΔEFL {r.EflChange,8:F3}  score {r.Score:F3}");
        var design = MaterialSearch.Run(system, Catalogs.Library, mount, new SearchCandidates(), new SearchOptions { Keep = 1 })[0];
        Assert.True(results[0].Score <= design.Score + 1e-12);
    }

    [Fact]
    public void A_search_too_large_is_refused()
    {
        var system = Lenses.AtInfinity(10).Surface(80.0, 4.0, "N-BK7", stop: true).Surface(-200.0, 90.0).Image();
        var many = Enumerable.Range(0, 10).Select(i => MountMaterial.OfCte(i * 1e-6)).ToList();
        var candidates = new SearchCandidates { Housing = many, Glasses = new Dictionary<int, IReadOnlyList<Material>> { [1] = Enumerable.Repeat(Catalogs.Find("SCHOTT:N-BK7"), 30).ToList() } };
        Assert.Throws<ArgumentException>(() => MaterialSearch.Run(system, Catalogs.Library, new Mount(many[0]), candidates,
            new SearchOptions { MaxCombinations = 100 }));
    }
}

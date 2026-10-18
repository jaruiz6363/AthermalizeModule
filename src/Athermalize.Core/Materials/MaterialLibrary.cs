using AberrationCalculator.Core.Glass;

namespace Athermalize.Core.Materials;

/// <summary>
/// Every material the module can find by name: the glasses of the .AGF catalogs, with their thermal and expansion
/// lines, the measured materials it carries, and the glasses it carries from makers' data sheets.
/// </summary>
public sealed class MaterialLibrary
{
    private readonly Dictionary<string, MeasuredMaterial> _measured = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Tie19Material> _dataSheet = new(StringComparer.OrdinalIgnoreCase);

    public MaterialLibrary()
    {
        foreach (var m in MeasuredMaterial.BuiltIn) _measured[m.Name] = m;
        foreach (var g in DataSheetMaterials.BuiltIn)
        {
            _dataSheet[g.Name] = g;
            _dataSheet.TryAdd(g.Glass, g);
        }
    }

    /// <summary>The catalogs' dispersion data.</summary>
    public GlassCatalog Glasses { get; } = new();

    /// <summary>The catalogs' thermal and expansion data.</summary>
    public ThermalCatalog Thermal { get; } = new();

    public IEnumerable<MeasuredMaterial> Measured => _measured.Values;

    /// <summary>The glasses carried from makers' data sheets.</summary>
    public IEnumerable<Tie19Material> DataSheet => DataSheetMaterials.BuiltIn;

    /// <summary>A library with every .agf file in <paramref name="folder"/>.</summary>
    public static MaterialLibrary FromFolder(string folder)
    {
        var library = new MaterialLibrary();
        library.LoadFolder(folder);
        return library;
    }

    public void LoadFolder(string folder)
    {
        Glasses.LoadFolder(folder);
        Thermal.LoadFolder(folder);
    }

    public void LoadFile(string path)
    {
        Glasses.LoadFile(path);
        Thermal.LoadFile(path);
    }

    /// <summary>
    /// Finds a material by "CATALOG:NAME", or by a bare name as <see cref="GlassCatalog.Find"/> resolves it.
    /// Returns null when nothing matches.
    /// </summary>
    public Material? Find(string name) => Find(name, null);

    /// <summary>The same, a bare name being looked up in the <paramref name="preferred"/> catalogs first.</summary>
    public Material? Find(string name, IReadOnlyList<string>? preferred)
    {
        if (_measured.TryGetValue(name, out var m)) return m;
        var glass = Glasses.Find(name, preferred);
        if (glass != null) return Tie19Material.FromCatalog(glass, Thermal);
        return _dataSheet.TryGetValue(name, out var d) ? d : null;
    }

    /// <summary>Every catalog glass, with its thermal data.</summary>
    public IEnumerable<Tie19Material> CatalogGlasses()
    {
        foreach (var catalog in Glasses.Catalogs)
            foreach (var glass in Glasses.InCatalog(catalog))
                yield return Tie19Material.FromCatalog(glass, Thermal);
    }
}

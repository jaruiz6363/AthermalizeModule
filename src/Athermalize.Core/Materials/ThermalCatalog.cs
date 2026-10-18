using System.Globalization;
using AberrationCalculator.Core.Glass;

namespace Athermalize.Core.Materials;

/// <summary>
/// The thermal (<c>TD</c>) and expansion (<c>ED</c>) lines of .AGF catalogs, which AberrationCalculator's
/// <see cref="GlassCatalog"/> skips. Glasses are keyed as that catalog keys them, "CATALOG:NAME", with the
/// catalog named after its file, so the two read side by side.
///
/// Fields are separated by any run of spaces or tabs and read with the invariant culture, so a doubled space,
/// a tab or the computer's own number format can't shift or change them. A number that won't parse ends the
/// line: the fields before it are kept, and the line counts as incomplete.
/// </summary>
public sealed class ThermalCatalog
{
    private readonly Dictionary<string, (ThermalData Thermal, ExpansionData Expansion)> _byQualifiedName =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byQualifiedName.Count;

    /// <summary>Reads every .agf file in a folder, however its extension is cased.</summary>
    public void LoadFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in GlassCatalog.AgfFiles(folder)) LoadFile(file);
    }

    /// <summary>Reads one .agf file. The catalog name is the file name without its extension.</summary>
    public void LoadFile(string path) =>
        Load(Path.GetFileNameWithoutExtension(path).ToUpperInvariant(), File.ReadLines(path));

    /// <summary>Reads catalog text given as lines, under <paramref name="catalog"/>.</summary>
    public void Load(string catalog, IEnumerable<string> lines)
    {
        catalog = catalog.ToUpperInvariant();
        string? current = null;
        ThermalData thermal = ThermalData.None();
        ExpansionData expansion = ExpansionData.None();

        void Flush()
        {
            if (current != null) _byQualifiedName[catalog + ":" + current] = (thermal, expansion);
        }

        foreach (var raw in lines)
        {
            var parts = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0].ToUpperInvariant())
            {
                case "NM":
                    Flush();
                    current = parts.Length >= 2 ? parts[1] : null;
                    thermal = ThermalData.None();
                    expansion = ExpansionData.None();
                    break;
                case "TD":
                    if (current != null) thermal = ThermalData.FromFields(Numbers(parts));
                    break;
                case "ED":
                    if (current != null) expansion = ExpansionData.FromFields(Numbers(parts));
                    break;
            }
        }
        Flush();
    }

    /// <summary>The thermal data of a glass, or a <see cref="DataState.Missing"/> record when there is none.</summary>
    public ThermalData Thermal(string catalog, string name) =>
        _byQualifiedName.TryGetValue(catalog + ":" + name, out var e) ? e.Thermal : ThermalData.None();

    /// <summary>The expansion data of a glass, or a <see cref="DataState.Missing"/> record when there is none.</summary>
    public ExpansionData Expansion(string catalog, string name) =>
        _byQualifiedName.TryGetValue(catalog + ":" + name, out var e) ? e.Expansion : ExpansionData.None();

    public ThermalData Thermal(GlassData glass) => Thermal(glass.Catalog, glass.Name);
    public ExpansionData Expansion(GlassData glass) => Expansion(glass.Catalog, glass.Name);

    private static List<double> Numbers(string[] parts)
    {
        var values = new List<double>();
        for (int i = 1; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) break;
            values.Add(v);
        }
        return values;
    }
}

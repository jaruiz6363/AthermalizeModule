using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Athermalize.Core.Lens;

/// <summary>
/// How a lens is held and the conditions it is designed for, kept in a file beside the lens: <c>doublet.mount.json</c>
/// beside <c>doublet.zmx</c>. A lens file says nothing about its housing, so this is where the choice is kept from one
/// run to the next. It is plain JSON, to be read, edited and kept with the lens.
///
/// <para>A material is a name from <see cref="MountMaterials"/> or a number, the expansion coefficient in 10⁻⁶/K, as
/// on the command line. Every field may be left out.</para>
/// </summary>
public sealed class MountFile
{
    /// <summary>The format's version; a file of a later version is refused.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The temperature the design is for, °C; 20 °C when left out.</summary>
    public double? NominalTemperatureC { get; set; }

    /// <summary>The air pressure the design is for, in standard atmospheres; 0 is vacuum; 1 when left out.</summary>
    public double? PressureAtm { get; set; }

    /// <summary>The housing's material.</summary>
    public string? Housing { get; set; }

    /// <summary>Spacers, each filling the air space after a surface.</summary>
    public List<SpacerEntry> Spacers { get; set; } = new();

    /// <summary>Parts holding one surface to another: barrel sections, or the part that holds the image plane.</summary>
    public List<PartEntry> Parts { get; set; } = new();

    /// <summary>The substrate of each mirror; a mirror not listed is the housing's material.</summary>
    public SortedDictionary<int, string> Mirrors { get; set; } = new();

    /// <summary>Which glass a cemented surface grows with: "following" (the default) or "preceding".</summary>
    public string? Cemented { get; set; }

    /// <summary>Materials to use in place of the ones the lens file names, by the name in the file.</summary>
    public SortedDictionary<string, string> Use { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Expansion coefficients, 10⁻⁶/K, in place of the data's, by material name.</summary>
    public SortedDictionary<string, double> Cte { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="After">The surface whose following air space the spacer fills.</param>
    /// <param name="Material">Its material.</param>
    /// <param name="Height">Where it touches both surfaces, mm from the axis; 0 is the vertices.</param>
    public sealed record SpacerEntry(int After, string Material, double Height = 0.0);

    /// <param name="From">The first surface the part holds.</param>
    /// <param name="To">The last, or the image surface.</param>
    /// <param name="Material">Its material.</param>
    /// <param name="FromHeight">Where it touches the first surface, mm from the axis.</param>
    /// <param name="ToHeight">Where it touches the last.</param>
    public sealed record PartEntry(int From, int To, string Material, double FromHeight = 0.0, double ToHeight = 0.0);

    /// <summary>The mount file that goes with <paramref name="lensPath"/>: the same name, ending <c>.mount.json</c>.</summary>
    public static string PathFor(string lensPath)
    {
        string dir = Path.GetDirectoryName(lensPath) ?? "";
        return Path.Combine(dir, Path.GetFileNameWithoutExtension(lensPath) + ".mount.json");
    }

    // ── Materials ────────────────────────────────────────────────────────────────────

    /// <summary>A mount material from its text: a name from <see cref="MountMaterials"/>, or α in 10⁻⁶/K.</summary>
    public static MountMaterial Material(string text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double cte))
            return MountMaterial.OfCte(cte * 1e-6);
        return MountMaterials.Find(text)
               ?? throw new ArgumentException($"'{text}' is neither a number nor a mount material (atherm lens --mount-materials)");
    }

    /// <summary>A mount material's text: its name if it is one carried, else its coefficient in 10⁻⁶/K.</summary>
    public static string Text(MountMaterial m)
    {
        if (MountMaterials.Find(m.Name) is { } known && known == m) return known.Name;
        if (m.Curve != null)
            throw new ArgumentException($"{m.Name} has a curve of its own, which a mount file cannot hold; save its name only if it is carried");
        return Round(m.Cte * 1e6).ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>A coefficient to 12 figures, so that one given as 7.2 is written 7.2, not 7.199999999999999.</summary>
    private static double Round(double v) => double.Parse(v.ToString("G12", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    // ── To and from the model ───────────────────────────────────────────────────────

    /// <summary>The mount the file describes; null when it names no housing.</summary>
    public Mount? ToMount()
    {
        if (Housing == null) return null;
        var segments = Spacers.Select(s => MountSegment.Spacer(s.After, Material(s.Material), s.Height))
            .Concat(Parts.Select(p => new MountSegment(p.From, p.To, Material(p.Material), p.FromHeight, p.ToHeight)));
        var mirrors = Mirrors.ToDictionary(m => m.Key, m => Material(m.Value));
        return new Mount(Material(Housing), segments, mirrors, CementedRule);
    }

    /// <summary>The cemented-surface rule the file names.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public CementedSurface CementedRule => Cemented?.ToLowerInvariant() switch
    {
        null or "following" => CementedSurface.FollowingGlass,
        "preceding" => CementedSurface.PrecedingGlass,
        _ => throw new ArgumentException($"cemented: '{Cemented}' is neither 'following' nor 'preceding'"),
    };

    /// <summary>A file holding <paramref name="mount"/> and the rest.</summary>
    public static MountFile From(Mount mount, double nominalTemperatureC = 20.0, double pressureAtm = 1.0,
        IReadOnlyDictionary<string, string>? use = null, IReadOnlyDictionary<string, double>? cte = null)
    {
        var f = new MountFile
        {
            NominalTemperatureC = nominalTemperatureC,
            PressureAtm = pressureAtm,
            Housing = Text(mount.Housing),
            Cemented = mount.Cemented == CementedSurface.FollowingGlass ? "following" : "preceding",
        };
        foreach (var s in mount.Segments)
        {
            if (s.To == s.From + 1 && s.FromHeight == s.ToHeight && s.FreeSpace == null)
                f.Spacers.Add(new SpacerEntry(s.From, Text(s.Material), s.FromHeight));
            else if (s.FreeSpace == null)
                f.Parts.Add(new PartEntry(s.From, s.To, Text(s.Material), s.FromHeight, s.ToHeight));
            else
                throw new ArgumentException($"the part {s.From}-{s.To} names its free air space, which a mount file cannot hold");
        }
        foreach (var (k, m) in mount.MirrorSubstrates) f.Mirrors[k] = Text(m);
        if (use != null) foreach (var (k, v) in use) f.Use[k] = v;
        if (cte != null) foreach (var (k, v) in cte) f.Cte[k] = Round(v);
        return f;
    }

    // ── Reading and writing ──────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Reads a mount file. A material may be written as a name or as a number.</summary>
    public static MountFile Read(string path)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
        catch (JsonException e) { throw new ArgumentException($"{path}: not a mount file: {e.Message}"); }
        if (root is not JsonObject o) throw new ArgumentException($"{path}: not a mount file");
        int version = o["version"]?.GetValue<int>() ?? CurrentVersion;
        if (version > CurrentVersion) throw new ArgumentException($"{path}: format version {version}; this program reads {CurrentVersion}");

        // Materials may be numbers; the model keeps them as text.
        foreach (string key in new[] { "housing" }) Textify(o, key);
        foreach (string list in new[] { "spacers", "parts" })
            if (o[list] is JsonArray a) foreach (var e in a) if (e is JsonObject eo) Textify(eo, "material");
        if (o["mirrors"] is JsonObject mirrors) foreach (var k in mirrors.Select(p => p.Key).ToList()) Textify(mirrors, k);

        try
        {
            var f = o.Deserialize<MountFile>(Json) ?? new MountFile();
            f.Use = new SortedDictionary<string, string>(f.Use, StringComparer.OrdinalIgnoreCase);
            f.Cte = new SortedDictionary<string, double>(f.Cte, StringComparer.OrdinalIgnoreCase);
            return f;
        }
        catch (JsonException e) { throw new ArgumentException($"{path}: {e.Message}"); }

        static void Textify(JsonObject o, string key)
        {
            if (o[key] is JsonValue v && v.TryGetValue(out double d))
                o[key] = d.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Writes the file, with its format and version first.</summary>
    public void Write(string path)
    {
        var o = JsonSerializer.SerializeToNode(this, Json)!.AsObject();
        var ordered = new JsonObject { ["format"] = "athermalize-mount", ["version"] = CurrentVersion };
        foreach (var (k, v) in o.ToList())
        {
            o.Remove(k);
            if (v is JsonArray { Count: 0 } || v is JsonObject { Count: 0 }) continue;
            ordered[k] = v;
        }
        // A number written as a material reads back as α in 10⁻⁶/K; keep it a number in the file.
        void Numberify(JsonObject x, string key)
        {
            if (x[key] is JsonValue v && v.TryGetValue(out string? s)
                && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                x[key] = d;
        }
        Numberify(ordered, "housing");
        foreach (string list in new[] { "spacers", "parts" })
            if (ordered[list] is JsonArray a) foreach (var e in a) if (e is JsonObject eo) Numberify(eo, "material");
        if (ordered["mirrors"] is JsonObject mirrors) foreach (var k in mirrors.Select(p => p.Key).ToList()) Numberify(mirrors, k);
        File.WriteAllText(path, ordered.ToJsonString(Json) + Environment.NewLine);
    }
}

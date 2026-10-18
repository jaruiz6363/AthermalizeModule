using Athermalize.Core.Materials;

namespace Athermalize.Tests;

/// <summary>AberrationCalculator's glass catalogs, copied next to the tests, read once.</summary>
internal static class Catalogs
{
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "catalogs", "Glass");

    private static readonly Lazy<MaterialLibrary> _library = new(() => MaterialLibrary.FromFolder(Folder));

    public static MaterialLibrary Library => _library.Value;

    public static Material Find(string name) =>
        Library.Find(name) ?? throw new InvalidOperationException($"{name} is not in the catalogs");
}

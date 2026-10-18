namespace Athermalize.Core.Lens;

/// <summary>
/// Common mount materials, each with the source of its expansion coefficient. The coefficients are means over
/// roughly 0 to 100 °C. Published values for one alloy differ by several percent between sources, and Invar's
/// depends on the composition and heat treatment of the stock, so a coefficient measured for the actual stock is
/// better when there is one.
/// </summary>
public static class MountMaterials
{
    public static IReadOnlyList<MountMaterial> All { get; } = new MountMaterial[]
    {
        new("aluminum-6061-t6", 23.6e-6,
            "the mean from 20 to 100 °C, Aluminum Association typical value, ASM Aerospace Specification Metals and MatWeb, \"Aluminum 6061-T6; 6061-T651\""),
        new("stainless-303", 17.2e-6,
            "the mean from 0 to 100 °C, ASM Aerospace Specification Metals and MatWeb, \"AISI Type 303 Stainless Steel, annealed\""),
        new("stainless-416", 9.9e-6,
            "the mean from 0 to 100 °C, Ulbrich, \"416 Stainless, UNS S41600\" data sheet (2015)"),
        new("ti-6al-4v", 8.6e-6,
            "the mean from 20 to 100 °C, annealed, ASM Aerospace Specification Metals and MatWeb, \"Titanium Ti-6Al-4V (Grade 5), Annealed\"; TIMET gives 9.0 from 0 to 100 °C"),
        new("invar-36", 1.3e-6,
            "the mean up to 93 °C, Carpenter Technology, \"Invar 36\" data sheet (2025); it depends on the stock"),
    };

    /// <summary>A material by name, case ignored; null when none is carried.</summary>
    public static MountMaterial? Find(string name) =>
        All.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
}

using Athermalize.Core.Materials;

namespace Athermalize.Core.Lens;

/// <summary>
/// Common mount materials, each with the source of its expansion coefficient. The coefficients are means over
/// roughly 0 to 100 °C. Published values for one alloy differ by several percent between sources, and Invar's
/// depends on the composition and heat treatment of the stock, so a coefficient measured for the actual stock is
/// better when there is one.
///
/// <para>The <c>-cryo</c> materials carry their length from 4 to 300 K, NIST's fits for cryogenic design, for a
/// lens that works cold: from room temperature to 77 K, aluminum shrinks 0.39 %, where its mean coefficient near
/// room temperature would give 0.51 %. Above 300 K they hold their coefficient at 300 K.</para>
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
        Cryogenic("aluminum-6061-t6-cryo", "6061%20Aluminum/6061_T6Aluminum_rev.htm", 4, 300, 18, -415.45,
            new[] { -4.1277E2, -3.0389E-1, 8.7696E-3, -9.9821E-6, 0 }, 4),
        Cryogenic("stainless-304-cryo", "304Stainless/304Stainless_rev.htm", 4, 300, 23, -300.04,
            new[] { -2.9554E2, -3.9811E-1, 9.2683E-3, -2.0261E-5, 1.7127E-8 }, 5),
        Cryogenic("ti-6al-4v-cryo", "Ti6Al4V/Ti6Al4V_rev.htm", 4, 300, 24, -173.61,
            new[] { -1.711E2, -2.140E-1, 4.807E-3, -7.111E-6, 0 }, 1.5),
        Cryogenic("invar-36-cryo", "Invar%28Fe-36Ni%29/Invar_rev.htm", 4, 300, 80, -40,
            new[] { -5.265E1, 1.009E-1, 8.395E-4, -1.973E-6, 8.794E-11 }, 5),
    };

    /// <summary>
    /// A material from NIST's cryogenic material properties: (L − L₂₉₃)/L₂₉₃ × 10⁵ = a + bT + cT² + dT³ + eT⁴ above
    /// <paramref name="lowK"/> and <paramref name="low"/> below it, T in kelvin.
    /// </summary>
    private static MountMaterial Cryogenic(string name, string page, double minK, double maxK, double lowK, double low,
        double[] coefficients, double fitPercent)
    {
        var curve = new PolynomialExpansion(new[]
        {
            new PolynomialExpansion.Piece(0, 0, new[] { low * 1e-5 }),
            new PolynomialExpansion.Piece(lowK, 0, coefficients.Select(c => c * 1e-5).ToArray()),
        }, (minK, maxK))
        {
            Source = "NIST, Cryogenic Material Properties, https://trc.nist.gov/cryogenics/materials/" + page,
        };
        return MountMaterial.OfCurve(name, curve, FormattableString.Invariant(
            $"the length from {minK:0} to {maxK:0} K, NIST's fit (within {fitPercent:0.#} % of the data), Cryogenic Material Properties, https://trc.nist.gov/cryogenics/materials/{page}"));
    }

    /// <summary>A material by name, case ignored; null when none is carried.</summary>
    public static MountMaterial? Find(string name) =>
        All.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
}

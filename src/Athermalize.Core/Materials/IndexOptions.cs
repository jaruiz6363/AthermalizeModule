using Athermalize.Core.Air;

namespace Athermalize.Core.Materials;

/// <summary>Which index scales the thermal change of the index, the factor (n² − 1)/(2n) of TIE-19 eq. (3).</summary>
public enum Prefactor
{
    /// <summary>The catalog index, relative to air at the reference temperature and one atmosphere. TIE-19 says it may be used, and glass makers' data-sheet tables agree with it more often than with the absolute index.</summary>
    Relative,

    /// <summary>The absolute index at the reference temperature, n_rel·n_air.</summary>
    Absolute,
}

/// <summary>The wavelength in the E₀ and E₁ term of TIE-19 eq. (3).</summary>
public enum ThermalWavelength
{
    /// <summary>The wavelength in air at the reference conditions, the one the catalog formula is evaluated at. The common practice with AGF catalogs.</summary>
    ReferenceAir,

    /// <summary>The vacuum wavelength, as TIE-19 defines λ.</summary>
    Vacuum,
}

/// <summary>How the sign of λtk enters the denominator λ² − λtk² of TIE-19 eq. (3).</summary>
public enum LambdaTkSign
{
    /// <summary>The AGF rule: the denominator is λ² − sign(λtk)·λtk², so a negative λtk adds.</summary>
    Agf,

    /// <summary>TIE-19 as printed: λ² − λtk², whatever the sign.</summary>
    Ignore,
}

/// <summary>The wavelength the air formulas are evaluated at.</summary>
public enum AirWavelength
{
    /// <summary>The wavelength as given, in air at the system conditions, for both the system and the reference air. The common practice with AGF catalogs.</summary>
    Input,

    /// <summary>The vacuum wavelength, as TIE-19 and Ciddor define λ in their air formulas. The two choices give a glass's index within 10⁻⁸ of each other up to one atmosphere, and within about 5×10⁻⁸ at two.</summary>
    Vacuum,
}

/// <summary>
/// The conventions of the index over temperature and pressure. Wherever published methods differ, the choice is
/// named here, and the default is stated:
/// <list type="bullet">
/// <item>the prefactor follows TIE-19's allowance and the makers' data-sheet tables;</item>
/// <item>the wavelengths, in the E term and in the air formulas, and the λtk sign follow the common practice with AGF
/// catalogs, so that results agree with other implementations of the same model. TIE-19's text takes the vacuum
/// wavelength in both places, and the switches give that choice.</item>
/// </list>
/// </summary>
public sealed record IndexOptions(
    AirModel AirModel = AirModel.Tie19,
    Prefactor Prefactor = Prefactor.Relative,
    ThermalWavelength ThermalWavelength = ThermalWavelength.ReferenceAir,
    LambdaTkSign LambdaTkSign = LambdaTkSign.Agf,
    AirWavelength AirWavelength = AirWavelength.Input)
{
    public static IndexOptions Default { get; } = new();
}

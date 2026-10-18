# Materials

How the module computes a material's refractive index at a temperature and pressure.

## Glasses: SCHOTT's model

Glass catalogs in the AGF format give each glass a dispersion formula that holds in air at a reference temperature, Tref (20 °C in every maker's catalog), and one atmosphere, and a `TD` line of six thermal coefficients, D₀, D₁, D₂, E₀, E₁ and λtk, followed by Tref. SCHOTT's technical information TIE-19, "Temperature Coefficient of the Refractive Index" (2023), gives the model the coefficients belong to. For light of wavelength λ in the system's air, with the glass at temperature T:

1. The air index at the system conditions, n_sys, and at Tref and one atmosphere, n_ref (TIE-19 eqs. 8 and 9).
2. The same light's wavelength in the reference air, λ_ref = λ·n_sys/n_ref.
3. The catalog index there, n_rel0, and the absolute index, n_abs0 = n_rel0·n_ref.
4. The change of the absolute index, with ΔT = T − Tref (TIE-19 eq. 3):
   Δn_abs = (n² − 1)/(2n)·[D₀ΔT + D₁ΔT² + D₂ΔT³ + (E₀ΔT + E₁ΔT²)/(λ² − λtk²)].
5. The index relative to the system air, n = (n_abs0 + Δn_abs)/n_sys.

The model holds from −100 to +140 °C and 0.365 to 1.014 µm (TIE-19); results outside that range, or outside a glass's own wavelength range, are marked.

**Glasses without thermal data.** Some catalog glasses have no `TD` line, an empty one, one without Tref, or coefficients that are all zero. The module takes Tref as 20 °C, never 0 °C, and the glass's index then changes only with the air: steps 1 to 5 with Δn_abs = 0. The output names the glass's state.

**Model glasses** (nd, Vd and ΔPgF, AberrationCalculator's model) are treated the same way: they change with the air only, unless thermal coefficients are given for them.

The catalogs are read with fields separated by any run of spaces or tabs, numbers in the invariant culture, and UTF-8 or UTF-16 text.

## Conventions

Published methods differ in a few places. Each is an option, with a stated default:

| Option | Default | Alternative | Size of the difference |
|---|---|---|---|
| The index in the prefactor (n² − 1)/(2n) | `relative`: the catalog index, which TIE-19 says may be used, and which the makers' data-sheet tables agree with more often | `absolute`: n_abs0 | Up to about 10⁻⁵ for high-index materials far from Tref; about 10⁻⁷ for a typical glass from −40 to +80 °C |
| The wavelength in the E term | `reference`: λ_ref, the common practice with AGF catalogs | `vacuum`: as TIE-19 defines λ | Up to about 10⁻⁶ in the blue at −40 °C; more near λtk |
| The sign of λtk | `agf`: a negative λtk adds λtk² to λ², as AGF catalogs use it | `ignore`: λ² − λtk² as printed in TIE-19 | Only glasses with a negative λtk |
| The wavelength in the air formulas | `input`: λ as given, the common practice with AGF catalogs | `vacuum`: as TIE-19 and Ciddor define λ | Under 10⁻⁸ up to one atmosphere; about 5×10⁻⁸ at two |
| The air model | `tie19`: TIE-19's dry air, which the makers fit their coefficients with | `ciddor`: Ciddor (1996), with humidity and CO₂ | About 4×10⁻⁷ in the air index at 50 % humidity |

## Air

- **TIE-19** (eqs. 8 and 9): Edlén's 1953 dispersion of dry standard air at 15 °C, scaled to the temperature and pressure as an ideal gas. It has no humidity, CO₂ or compressibility term.
- **Ciddor (1996)**: P. E. Ciddor, "Refractive index of air: new equations for the visible and near infrared," Appl. Opt. 35, 1566-1573, with the water-vapor terms as NIST's Engineering Metrology Toolbox states them. Valid from −40 to +100 °C, 80 to 120 kPa, 0 to 100 % humidity and about 0.3 to 1.7 µm.

Pressure is absolute. One standard atmosphere is 101 325 Pa; zero is vacuum, where the relative and absolute indices are the same and wavelengths are vacuum wavelengths.

## Measured materials

For some crystals and fused silicas the module carries models fitted to measurements over temperature rather than catalog coefficients. Five are absolute-index Sellmeier model whose strengths and wavelengths are polynomials in the temperature in kelvin, as published with the measurements made by the Cryogenic High-Accuracy Refraction Measuring System (CHARMS) at NASA Goddard:

| Name | Material | Range | Source |
|---|---|---|---|
| `CHARMS:SI` | Silicon | 1.1-5.6 µm, 20-300 K | Frey, Leviton and Madison, Proc. SPIE 6273, 62732J (2006) |
| `CHARMS:GE` | Germanium | 1.9-5.5 µm, 20-300 K | Same |
| `CHARMS:CAF2` | Calcium fluoride | 0.4-5.6 µm, 25-300 K | Leviton, Frey and Madison, Proc. SPIE 6692, 669204 (2007) |
| `CHARMS:INFRASIL_301` | Infrasil 301 | 0.5-3.6 µm, 35-300 K | Same |
| `CHARMS:FS_7980` | Corning 7980 fused silica | 0.4-2.6 µm, 30-300 K | Leviton and Frey, Proc. SPIE 6273, 62732K (2006) |

These models are measured up to 300 K (27 °C); above that they are extrapolated, and the output marks it.

For the long-wave infrared and for lenses that run warm, the module also carries the recommended indices of H. H. Li's critical reviews in the Journal of Physical and Chemical Reference Data. Li fitted one dispersion equation, with temperature-dependent terms, to the data sets judged reliable from the whole literature on each material:

| Name | Material | Range | Uncertainty in n | Source |
|---|---|---|---|---|
| `JPCRD:GE` | Germanium | 1.9-18 µm, 100-550 K | 2×10⁻³ | Li, J. Phys. Chem. Ref. Data 9, 561 (1980), eq. (28) |
| `JPCRD:ZNS` | CVD zinc sulfide | 0.5-14 µm, 93-618 K | 5×10⁻⁴ | Li, J. Phys. Chem. Ref. Data 13, 103 (1984), eq. (20) |
| `JPCRD:ZNSE` | CVD zinc selenide | 0.55-18 µm, 93-618 K | 4×10⁻⁴ | Same, eq. (23) |

Li gives the index relative to air without naming the air; the module takes it as dry air at 20 °C and one atmosphere (TIE-19's formula), so the absolute index is Li's times that air's index. The choice changes dn/dT by about n·10⁻⁶/K, well inside Li's uncertainties. Li's germanium equation carries the lattice's expansion from 293 K as two cubics. The paper prints the lower cubic's constant as −0.089, a value in percent: as a fraction it is −8.9×10⁻⁴, which makes the cubic vanish at 293 K and meet the upper one with the same slope, and that is the value the module uses.

Where both germanium models hold, 2.5 to 5.5 µm at room temperature, they agree to within 8×10⁻⁴ in the index and about 5 % in dn/dT. Near germanium's absorption edge they part: at 2 µm Li's index is 6.8×10⁻³ below the CHARMS measurement, so below 2.5 µm `CHARMS:GE` is the better choice, as it is for any germanium below room temperature. Above room temperature, or past 5.5 µm, only `JPCRD:GE` holds. Li gives the uncertainty of germanium's dn/dλ as 1.2×10⁻⁴/µm at 10 µm, so its Abbe number over 8-12 µm, 834, is uncertain by about 15 %.

The coefficients and full citations are in `src/Athermalize.Core/Materials/MeasuredMaterials.json`.

Each also carries an expansion coefficient from a separate source:

| Material | α, 10⁻⁶/K | For | Source |
|---|---|---|---|
| Silicon | 2.555 | 293 K, instantaneous | Swenson, J. Phys. Chem. Ref. Data 12, 179 (1983), doi:10.1063/1.555681 |
| Germanium | 5.67 | 293 K, instantaneous | NBS Monograph 29 (1961), from Gibbons, Phys. Rev. 112, 136 (1958) |
| Calcium fluoride | 18.41 | mean, −30 to +70 °C | Hellma Materials, calcium fluoride data sheet (2012) |
| Infrasil 301 | 0.51 | mean, 0 to 100 °C | Heraeus, "Quartz Glass for Optics: Data and Properties" (2019) |
| Corning 7980 | 0.52 | mean, 5 to 35 °C | Corning, HPFS fused silica product information (2014) |
| Germanium (`JPCRD:GE`) | 5.790 | 293 K, instantaneous | The lattice expansion in Li's eq. (28), from Touloukian et al., TPRC Data Series 12 (1975) |
| CVD ZnS | 6.8 | 20 °C, instantaneous | Feldman, Horowitz, Waxler and Dodge, NBS Technical Note 993 (1979), Table 3 |
| CVD ZnSe | 7.3 | 20 °C, instantaneous | Same |

The two germanium entries take their coefficients from different sources, each matching its index model; they differ by 2 %.

These are single values. Silicon's coefficient rises from about 2.1×10⁻⁶/K at 250 K to 2.9×10⁻⁶/K at 350 K (Okada and Tokumaru, J. Appl. Phys. 56, 314, 1984), so over a wide range a single value is an approximation, as a glass's mean coefficient is. The NBS table gives ZnS 5.9 and ZnSe 6.7×10⁻⁶/K at −40 °C, and 7.3 and 7.7 at +80 °C.

## Glasses from makers' data sheets

Some infrared glasses are in no catalog file the module reads, so it carries them from their makers' data sheets as glasses of SCHOTT's model, with the sheets' constants:

| Name | Glass | Range | α, 10⁻⁶/K (20 to 100 °C) | Source |
|---|---|---|---|---|
| `SCHOTT_IRG:IRG22` | Ge₃₃As₁₂Se₅₅ | 1-12 µm | 12.5 | SCHOTT, IRG 22 data sheet (July 2018) |
| `SCHOTT_IRG:IRG25` | Ge₂₈Sb₁₂Se₆₀ | 1-12 µm | 14.0 | SCHOTT, IRG 25 data sheet (July 2018) |
| `SCHOTT_IRG:IRG26` | As₄₀Se₆₀ | 1-12 µm | 21.4 | SCHOTT, IRG 26 data sheet (July 2018) |
| `SCHOTT_IRG:IRG27` | As₂S₃ | 0.6-12 µm | 22.5 | SCHOTT, IRG 27 data sheet (September 2018) |

A bare name (`IRG26`) finds them too, and `--catalog SCHOTT_IRG` puts all four on a map. Each sheet gives a Sellmeier formula at 22 °C and TIE-19's D₀, E₀ and λtk, the absolute index's rate of change at 22 °C, without the higher terms: the model is linear in temperature. The module takes the sheet's index as relative to air at 22 °C and one atmosphere, as catalog indices are. SCHOTT's earlier (2013) sheets tabulated the change from −55 to +75 °C, and the module takes that as the range. The sheets' URLs and the constants are in `src/Athermalize.Core/Materials/DataSheetMaterials.json`.

IRG 27's sheet prints its index at 10 µm as 2.3842, out of line with its neighbors; the formula gives 2.3873, as SCHOTT's product page does.

Other chalcogenide glasses (Umicore's GASIR, LightPath's BD-2 and BD-6) are published as tables of the index and a dn/dT or two, with no formula. One can be used through `LinearMaterial`, from its index, one dn/dT and its expansion coefficient.

## Thermal constants

`atherm materials` holds each wavelength's vacuum wavelength fixed, since that is what stays the same when a lens warms, and puts the material and the air at each temperature:

- dn_rel/dT and dn_abs/dT, by a central difference of ±0.5 K;
- x_f = α − (dn_rel/dT)/(n_rel − 1), the relative change per kelvin of a thin lens's focal length in air at the same temperature (Jamieson, "Thermal effects in optical systems," Opt. Eng. 20, 156-160, 1981). Positive x_f means the focal length grows. α is the `ED` line's expansion coefficient from −30 to +70 °C, or a measured material's.

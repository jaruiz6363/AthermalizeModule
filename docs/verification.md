# Verification

How the module's results are checked, against independent references first. Every check here is a test in `tests/Athermalize.Tests` and runs with `dotnet test`. The checks against Optiland (section 10) need its embedded Python (`tools/setup-python.ps1`); without it they are reported as skipped.

## 1. The index model against its published definition

- **TIE-19 written out.** The test writes TIE-19's steps out again from the published equations and compares them with the module for nine glasses from five makers, from −100 to +140 °C, vacuum to two atmospheres and 0.4 to 1.0 µm, with the default conventions. They agree exactly. (`Tie19MaterialTests.Matches_TIE19_written_out`)
- **Reference conditions.** At its reference temperature, in air at that temperature and one atmosphere, every glass in AberrationCalculator's catalogs (about 1,850 glasses) has its catalog index to 10⁻¹², whatever its `TD` line says. (`Every_glass_has_its_catalog_index_at_the_reference_conditions`)
- **Glasses without thermal data** and model glasses change only with the air, by the closed form n_catalog(λ_ref)·n_ref/n_sys. (`Without_thermal_data_only_the_air_changes`)
- **Conventions.** The prefactor switch scales the thermal change by exactly the ratio of the two prefactors; the λtk sign rule changes only glasses with a negative λtk; and the wavelength options are as large as [materials.md](materials.md) states.

## 2. Glass makers' dn/dT tables

A maker's data sheet tabulates the mean dn/dT over 20 K intervals, which its own thermal coefficients generate. The test computes each entry the same way, (n(T₂) − n(T₁))/20 with the glass and the air at each temperature, at one atmosphere, holding the vacuum wavelength fixed, with the coefficients from the catalogs. (`VendorTableTests`)

- **SCHOTT** data sheets, N-FK51A, N-BK7, N-LASF9, N-SF6 and SF57: relative and absolute, three intervals (−40/−20, +20/+40, +60/+80 °C), at 1060 nm and the e and g lines. All 90 values agree to the printed 0.1×10⁻⁶/K. With the absolute index in the prefactor instead, 88 do.
- **HOYA** NBFD27, +20/+40 °C, relative and absolute at ten wavelengths: 19 of 20 agree to the printed rounding. The relative value at 706.52 nm, 3.7, is 0.06 from the model's 3.64.

SCHOTT's IRG data sheets print the index and the absolute dn/dT at 22 °C, which the sheets' own formulas generate. (`DataSheetMaterialTests`)

- IRG 22, 25, 26 and 27, from 0.6 to 12 µm, 26 values: every index agrees to within 2×10⁻⁴, the rounding of the constants' printed four or five figures; the sheets give the index's tolerance as ±0.001. Every dn/dT agrees to within 0.15×10⁻⁶/K, except two near λtk, where the E₀ term is large and the three printed figures of λtk and E₀ count: IRG 22 at 1 µm (λtk 0.996 µm), 94.2 against 94.8, and IRG 27 at 0.6 µm, 61.6 against 62.2.
- IRG 27 at 10 µm: the sheet prints 2.3842, out of line with its neighbors; the formula gives 2.3873, as SCHOTT's product page does.

## 3. Air

Against NIST's reference values (Stone and Zimmerman, Engineering Metrology Toolbox, Table 1: 14 rows, −40 to +50 °C, 60 to 120 kPa, 0 to 100 % humidity, 300 to 1700 nm). (`AirTests`)

- Ciddor's equation agrees with NIST's Ciddor values to 10⁻⁹, the last printed digit, in every row.
- TIE-19's dry-air formula, a different and older model, is within 10⁻⁷ of NIST's modified Edlén values for the dry rows; the largest difference, 8.9×10⁻⁸, is at −40 °C, 120 kPa and 300 nm.
- TIE-19's formula gives dn_air/dT = −0.967×10⁻⁶/K at 0.55 µm and 15 °C, against Jamieson's (1979) −0.964×10⁻⁶/K from a related form with a rounder constant.

## 4. Measured materials

The CHARMS papers print index tables computed from the models the module carries. (`MeasuredMaterialTests`)

- CaF₂ and Corning 7980, at 295 K (and 30 K for fused silica), 0.4 to 5 µm: every value agrees to the printed five decimals.
- Silicon and germanium, at 295 K (and 30 K for silicon): every value agrees to 10⁻⁵. Their models cancel large terms, so the coefficients' six printed figures cost the sixth decimal. The papers give the models' fit to the measurements as 10⁻⁴ for these two.

Li's reviews print index tables, to four decimals, and dn/dT tables computed from the equations the module carries.

- Germanium, ZnS and ZnSe, from 0.5 to 18 µm and from 93 to 618 K, 19 values: every one agrees to within 7×10⁻⁵.
- dn/dT at 293 K: germanium 4.433×10⁻⁴/K at 3 µm and 4.040×10⁻⁴/K at 10 µm, against the printed 4.431 and 4.033; ZnS 4.09×10⁻⁵/K at 10 µm against 4.1; ZnSe 6.34×10⁻⁵/K at 3 µm against 6.3.

Li's germanium against the CHARMS measurement, an independent one, at 295 K: from 2.5 to 5.5 µm the indices agree to within 8×10⁻⁴ and dn/dT to within 2.4×10⁻⁵/K, inside Li's stated uncertainties of 2×10⁻³ and 0.5×10⁻⁴/K. At 2 µm, near the absorption edge, Li's index is 6.8×10⁻³ low, beyond its uncertainty; [materials.md](materials.md) says which model to use where.

The crystals of AberrationCalculator's MISC catalog carry TIE-19 constants fitted to published measurements: NBS Technical Note 993 for CaF₂, sapphire, MgF₂ and ZnSe, Li's reviews for silicon and germanium, and CHARMS for Infrasil. At 20 °C their dn/dT agrees with the measured models here to within 5 %: CaF₂ and Infrasil against CHARMS, silicon against CHARMS, germanium and ZnSe against Li.

## 5. The lens at a temperature

Closed forms and invariances, each on a lens written for the test. (`ThermalLensTests`)

- **The nominal state.** At the design's own temperature the lens is the nominal design: every radius and thickness the same, and every index AberrationCalculator's catalog index to 10⁻¹², for a doublet at three wavelengths. Building a temperature never changes the nominal design.
- **A lens that only grows.** With every part of the same α and no index change (model glasses without thermal data, in vacuum), a lens with an asphere, a spacer touching the glass 9 mm off the axis and a part holding the image plane grows uniformly: every radius and thickness by 1 + αΔT, the aspheric coefficients as the shape requires, the focal length by 1 + αΔT, and it stays in focus to 10⁻¹⁰ mm, at −60 and +80 °C. An afocal telescope stays afocal, with the same angular magnification to 10⁻¹².
- **A mirror.** A concave mirror on a spacer of its own material stays in focus at −40 and +70 °C. On a spacer of another material it defocuses by exactly f·(α_mirror − α_spacer)·ΔT.
- **An all-aluminum telescope.** Xu et al. (2019) give the back focal length of an aluminum Ritchey-Chrétien telescope in an aluminum tube (α = 23×10⁻⁶/K) as 190.1526, 190.5032 and 190.8537 mm at −60, +20 and +100 °C. A two-mirror telescope written for the test and scaled to 190.5032 mm gives 190.152674 and 190.853726 mm, within 10⁻⁴ mm of the printed values, and exactly 190.5032·(1 + αΔT).
- **A thin singlet.** An N-BK7 singlet of 100 mm focal length, 0.01 mm thick, in an aluminum housing that holds its image plane, defocuses by −2.19133 µm/K, against f·(x_f − α_h) = −2.19140 µm/K from the glass's x_f (Jamieson 1981): within 10⁻³, the rest being the lens's thickness.
- **A thick singlet** against the lensmaker's formula with the grown radii and thickness and the glass's index at the temperature: the index to 10⁻¹⁴, the focal length and the defocus to 10⁻⁹ mm, at −40 and +60 °C.
- **A spacer touching off the axis** (Shi et al. 2020): the air space from the spacer's grown length and the sags of the grown surfaces at the grown contact height, written out with the sphere's sag formula, to 10⁻¹² mm. At a contact height of 8 mm the air space grows by 5.36 µm from 20 to 75 °C, against 5.19 µm measured at the vertices.
- **A chain of barrel sections** (Liu et al. 2025, eq. 5): with three elements seated in aluminum, titanium and Invar sections, the last holding the image plane, each element and the image plane move by the sum of the sections' growths behind them, to 10⁻¹² mm.
- **The stop** grows with its surface: the paraxial marginal ray's height at the stop grows by its glass's 1 + αΔT, to 10⁻¹².
- **An even asphere** grown by s has, at height s·r, s times its nominal sag at r.
- A mount whose parts overlap, a free space that is glass, two parts on the same surfaces, and a glass not in the catalogs are refused.

Expansion over temperature. (`ExpansionCurveTests`)

- **The tables.** The silicon and germanium curves give back the printed rows of Swenson's Table 2 and NBS Monograph 29's Table 2.1, the change of length to 0.05×10⁻⁶ and α exactly. The tables of α alone (NBS Technical Note 993) integrate to the trapezoid rule's length: ZnS grows 810×10⁻⁶ from −40 to +80 °C.
- **Between the rows.** Silicon's α follows Swenson's smooth relation, his eq. (1) with his Table 1, to 3×10⁻⁸/K at seven temperatures from 95 to 775 K, none of them a row. The rest is the rounding of the printed lengths, which set the slope between rows.
- **One lattice.** `JPCRD:GE` grows by the lattice expansion its index model carries, to 10⁻¹⁵, with α 5.790×10⁻⁶/K at 293 K.
- **NIST's fits.** Cryogenic 6061-T6 has the length at 293 K there, shrinks 0.389 % by 77 K and 0.415 % by 4 K, and has α = 22.6×10⁻⁶/K at 293 K. The other three fits also give zero change at 293 K, which checks their coefficients' transcription.
- **Each curve against its single value.** At 20 °C each material's curve gives its tabulated coefficient to 0.5 %.
- **A cold lens.** A silicon singlet in a cryogenic aluminum housing, cooled from 20 °C to 77 K, shrinks between Swenson's 75 and 80 K rows, by 230×10⁻⁶ where α·ΔT would give 552×10⁻⁶. Its housing shrinks by NIST's 0.389 %. At +60 °C, above NIST's data, the output says the housing's length is extrapolated.
- **One coefficient** still grows a length by exactly 1 + αΔT, so every result above for catalog glasses is unchanged.

## 6. Worked examples from the literature

Each reproduced end to end, from the lens to the published number. (`WorkedExampleTests`)

- **Fischer (1985), p. 91: a germanium soak.** A germanium lens of 8 in focal length at f/2, n = 4, dn/dT = 396×10⁻⁶/K, expansion left out, is warmed by 50 °C.
  - Fischer gives 0.0527 in closer, 16.7 depths of focus of ±2λN² at 10 µm.
  - The module's rate times 50 K gives 0.0528 in; Fischer's df = 0.333·f·dn rounds 1/3.
  - Its exact change is 0.0524 in, the thin-lens value, with a depth of ±0.00315 in, so 16.65 depths.
- **Bardazzi (2008): a germanium singlet.** Germanium's x_f in the 2-5 µm band is −131×10⁻⁶/K, so a 21.7 mm lens defocuses optically by −0.142 mm over 50 K. The paper doesn't name its germanium data. From the CHARMS model and germanium's expansion coefficient, at 4 µm over −30 to +20 °C where the model holds, the module gives:
  - x_f = −126×10⁻⁶/K;
  - −0.137 mm over 50 K;
  - both within 4% of the paper's values.
- **Itek's B4 Cassegrain** (Itek Corporation for the Jet Propulsion Laboratory, 1972, NASA N72-33381). This is the 1000 mm f/4.4 Cassegrain with two BK7 correctors of the report's Table 2-7, soaked ±27.5 °C from 7.5 °C in vacuum. Its paraxial EFL and back focus match the printed 39.3046 and 5.8517 in to 2×10⁻⁴ in. The report gives the defocus, and the RMS wavefront change at 0.55 µm without refocusing over fields 0, 0.7 and 1 with its 0.52 central obstruction. The module, at its best focus by the same RMS:

  | Case (Appendix G) | ΔT | Report: defocus in, RMS waves | Module |
  |---|---|---|---|
  | All structure α = 1.5×10⁻⁶/K, ULE mirrors (Table G-6) | +27.5 | −0.0023, 0.142 | −0.00234, 0.150 |
  | | −27.5 | +0.0025, 0.151 | +0.00234, 0.150 |
  | Mirrors only, BK-7, cell α = 0 (Table G-2) | +27.5 | +0.0111, 0.677 | +0.01109, 0.709 |
  | | −27.5 | −0.0107, 0.654 | −0.01108, 0.710 |
  | Mirrors only, ULE | +27.5 | +0.0001, 0.003 | +0.00005, 0.003 |
  | | −27.5 | +0.0001, 0.007 | −0.00005, 0.003 |

  - The warm values agree to 2×10⁻⁴ in.
  - The module's model is linear, so its warm and cold values are equal and opposite. The report's differ by up to 4×10⁻⁴ in, and give the ULE mirrors +0.0001 in both ways, which a linear model can't.
  - BK-7's α is taken as N-BK7's 7.1×10⁻⁶/K; the report doesn't print it.
  - The cases with titanium (Tables G-1 and G-5) aren't tested, since the report doesn't give titanium's α.

## 7. The focus report and the image quality

(`AnalysisTests`)

- **Each part's contribution.** For a doublet and a singlet with a spacer touching off the axis and a part holding the image plane, the six parts' contributions to the focus and focal-length rates add up to the whole lens's rates to 10⁻¹⁴. The rate times ΔT gives the change to 3% over 60 K (2.6% warm, 0.4% cold), the rest being the rise of the glasses' dn/dT with temperature.
- **A thin singlet's contributions** are the glass's f·x_f and the housing's −L·α_h, to 10⁻³ and 10⁻¹² mm/K.
- **A pure defocus.** A paraboloid on axis at f/10, its image plane 0.05 mm from focus, has the closed-form RMS Δz/(8√12 N²) (Itek 1972, Table G-3) to 0.05%. With an obstruction of 0.5 it has that times 1 − ε², also to 0.05%. The compensator returns the plane to the focus to 10⁻⁵ mm.
- **First order against real rays.** For the same doublet and singlet over −40 to +80 °C, the paraxial focus change and the best-focus change by the polychromatic RMS agree to 1.1% (51.2 against 51.8 µm, −52.8 against −53.1 µm). Refocusing never raises the RMS.
- **The geometric MTF temperature range.** A paraboloid of α = 7.1×10⁻⁶/K in an aluminum housing has a geometric MTF at a defocus of 2J₁(x)/x, x = πνΔz/N. So at 40 cycles/mm it stays above 0.8 for 20 ± 12.646 °C. The module finds 7.409 to 32.591 °C, and refocused, the whole range searched.
- **The diffraction MTF temperature range, in the infrared.** A paraboloid of 200 mm at F/8 and 10 µm, on a substrate that doesn't expand, in a housing of 100×10⁻⁶/K.
  - In focus its diffraction MTF at 5 cycles/mm is 0.5025, against the perfect lens's 0.5042; the geometric MTF says 1.
  - The temperatures over which it stays above 0.8 of the perfect lens's are −36.98 to 76.53 °C, against −36.99 to 76.99 °C from Hopkins's (1955) defocused OTF.
  - The high end's 0.46 K is the image point's move, of order Δz/f, which the formula leaves out.

  WavefrontErrorCalculator's own tests check the diffraction MTF against the perfect lens at several wavelengths, Hopkins's defocus, and the Fourier transform of the directly summed PSF off axis (its method.md §11).

## 8. Athermalization

(`AthermalTests`, `SearchTests`)

- **Jiang and Liu (2024), Table 3.** Their final telephoto's eight glasses, by ω, γ, φ and h in an aluminum housing of 22.5×10⁻⁶/K, give the composite point ω′ = −0.277×10⁻³ and γ′ = 0.205×10⁻⁶/K from the table as printed. The paper gives −0.26×10⁻³ and 0.22×10⁻⁶/K, computed before rounding.
- **Jiang and Liu (2024), Table 2.** Their objective from their five best sets' normalized residuals gives 8.53, 8.79, 9.36, 9.38 and 9.96, against the printed 8.55, 8.83, 9.33, 9.36 and 9.93. The columns are rounded to 0.01.
- **The athermal achromatic triplet** of three materials makes the residuals vanish to rounding, and three collinear points have none (Zhang et al. 2012).
- **A doublet** whose two points lie on a line through the housing's point is athermal, and in a warmer housing defocuses by exactly −(Δα_h)/φ per kelvin.
- **The feasibility test** accepts a set whose composite hull holds the origin and rejects two that miss it.
- **Schuster and Franks (2012), Table 2.** γ_T for Ge, GaAs, IKS25, GASIR 1, ZnSe and ZnS from their n, dn/dT and α. All agree to the printed 0.1×10⁻⁶/K but ZnSe, printed 36.6 against the formula's 36.3.
- **Schuster and Franks (2012), eq. 4 and Tables 1 and 3.** A GASIR 1 lens's temperature for one depth of focus, from the full model of a thin lens at f/RF, matches their values to 1.5 K: 84, 52, 36, 18 and 12 K in aluminum, and 203 and 115 K in their passive mounts.
- **A thin doublet from the solver**, N-BK7 and N-SF6, built as thin lenses in the full model, has no axial color (to 10⁻⁵ mm) and the solver's focus rate to 2×10⁻³.
- **The search.** Over aluminum, titanium or Invar for each of three barrel sections, its best is the least-defocus combination of all 27, each built alone, and better than all aluminum (Invar throughout: −1.48 against −3.10 µm/K). Over glasses near a doublet's flint, the best scores no worse than the design, which is among the candidates. A search beyond its limit is refused.

The γ the module computes for Jiang and Liu's SCHOTT glasses, at the d line and 20 °C, relative to the air, is within 0.5×10⁻⁶/K of the values they tabulate. For example N-PK52A gives −25.68 against −25.887, and N-SK11 −1.17 against −0.825. The paper doesn't say whether its dn/dT is absolute or relative, or at which wavelength.

## 9. Catalog reading

Fields separated by doubled spaces or tabs, numbers under German, French and Swedish number settings, UTF-16 files, and missing, empty and short `TD` lines all read as intended, and the shipped catalogs' glasses with missing, empty or all-zero thermal lines are recognized. (`ThermalCatalogTests`)

## 10. Optiland

[Optiland](https://github.com/HarrisonKramer/optiland), an open-source optical design package in Python (MIT license), has its own implementation of SCHOTT's model for the glasses of the refractiveindex.info database it ships with. The tests run it in process, through AberrationCalculator's embedding of Python, with Optiland 0.6.3 unmodified. Optiland changes a glass's index with temperature but does not model a lens over temperature, so the comparison is of the index. (`OptilandTests`)

- **The same glass.** Every one of the 1,300 database glasses Optiland changes with temperature, from CDGM, Hikari, HOYA, Ohara, SCHOTT and Sumita, goes through the module with Optiland's dispersion and thermal constants and Optiland's conventions (`--prefactor absolute --thermal-wavelength system --lambda-tk-sign ignore`). At up to eight wavelengths from 0.40 to 2.33 µm, from −40 to +100 °C, at one atmosphere and at half of one, the 90,370 indices agree with Optiland's to 2×10⁻¹⁶.
- **The conventions.** With the module's defaults the index at the d line, taken from 20 °C to −40 or +100 °C, differs from Optiland's by up to 1.1×10⁻⁵, all of it from the sign of a negative λtk: Optiland squares λtk as printed, and the module takes a negative λtk as adding to λ², as AGF catalogs use it. Only glasses with a negative λtk differ that way; the most is Hikari J-KZFH4. The prefactor alone moves the index by up to 9×10⁻⁷, and the E-term wavelength by up to 2×10⁻⁷.
- **The catalogs.** For 896 glasses the module's catalogs and Optiland's database carry the same thermal constants, and dn/dT at the d line and 20 °C agrees to 1.1×10⁻⁸/K, each with its own conventions. For 244 the constants differ, the catalogs being different editions, by a median of 4×10⁻⁸/K and at most 4×10⁻⁶/K.

## 11. The frozen lens

(`FrozenLensTests`) The doublet designed in air at 20 °C, written with `--write` as it is at −40 °C in vacuum, in `.zmx` and in `.lhlt`, and read back with only its own glass catalog through AberrationCalculator's readers:
- every radius and thickness, and every wavelength, is the module's;
- every relative index at every wavelength is the module's to 10⁻⁷;
- the paraxial back focus at each wavelength is the module's to 1 nm;
- read by the module itself at 20 °C and 1 atm, the frozen glasses, which carry no thermal data, keep their indices to 10⁻⁷.

Germanium and ZnSe over 8-12 µm, frozen at 60 °C, fit within 10⁻⁷; ZnSe needs the Extended 2 formula, the Schott formula missing it by 4×10⁻⁶. Why a lens traced with relative indices at the wavelengths in its air is the same lens is in [other-programs.md](other-programs.md).

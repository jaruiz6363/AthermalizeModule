# Verification

How the module's results are checked, against independent references first. Every check here is a test in `tests/Athermalize.Tests` and runs with `dotnet test`.

## 1. The index model against its published definition

- **TIE-19 written out.** The test writes TIE-19's steps out again from the published equations and compares them with the module for nine glasses from five makers, from −100 to +140 °C, vacuum to two atmospheres and 0.4 to 1.0 µm, with the default conventions. They agree exactly. (`Tie19MaterialTests.Matches_TIE19_written_out`)
- **Reference conditions.** At its reference temperature, in air at that temperature and one atmosphere, every glass in AberrationCalculator's catalogs (about 1,850 glasses) has its catalog index to 10⁻¹², whatever its `TD` line says. (`Every_glass_has_its_catalog_index_at_the_reference_conditions`)
- **Glasses without thermal data** and model glasses change only with the air, by the closed form n_catalog(λ_ref)·n_ref/n_sys. (`Without_thermal_data_only_the_air_changes`)
- **Conventions.** The prefactor switch scales the thermal change by exactly the ratio of the two prefactors; the λtk sign rule changes only glasses with a negative λtk; and the wavelength options are as large as [materials.md](materials.md) states.

## 2. Glass makers' dn/dT tables

A maker's data sheet tabulates the mean dn/dT over 20 K intervals, which its own thermal coefficients generate. The test computes each entry the same way, (n(T₂) − n(T₁))/20 with the glass and the air at each temperature, at one atmosphere, holding the vacuum wavelength fixed, with the coefficients from the catalogs. (`VendorTableTests`)

- **SCHOTT** data sheets, N-FK51A, N-BK7, N-LASF9, N-SF6 and SF57: relative and absolute, three intervals (−40/−20, +20/+40, +60/+80 °C), at 1060 nm and the e and g lines. All 90 values agree to the printed 0.1×10⁻⁶/K. With the absolute index in the prefactor instead, 88 do.
- **HOYA** NBFD27, +20/+40 °C, relative and absolute at ten wavelengths: 19 of 20 agree to the printed rounding. The relative value at 706.52 nm, 3.7, is 0.06 from the model's 3.64.

## 3. Air

Against NIST's reference values (Stone and Zimmerman, Engineering Metrology Toolbox, Table 1: 14 rows, −40 to +50 °C, 60 to 120 kPa, 0 to 100 % humidity, 300 to 1700 nm). (`AirTests`)

- Ciddor's equation agrees with NIST's Ciddor values to 10⁻⁹, the last printed digit, in every row.
- TIE-19's dry-air formula, a different and older model, is within 10⁻⁷ of NIST's modified Edlén values for the dry rows; the largest difference, 8.9×10⁻⁸, is at −40 °C, 120 kPa and 300 nm.
- TIE-19's formula gives dn_air/dT = −0.967×10⁻⁶/K at 0.55 µm and 15 °C, against Jamieson's (1979) −0.964×10⁻⁶/K from a related form with a rounder constant.

## 4. Measured materials

The CHARMS papers print index tables computed from the models the module carries. (`MeasuredMaterialTests`)

- CaF₂ and Corning 7980, at 295 K (and 30 K for fused silica), 0.4 to 5 µm: every value agrees to the printed five decimals.
- Silicon and germanium, at 295 K (and 30 K for silicon): every value agrees to 10⁻⁵. Their models cancel large terms, so the coefficients' six printed figures cost the sixth decimal. The papers give the models' fit to the measurements as 10⁻⁴ for these two.

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

## 6. Catalog reading

Fields separated by doubled spaces or tabs, numbers under German, French and Swedish number settings, UTF-16 files, and missing, empty and short `TD` lines all read as intended, and the shipped catalogs' glasses with missing, empty or all-zero thermal lines are recognized. (`ThermalCatalogTests`)

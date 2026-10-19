# AthermalizeModule

A general-purpose module, as a .NET library and a command-line tool, for the thermal analysis and athermalization of lenses. It is under development. All four parts work now: the materials, the lens at a temperature, the analysis of focus and image quality over temperature, and the choice of materials and mounts.

Planned:

- **Materials:** the refractive index of glasses and crystals over temperature and pressure, from catalog thermal data, with the air index at the system conditions. *Available now; see below.*
- **The lens at a temperature:** radii, thicknesses and aspheres that grow with each element's expansion, and air spaces set by the spacers and the housing. *Available now; see below.*
- **Analysis:** focal length, focus and image quality over a temperature range, at a fixed image plane and with a focus compensator, and each element's share of the focus shift. *Available now; see below.*
- **Athermalization:** materials and mounts that hold focus over the range. *Available now; see below.*

The first release covers a lens soaked at one temperature, of glass and crystal elements in metal mounts. Plastics and temperature gradients come later.

It is meant for rotationally symmetric designs of spheres and even aspheres, refractive and reflective.

## Materials

`atherm materials` prints a material's index and thermal constants over temperature:

```
$ atherm materials N-BK7 --temperatures 20,60 --wavelengths 0.5875618
Air at 1 atm, dry, TIE-19 air; material and air at each temperature. Wavelengths in standard air (20 °C, 1 atm); each is held at its vacuum wavelength.
Prefactor relative; E-term wavelength in reference air; λtk sign agf; air formulas at the input wavelength.
x_f = α − (dn_rel/dT)/(n_rel − 1), the relative change of a thin lens's focal length per kelvin.

SCHOTT:N-BK7: thermal data present, Tref 20 °C; α 7.1×10⁻⁶/K
    T °C      λ µm      n_rel        n_abs     dn_rel/dT  dn_abs/dT      x_f   (10⁻⁶/K)
    20.0  0.587562   1.5168000   1.5172132      2.798      1.386     1.686
    60.0  0.587562   1.5169151   1.5172786      2.968      1.875     1.359
```

- **Glasses** come from the .AGF catalogs: their dispersion formulas through AberrationCalculator, and their thermal (`TD`) and expansion (`ED`) lines read here. The index follows SCHOTT's model (TIE-19). A glass whose catalog gives no thermal coefficients changes only with the air, and the output says so.
- **Measured materials** (`atherm materials --measured`): silicon, germanium, CaF₂, Corning 7980 fused silica and Infrasil 301, from the temperature-dependent Sellmeier models published with NASA Goddard's CHARMS measurements, which hold from about 20 K to 300 K; and germanium, ZnS and ZnSe from 0.5 to 18 µm and about 100 to 600 K, from H. H. Li's reviews in the Journal of Physical and Chemical Reference Data, for the long-wave infrared and warm lenses. The output marks values outside a model's range.
- **Infrared glasses from data sheets:** SCHOTT's IRG 22, 25, 26 and 27 chalcogenides (`SCHOTT_IRG:IRG26`, or `IRG26`), with the constants of SCHOTT's model from their data sheets.
- **Air:** TIE-19's dry-air formula by default, or Ciddor (1996) with humidity and CO₂ (`--air ciddor --humidity 50`).
- **Conventions:** where published methods differ, the choice is a named option with a stated default (`atherm --help`). See [docs/materials.md](docs/materials.md).

## The lens at a temperature

`atherm lens` builds a lens file at each temperature, in its mount, and prints it paraxially:

```
$ atherm lens examples/doublet.zmx --housing aluminum-6061-t6 --temperatures -40,20,80
Cemented doublet, f = 122 mm, f/8 (doublet.zmx), 5 surfaces
Designed for 20 °C in air at 1 atm; every part of the lens and the air at each temperature, the pressure held.
Mount: housing, aluminum-6061-t6 (α 23.6×10⁻⁶/K).
Materials:
  after surface  1  SCHOTT:N-BK7             α 7.1×10⁻⁶/K
  after surface  2  SCHOTT:N-SF6             α 9.03×10⁻⁶/K

Paraxial, at 0.587562 µm (0.587722 µm in vacuum):
    T °C        EFL mm        BFL mm   focus − image plane µm   change from 20 °C µm
   -40.0    122.004701    117.934178                 318.957                 173.016
    20.0    121.999778    117.927941                 145.941                   0.000
    80.0    121.992352    117.919230                 -29.549                -175.490
```

- **Elements** grow with their own expansion coefficients: radii, thicknesses, aspheric terms and apertures, each surface keeping its shape. A mirror grows with its substrate. Silicon, germanium, CaF₂, ZnS and ZnSe, and the `-cryo` mount metals, grow by their measured length over temperature, which matters most for a lens that works cold.
- **The mount** sets the air spaces. The housing holds every air space and the image plane, vertex to vertex. Spacers and barrel sections, each of its own material, hold particular surfaces (`--spacer`, `--part`), and may touch the glass away from the axis, so the change of the surfaces' sags at the contact counts too. Common mount metals are listed by `atherm lens --mount-materials`, each with its source. `--save-mount` keeps the mount in a file beside the lens, `doublet.mount.json`, which later runs read.
- **The indices** are each material's at the temperature, relative to the air at the temperature, at the design's vacuum wavelengths; the stop grows with its surface.
- The lens at a temperature is a copy of AberrationCalculator's lens model with its index tables, so AberrationCalculator's analyses run on it unchanged. See [docs/lens-at-temperature.md](docs/lens-at-temperature.md).

## Focus and image quality over temperature

`atherm focus` gives the first-order report: the focus against the depth of focus at each temperature, and each part's share of the focus shift, with that part alone warmed:

```
$ atherm focus examples/doublet.zmx --housing aluminum-6061-t6 --temperatures -40,20,80
...
First order, at 0.587562 µm. Focus change from 20 °C, positive when the image falls beyond the image plane:
    T °C        EFL mm        BFL mm   change µm   first order µm   F/#    depth of focus ±µm   in depths
   -40.0    122.004701    117.934178     173.016          173.432    8.14               77.803        2.22
    20.0    121.999778    117.927941       0.000           -0.000    8.13               77.735        0.00
    80.0    121.992352    117.919230    -175.490         -173.432    8.13               77.663       -2.26

Each part's share at 20 °C, that part alone warmed:
  part                                     focus µm/K      EFL µm/K
  SCHOTT:N-BK7 after surface 1                 -0.3314       -0.3261
  SCHOTT:N-SF6 after surface 2                  0.2205        0.2371
  housing (aluminum-6061-t6)                   -2.7797        0.0000
  sum of the shares                            -2.8905       -0.0891
  whole lens                                   -2.8905       -0.0891

Glasses at 20 °C and 0.587722 µm (vacuum); x_f = α − (dn_rel/dT)/(n − 1), γ = −x_f, ν_T = −1/x_f:
  part                                       n_rel   dn_rel/dT      α        x_f        γ  (10⁻⁶/K)      ν_T (K)
  SCHOTT:N-BK7 after surface 1            1.516800       2.798     7.10      1.686   -1.686              -592996
  SCHOTT:N-SF6 after surface 2            1.805180       0.954     9.03      7.845   -7.845              -127474
```

`atherm image` gives the RMS wavefront error and the diffraction MTF from real rays, through WavefrontErrorCalculator, at the image plane the mount holds and with the focus compensated, and the temperatures over which the MTF stays above a limit. The MTF is the pupil function's autocorrelation, traced exactly off axis and under vignetting, which matters most in the thermal infrared, where the blur is mostly diffraction; `--mtf geometric` gives the geometric MTF instead:

```
$ atherm image examples/doublet.zmx --housing aluminum-6061-t6 --temperatures -40,20,80 --frequencies 30 --mtf-limit 0.5
...
Image quality, every wavelength, by its weight; RMS wavefront error in waves over the fields by their weights; refocused, the image plane moved to the least RMS:
    T °C   RMS fixed   focus shift µm   RMS refocused
   -40.0      0.1848          172.656          0.0775
    20.0      0.0777            0.155          0.0777
    80.0      0.1865         -174.555          0.0779

Diffraction MTF, sagittal/tangential, fixed | refocused:
  field        0     30 c/mm:  -40 °C 0.400/0.400 | 0.769/0.769  20 °C 0.769/0.769 | 0.769/0.769  80 °C 0.646/0.646 | 0.769/0.769
  field      1.5     30 c/mm:  -40 °C 0.593/0.743 | 0.783/0.695  20 °C 0.783/0.694 | 0.783/0.694  80 °C 0.483/0.287 | 0.783/0.694

The lowest diffraction MTF at 30 c/mm stays at or above 0.5 at the fixed plane: -27.9 to 50.9 °C (searched -60 to 100 °C)
The lowest diffraction MTF at 30 c/mm stays at or above 0.5 refocused: -60.0 to 100.0 °C (searched -60 to 100 °C)
```

## Choosing materials and mounts

- `atherm map` puts materials on the athermal map, x_f against 1/ν, with the housing's point. A thin doublet is athermal and achromatic when its two points and the housing's lie on one line ([map-visible.svg](examples/map-visible.svg), and [map-lwir.svg](examples/map-lwir.svg) for 8-12 µm).
- `atherm solve` gives the thin athermal achromatic doublet or triplet of given materials, and a glass's best partners.
- `atherm select` tries every combination of candidate glasses and mount materials on the lens itself and ranks them:

```
$ atherm select examples/doublet.zmx --housing aluminum-6061-t6 --near 2:0.08:6 --catalog SCHOTT \
      --housing-candidates aluminum-6061-t6,stainless-303,ti-6al-4v --keep 5
...
105 combinations. Score = (|focus rate|·60 K + |axial color|)/depth of focus × (1 + 2|ΔEFL|/EFL), at the design's shapes:
  rank     score   focus µm/K   color µm   ΔEFL mm   materials
  design    2.677     -2.8905     -34.645     0.0000   housing aluminum-6061-t6
  1        0.322     -0.2492      -9.937     0.3753   after 2: SCHOTT:SF6G05; housing ti-6al-4v
  2        0.772     -0.2791     -43.272     0.0002   after 2: SCHOTT:SF6; housing ti-6al-4v
  3        0.772     -0.2791     -43.272     0.0002   after 2: SCHOTT:SF6HT; housing ti-6al-4v
  4        1.109     -1.2621      -9.937     0.3753   after 2: SCHOTT:SF6G05; housing stainless-303
  5        1.313     -1.1238     -34.645     0.0000   after 2: SCHOTT:N-SF6; housing ti-6al-4v
A glass swap changes the lens: optimize the best before taking them further.
```

The [user guide](docs/user-guide.md) shows the options and the library, and [docs/theory.md](docs/theory.md) the definitions and signs. [docs/references.md](docs/references.md) lists every source of data, method and check.

How it is checked: [docs/verification.md](docs/verification.md). Besides closed forms, it reproduces worked examples from the literature: Fischer's (1985) germanium soak, Bardazzi's (2008) germanium singlet, and the soak tables of a 1972 Itek study of a Cassegrain for the Jet Propulsion Laboratory. Its index over temperature agrees with Optiland's, an independent implementation of the same model, for the 1,300 glasses Optiland carries thermal data for.

## Building

[docs/building.md](docs/building.md) has the details: Visual Studio, the tests, and an `atherm` to use outside the repository.

.NET 8 or later. AberrationCalculator and WavefrontErrorCalculator are carried in `external/` as git subtrees, so a plain clone builds:

```
dotnet build
dotnet test
dotnet run --project src/Athermalize.Cli -- materials N-BK7
```

The cross-check against Optiland needs an embedded Python with Optiland: `.\tools\setup-python.ps1` installs one into `python-embed\` (ignored by git). Without it those tests are reported as skipped.

On Windows, the subtrees nest deep enough that a clone into a folder with a long path can pass the 260-character limit: clone to a short path, such as `C:\src`, or set `git config --global core.longpaths true` first.

The optics come from libraries carried in `external/` as subtrees:

- [AberrationCalculator](https://github.com/jaruiz6363/AberrationCalculator): the lens model, readers for lens files, glass catalogs, paraxial and real ray tracing.
- [WavefrontErrorCalculator](https://github.com/jaruiz6363/WavefrontErrorCalculator): wavefront error from real rays, and the diffraction and geometric MTF.

## License

MIT

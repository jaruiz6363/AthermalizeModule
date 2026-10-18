# AthermalizeModule

A general-purpose module, as a .NET library and a command-line tool, for the thermal analysis and athermalization of lenses. It is under development. What works now is the first two parts: the materials, and the lens at a temperature.

Planned:

- **Materials:** the refractive index of glasses and crystals over temperature and pressure, from catalog thermal data, with the air index at the system conditions. *Available now; see below.*
- **The lens at a temperature:** radii, thicknesses and aspheres that grow with each element's expansion, and air spaces set by the spacers and the housing. *Available now; see below.*
- **Analysis:** focal length, focus and image quality over a temperature range, at a fixed image plane and with a focus compensator, and each element's share of the focus shift.
- **Athermalization:** materials and mounts that hold focus over the range.

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
- **Measured materials** (`atherm materials --measured`): silicon, germanium, CaF₂, Corning 7980 fused silica and Infrasil 301, from the temperature-dependent Sellmeier models published with NASA Goddard's CHARMS measurements. They hold from about 20 K to 300 K, and the output marks values outside that range.
- **Air:** TIE-19's dry-air formula by default, or Ciddor (1996) with humidity and CO₂ (`--air ciddor --humidity 50`).
- **Conventions:** where published methods differ, the choice is a named option with a stated default (`atherm --help`). See [docs/materials.md](docs/materials.md).

## The lens at a temperature

`atherm lens` builds a lens file at each temperature, in its mount, and prints it paraxially:

```
$ atherm lens examples/doublet.zmx --housing aluminum-6061-t6 --temperatures -40,20,80
Cemented doublet, f = 122 mm (doublet.zmx), 5 surfaces
Designed for 20 °C in air at 1 atm; every part of the lens and the air at each temperature, the pressure held.
Mount: housing, aluminum-6061-t6 (α 23.6×10⁻⁶/K).
Materials:
  after surface  1  SCHOTT:N-BK7             α 7.1×10⁻⁶/K
  after surface  2  SCHOTT:N-SF6             α 9.03×10⁻⁶/K

Paraxial, at 0.587562 µm (0.587722 µm in vacuum):
    T °C        EFL mm        BFL mm   focus − image plane µm   change from 20 °C µm
   -40.0    122.409315    117.948766                 199.036                 199.015
    20.0    122.378039    117.916721                   0.021                   0.000
    80.0    122.344172    117.882118                -201.552                -201.573
```

- **Elements** grow with their own expansion coefficients: radii, thicknesses, aspheric terms and apertures, each surface keeping its shape. A mirror grows with its substrate.
- **The mount** sets the air spaces. The housing holds every air space and the image plane, vertex to vertex. Spacers and barrel sections, each of its own material, hold particular surfaces (`--spacer`, `--part`), and may touch the glass away from the axis, so the change of the surfaces' sags at the contact counts too. Common mount metals are listed by `atherm lens --mount-materials`, each with its source.
- **The indices** are each material's at the temperature, relative to the air at the temperature, at the design's vacuum wavelengths; the stop grows with its surface.
- The lens at a temperature is a copy of AberrationCalculator's lens model with its index tables, so AberrationCalculator's analyses run on it unchanged. See [docs/lens-at-temperature.md](docs/lens-at-temperature.md).

How it is checked: [docs/verification.md](docs/verification.md).

## Building

.NET 8 or later. AberrationCalculator is carried in `external/` as a git subtree, so a plain clone builds:

```
dotnet build
dotnet test
dotnet run --project src/Athermalize.Cli -- materials N-BK7
```

The optics come from libraries carried in `external/` as subtrees:

- [AberrationCalculator](https://github.com/jaruiz6363/AberrationCalculator): the lens model, readers for lens files, glass catalogs, paraxial and real ray tracing.
- [WavefrontErrorCalculator](https://github.com/jaruiz6363/WavefrontErrorCalculator), for wavefront error and the geometric MTF, joins it when image quality is added.

## License

MIT

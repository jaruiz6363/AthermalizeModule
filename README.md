# AthermalizeModule

A .NET library and a command-line tool, `atherm`, for the thermal analysis and athermalization of lenses: how a lens behaves when its temperature changes, why, and which materials and mounts would keep it in focus. It is under development, and everything below works now.

## What it is for

A lens is designed at one temperature, usually 20 °C, but it is used over a range, often −40 to +70 °C or wider. When the temperature changes, each glass element grows and its refractive index changes. The barrel and spacers grow too, which moves the elements and the detector. The air's index changes with temperature and pressure, and a lens designed in air may be used in vacuum. So the focus moves, and the image at the detector blurs. When the focus stays within the depth of focus over the whole range, the lens is *athermal*.

The module answers four questions about a lens:

| Question | Command |
|---|---|
| How far does the focus move over temperature, and is that within the depth of focus? | `atherm focus` |
| What does that do to the image: the wavefront error and the MTF, with and without refocusing? | `atherm image` |
| Which part moves the focus: which glass, the housing, a spacer? | `atherm focus` |
| Which materials and mount would hold the focus? | `atherm select`, `atherm map`, `atherm solve` |

It also gives the data behind the answers. `atherm materials` gives a material's index over temperature. `atherm lens` gives the lens itself at each temperature, surface by surface, and can write it to a lens file for another program to analyze unchanged.

## A quick look

A cemented doublet of 122 mm focal length at f/8, in an aluminum housing, from −40 to +80 °C:

```
$ atherm focus examples/doublet.zmx --housing aluminum-6061-t6 --temperatures -40,20,80
...
    T °C        EFL mm        BFL mm   change µm   first order µm   F/#    depth of focus ±µm   in depths
   -40.0    122.004701    117.934178     173.016          173.432    8.14               77.803        2.22
    20.0    121.999778    117.927941       0.000           -0.000    8.13               77.735        0.00
    80.0    121.992352    117.919230    -175.490         -173.432    8.13               77.663       -2.26

Each part's contribution at 20 °C, that part alone warmed:
  part                                     focus µm/K      EFL µm/K
  SCHOTT:N-BK7 after surface 1                 -0.3314       -0.3261
  SCHOTT:N-SF6 after surface 2                  0.2205        0.2371
  housing (aluminum-6061-t6)                   -2.7797        0.0000
  sum of the contributions                     -2.8905       -0.0891
  whole lens                                   -2.8905       -0.0891
```

- **The problem:** at −40 °C the focus has moved 173 µm, 2.2 depths of focus.
- **The cause:** the two glasses nearly cancel, and the aluminum housing's growth is most of the drift.
- **What image shows:** at the fixed image plane, the MTF at 30 cycles/mm stays above 0.5 only from −27.9 to +50.9 °C. Refocused, it holds over the whole range.
- **The fix:** `atherm select` tries other flint glasses and housings on the lens. A titanium housing with SF6G05 or SF6 for the flint cuts the drift from −2.89 to about −0.26 µm/K.

`--svg` plots it: the focus against temperature, the depth of focus, and each part's contribution.

![The doublet's focus over temperature, and each part's contribution](examples/doublet-focus.svg)

## What you give it

- **A lens file,** in any format AberrationCalculator reads (.zmx, .seq, .otx, .len, .json, .lhlt), with its glasses in .AGF catalogs.
- **The mount:** the housing, and any spacers and barrel parts, each of its own material. A lens file says nothing about its mount. `--save-mount` keeps it in a file beside the lens, so later runs need no mount options. [docs/mount.md](docs/mount.md) shows each kind of part in a diagram.
- **The temperatures,** and the design's own temperature and air pressure.

## What it covers

- **Materials:**
  - **catalog glasses** over temperature, by SCHOTT's thermal model (TIE-19), from the catalogs' thermal data;
  - **measured models for crystals:** silicon, germanium, CaF₂ and fused silicas from about 20 to 300 K, and germanium, ZnS and ZnSe to about 600 K;
  - **SCHOTT's IRG chalcogenide glasses,** from their data sheets;
  - **the air,** at any temperature and pressure, vacuum included.
- **The lens at a temperature:**
  - each element grows with its own expansion: radii, thicknesses, aspheric terms and apertures;
  - the housing, spacers and barrel parts set the air spaces, including where they touch the glass off the axis;
  - the crystals and the cryogenic mount metals grow by their measured length over temperature, which matters most for a lens that works cold.
- **Analysis:**
  - the focus and focal length over temperature against the depth of focus, and each part's contribution;
  - the RMS wavefront error and the diffraction or geometric MTF from real rays, at the fixed image plane and refocused;
  - the temperature range over which the MTF stays above a limit.
- **Athermalization:**
  - the athermal map of materials and housings;
  - the thin athermal achromatic doublet or triplet of given materials;
  - a search over candidate glasses and mount materials on the lens itself.
- **Other programs:** the lens at a temperature and pressure, written as a lens file with a glass catalog of its materials at those conditions, so another program traces exactly that lens at its default 20 °C and 1 atm.

The first release covers a lens soaked at one temperature, of glass and crystal elements in metal mounts, in rotationally symmetric designs of spheres and even aspheres, refractive and reflective. Plastics and temperature gradients come later.

## Documentation

| Page | What it covers |
|---|---|
| [User guide](docs/user-guide.md) | Start here: a typical session, then each command, what it is for and how to read its output, with worked examples from the literature |
| [Describing the mount](docs/mount.md) | Housings, spacers and barrel parts, with diagrams, and the mount file |
| [The lens at a temperature](docs/lens-at-temperature.md) | Exactly what changes in the lens, and how |
| [Materials](docs/materials.md) | The material data, which source to use where, and the index conventions |
| [Using the lens in other programs](docs/other-programs.md) | Writing the lens at new conditions, and why another program can trace it unchanged |
| [Theory](docs/theory.md) | The definitions and signs |
| [Verification](docs/verification.md) | How the module is checked |
| [References](docs/references.md) | Every source of data, method and check |
| [Building](docs/building.md) | Visual Studio, the tests, and an `atherm` to use outside the repository |

## How it is checked

Besides closed forms, it reproduces worked examples from the literature: Fischer's (1985) germanium soak, Bardazzi's (2008) germanium singlet, and the soak tables of a 1972 Itek study of a Cassegrain for the Jet Propulsion Laboratory. Its index over temperature agrees with Optiland's, an independent implementation of the same model, for the 1,300 glasses Optiland carries thermal data for. [docs/verification.md](docs/verification.md) has the details.

## Building

.NET 8 or later. AberrationCalculator and WavefrontErrorCalculator are carried in `external/` as git subtrees, so a plain clone builds:

```
dotnet build
dotnet test
dotnet run --project src/Athermalize.Cli -- focus examples/doublet.zmx --housing aluminum-6061-t6
```

The cross-check against Optiland needs an embedded Python with Optiland: `.\tools\setup-python.ps1` installs one into `python-embed\` (ignored by git). Without it those tests are reported as skipped.

On Windows, the subtrees nest deep enough that a clone into a folder with a long path can pass the 260-character limit: clone to a short path, such as `C:\src`, or set `git config --global core.longpaths true` first.

The optics come from libraries carried in `external/` as subtrees:

- [AberrationCalculator](https://github.com/jaruiz6363/AberrationCalculator): the lens model, readers for lens files, glass catalogs, paraxial and real ray tracing.
- [WavefrontErrorCalculator](https://github.com/jaruiz6363/WavefrontErrorCalculator): wavefront error from real rays, and the diffraction and geometric MTF.

## License

MIT

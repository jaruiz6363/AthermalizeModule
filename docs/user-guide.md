# User guide

AthermalizeModule is a .NET library and a command-line tool, `atherm`, for the thermal analysis of lenses. This guide shows the tool first, then the library.

## The commands

| Command | What it gives |
|---|---|
| `atherm materials <name>...` | A material's index, dn/dT and x_f over temperature ([materials.md](materials.md)) |
| `atherm lens <file>` | The lens at each temperature, paraxially, and with `--detail` its prescription |
| `atherm focus <file>` | The first-order report: focus against the depth of focus, and each part's share |
| `atherm image <file>` | RMS wavefront error and geometric MTF, at the fixed image plane and refocused |

`atherm --help` lists every option. A lens file is any format AberrationCalculator reads (.zmx, .seq, .otx, .len, .json, .lhlt), in millimeters, with its glasses in the .AGF catalogs next to `atherm` or in `--catalogs`.

### Describing the mount

Every lens command needs at least the housing:

```
atherm focus lens.zmx --housing aluminum-6061-t6
```

- `--housing` holds every air space and the image plane, vertex to vertex.
- `--spacer 3:titanium:9` puts a spacer in the air space after surface 3, touching both surfaces 9 mm off the axis.
- `--part 1-5:invar-36` holds surface 1 to surface 5 with a part of Invar; the last air space inside it takes up the rest. `--part 6-9:stainless-303` with surface 9 the image holds the image plane from surface 6.
- `--mirror 4:23` gives the mirror at surface 4 a substrate of α = 23×10⁻⁶/K.
- A material is a name from `atherm lens --mount-materials`, or a number, α in 10⁻⁶/K.

`--use CAF2=CHARMS:CAF2` uses another material in place of one the file names, and `--cte N-BK7=7.1` another expansion coefficient. The design's temperature is `--nominal` (20 °C), and the air pressure `--pressure` (1 atm; 0 is vacuum). The index conventions are the options of `atherm materials`.

### Reading the reports

The examples in [`../examples`](../examples) are a cemented doublet of 122 mm focal length at f/8 in an aluminum housing.

`atherm focus` gives, at each temperature:
- the focal length and back focal length;
- the change of focus, beside the first-order rate times ΔT;
- the change in depths of focus.

It then gives each part's share of the rates, and the glasses' x_f, γ and ν_T. In [`doublet-focus.txt`](../examples/doublet-focus.txt) the glasses nearly cancel, and the housing's growth, −2.78 µm/K, is most of the −2.89 µm/K total. Over ±60 K that is ±2.2 depths of focus.

`atherm image` gives the RMS wavefront error at the fixed image plane and refocused, and how far the plane must move. It gives the geometric MTF at `--frequencies` both ways, and with `--mtf-limit` the temperatures over which the lowest MTF stays above the limit. In [`doublet-image.txt`](../examples/doublet-image.txt):
- refocusing holds the RMS at 0.078 waves from −40 to +80 °C;
- the MTF at 30 cycles/mm stays above 0.5 from −36.8 to +42 °C at the fixed image plane.

## The library

```csharp
using AberrationCalculator.Core.IO;
using Athermalize.Core.Analysis;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;

var library = MaterialLibrary.FromFolder("catalogs/Glass");
var lens = LensFile.Read("doublet.zmx", library.Glasses);
var mount = new Mount(MountMaterials.Find("aluminum-6061-t6")!,
    new[] { MountSegment.Spacer(3, MountMaterials.Find("ti-6al-4v")!, height: 9.0) });
var model = new ThermalModel(lens, library, mount);

ThermalLens hot = model.At(60.0);                 // its own OpticalSystem, and an index table per wavelength
var report = FocusReport.Compute(model, new[] { -20.0, 20.0, 60.0 });
var image = ImageQuality.Compute(model, new[] { -20.0, 20.0, 60.0 }, frequencies: new[] { 30.0 });
```

- `ThermalModel.At(t)` builds the lens at a temperature. It is a copy of AberrationCalculator's `OpticalSystem` with `Indices[w]`, the index after each surface at each wavelength, so any AberrationCalculator analysis runs on it: `ParaxialTrace.Trace(hot.System, hot.PrimaryIndices, field)`.
- `ThermalModel.At(air, part => ...)` builds it with only some parts warmed; this is how the report finds each part's share.
- `ImageQuality.Model(hot)` gives WavefrontErrorCalculator's `LensModel` of the lens at the temperature, for any of its analyses.
- `Material` and its kinds give the index directly: `Tie19Material` for catalog glasses, `MeasuredMaterial` for the CHARMS models, and `LinearMaterial` for a material known by its index, one dn/dT and α.

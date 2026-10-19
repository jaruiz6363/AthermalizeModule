# Using the lens in other programs

How to analyze a lens at a temperature, or in vacuum, in another optical design program, and what makes that analysis valid. [lens-at-temperature.md](lens-at-temperature.md) says how the module builds the lens; this page says how to take it elsewhere.

## Why the relative index is enough

The module lists two indices for each material, relative and absolute. Only the relative one is needed to trace the lens.

- **The rays.** Snell's law at a surface is n₁ sin θ₁ = n₂ sin θ₂. Divide every index by the air's absolute index and the equation is unchanged, so the rays are unchanged. Absolute indices or indices relative to the air, the same rays come out.
- **The wavefront.** The optical path in absolute indices is n_air times the one in relative indices, and the wavelength measured in the air is the vacuum wavelength divided by n_air. The two factors cancel: the optical path difference in waves is the same.

So a lens traced with **indices relative to the air around it, at the wavelengths measured in that air**, gives exactly the rays, spots, wavefront, PSF and MTF of the same lens traced with absolute indices at the vacuum wavelengths. This holds when one air surrounds the whole lens, as it does in a uniform soak.

## What the absolute index is for

It is how the module moves between data and conditions:
- glass catalogs give indices relative to standard air, 20 °C and 1 atm, while thermo-optic data such as SCHOTT's TIE-19 coefficients are absolute;
- the same lens in another air has other relative indices. N-BK7 is 1.5168000 relative to standard air, and 1.5172132 in vacuum. That rise of 4×10⁻⁴ is why a lens designed in air and used in vacuum is out of focus: the example doublet moves 110 µm.

`atherm lens --detail` lists both, so that either can be compared with a data sheet or with another program's output.

## What a valid analysis needs

At the conditions to be analyzed:
1. **The geometry:** the radii, thicknesses, air spaces and apertures at the temperature, with the image plane where the mount holds it.
2. **The indices:** each material's index relative to the air at those conditions, at every wavelength used.
3. **The wavelengths:** measured in that air, consistently with the indices: in vacuum, the vacuum wavelengths.

And **nothing applied twice.** A program given a lens already grown to the temperature must not grow it again, or change its indices again for temperature or pressure.

## Two ways to do it

**A. The other program's own thermal model.** Open the original design there, set its temperature and pressure, and turn its thermal modeling on. The result is that program's model of the lens, not the module's. It depends on the program's thermo-optic conventions, its expansion data and its model of the mount, which is often simpler: air spaces growing with a coefficient given to each surface, rather than spacers touching the glass away from the axis or measured expansion curves. The two agree only as far as the models do. That makes it a useful independent comparison.

**B. The frozen lens: the module's lens, written out.** `atherm lens --write` writes the lens at one temperature and air so that any program traces it correctly at its default conditions, with nothing to set:

```
atherm lens examples/doublet.zmx --housing aluminum-6061-t6 --temperatures -40 --at-pressure 0 --write doublet-cold-vacuum.zmx
```

This writes two files:
- **`doublet-cold-vacuum.zmx`** ([in `examples`](../examples/doublet-cold-vacuum.zmx)), the lens at −40 °C: its radii, thicknesses, apertures and image distance grown as the mount and the materials say, its wavelengths those in the air at those conditions (in vacuum, the vacuum wavelengths), and its glasses named for the conditions, `N-BK7_M40C_VAC` and `N-SF6_M40C_VAC`.
- **`doublet-cold-vacuum.agf`** ([in `examples`](../examples/doublet-cold-vacuum.agf)), a glass catalog of those glasses. Each is the material's index relative to the air at those conditions, fitted across the band, from 10 % below the lens's shortest wavelength to 10 % above its longest. The fit is the Schott formula when it fits within 10⁻⁷, and otherwise the Extended 2 formula, whose λ⁴ and λ⁶ terms fit infrared materials. `atherm` prints each glass's largest fitting error. The glasses carry no thermal data and are flagged not to expand.

To use it:
- put the `.agf` where the program finds glass catalogs, or beside the lens, as the program requires;
- open the lens at the program's default conditions, 20 °C and 1 atm;
- leave its environment and thermal settings off or at those defaults, and don't change the lens's temperature or pressure.

The program then traces the module's lens: the same geometry, the same relative indices, the same wavelengths. At its reference conditions a program that does apply thermal changes leaves the frozen glasses as they are, since they have no thermal data. Analyses in waves, millimeters of the image and cycles per millimeter are all right. A quantity a program reports in absolute optical path, physical length times absolute index, differs by the factor n_air, which the frozen lens takes as 1.

Formats:
- **`.zmx` and `.lhlt`**, the lens with the `.agf` catalog beside it, which the lens names;
- **`.optiland.json`**, Optiland's format, written by AberrationCalculator's Optiland writer, with the glasses' dispersion data in an `.optiland_glass` folder beside the lens, one refractiveindex.info file per glass.

Each file holds one temperature and one air; write one for each condition to analyze. The image plane stays where the mount holds it, so the frozen lens shows the defocus; refocus with the other program's own compensator to see the best image.

## An example: the doublet at −40 °C in vacuum, in Optiland

The example doublet was designed in air at 20 °C and 1 atm. In an aluminum housing it is to be used in vacuum at −40 °C. [Optiland](https://github.com/HarrisonKramer/optiland) is an open-source optical design package in Python.

**1. Write the lens at those conditions.** The design's conditions are the defaults; they are given here explicitly:

```
atherm lens examples/doublet.zmx --housing aluminum-6061-t6 --nominal 20 --pressure 1 \
    --temperatures -40 --at-pressure 0 --detail --write doublet-cold-vacuum.optiland.json
```

This writes `doublet-cold-vacuum.optiland.json`, the folder `doublet-cold-vacuum.optiland_glass` of its glasses, and `doublet-cold-vacuum.agf`, the same glasses as a catalog for other programs. It prints the lens at −40 °C in vacuum, its first order among it:

```
    T °C        EFL mm        BFL mm   focus − image plane µm   change from 20 °C µm
   -40.0    121.864141    117.795204                 179.984                  34.043
```

**2. Open it in Optiland, at its defaults.** [`optiland_frozen.py`](../examples/optiland_frozen.py) loads the glass catalogs into the session (nothing is installed), loads the lens with Optiland's own reader, and analyzes it. Nothing about temperature or pressure is set: the lens already is at −40 °C in vacuum.

```
python examples/optiland_frozen.py doublet-cold-vacuum.optiland.json

EFL 121.864141 mm
back focal distance 117.795204 mm
paraxial focus from the image plane 179.984 µm
wavelengths, in the lens's air, µm: 0.587722 0.486266 0.656451
surface 1 N-BK7_M40C_VAC: 1.5171546 1.5227181 1.5146820
surface 2 N-SF6_M40C_VAC: 1.8057479 1.8283149 1.7966730
field 0: RMS wavefront error 0.05570 waves
field 1.5: RMS wavefront error 0.08251 waves
```

**3. Compare.** The wavelengths are the vacuum ones, and the indices are the module's at −40 °C in vacuum, digit for digit (`atherm lens --detail` lists them). The focal length, the back focus and the focus from the image plane are the module's to the last digit printed. The RMS over the two fields, √((0.05570² + 0.08251²)/2) = 0.0704 waves, is what `atherm image` gives for the same lens:

```
atherm image examples/doublet.zmx --housing aluminum-6061-t6 --temperatures -40 --at-pressure 0 --monochromatic

    T °C   RMS fixed   focus shift µm   RMS refocused
   -40.0      0.0704           -5.259          0.0703
```

The pupil grid must be fine for the RMS to agree: on Optiland's default grid of 32 rays across it is 0.0549 and 0.0880 waves, and it converges to the module's as the grid is refined, within 0.1 % from 512 rays.

From here it is an ordinary lens in Optiland: any of its analyses applies, at its defaults. The image plane is where the aluminum housing holds it, 180 µm before the paraxial focus, which the design's balance of defocus against spherical aberration leaves 5 µm from the best focus.

## A design not for 20 °C and 1 atm: the Cassegrain at 35 °C

Many designs are not made for 20 °C and 1 atm. The [Cassegrain from the literature](user-guide.md#a-visible-cassegrain-in-space), 1000 mm at f/4.4 with two BK7 correctors, is designed for 7.5 °C in vacuum, the conditions it works at in space. The question here is how it performs warmed to 35 °C, analyzed in a program that knows nothing about either temperature.

The design's conditions are given once, to the module, and never to the other program:

| Where | Temperature | Pressure |
|---|---|---|
| The design (the original lens file, as the module reads it) | 7.5 °C | vacuum |
| The conditions to analyze (the frozen lens) | 35 °C | vacuum |
| The other program, opening the frozen lens | its default, 20 °C | its default, 1 atm |

**1. Give the module the design's conditions.** They are in the lens's mount file, [`itek-cassegrain.mount.json`](../examples/itek-cassegrain.mount.json), with the structure and the mirror substrates:

```json
{
  "format": "athermalize-mount",
  "version": 1,
  "nominalTemperatureC": 7.5,
  "pressureAtm": 0,
  "housing": 1.5,
  "mirrors": { "3": 0.03, "4": 0.03 }
}
```

Without a mount file, give them as options: `--nominal 7.5 --pressure 0`. The module then knows that the file's BK7 is to be taken at 7.5 °C in vacuum, not at the catalog's 20 °C and 1 atm, and that the file's wavelengths are vacuum wavelengths.

**2. Write the lens at 35 °C.** The pressure is the design's, vacuum, so `--at-pressure` isn't needed:

```
atherm lens itek-cassegrain.zmx --temperatures 35 --detail --write itek-warm.zmx

Designed for 7.5 °C in vacuum; every part of the lens and the air at each temperature, the pressure held.
Mount file: itek-cassegrain.mount.json.
...
    T °C        EFL mm        BFL mm   focus − image plane µm   change from 7.5 °C µm
    35.0    998.290624    148.580230                 -60.331                 -60.212

Wrote the lens at 35 °C in vacuum to itek-warm.zmx, and its glasses to itek-warm.agf:
  N-BK7_35C_VAC                0.315-0.77 µm, Schott formula, fitted within 3.3E-8
```

[`itek-warm.zmx`](../examples/itek-warm.zmx) and [`itek-warm.agf`](../examples/itek-warm.agf) are in `examples`, and [`itek-warm.txt`](../examples/itek-warm.txt) is the whole output. With `--detail` it lists the lens at 7.5 °C and at 35 °C side by side:
- BK7 relative to vacuum at 0.55 µm is 1.5189271 at 7.5 °C, and 1.5189683 at 35 °C. The catalog's 20 °C and 1 atm would give 1.5185224, relative to air.
- The correctors grow with BK7, the mirrors barely change (ULE, 0.03×10⁻⁶/K), and the spacings grow with the structure (1.5×10⁻⁶/K): the primary-to-secondary spacing goes from 274.320 to 274.331 mm.
- The image plane stays where the structure holds it, so the focus falls 60 µm before it.

**3. Open it in the other program at its defaults.** The frozen lens holds the lens at 35 °C in vacuum: its geometry, its glass `N-BK7_35C_VAC` at 35 °C relative to vacuum, and its vacuum wavelengths. The other program opens it at 20 °C and 1 atm with its thermal settings off, as for any frozen lens. Neither 7.5 °C nor 35 °C is set there.

In Optiland, with the same script as the doublet (written as `.optiland.json`):

```
atherm lens itek-cassegrain.zmx --temperatures 35 --write itek-warm.optiland.json
python optiland_frozen.py itek-warm.optiland.json

EFL 998.290624 mm
back focal distance 148.580230 mm
paraxial focus from the image plane -60.331 µm
wavelengths, in the lens's air, µm: 0.550000 0.350000 0.450000 0.650000 0.700000
surface 5 N-BK7_35C_VAC: 1.5189683 1.5396648 1.5257815 1.5149577 1.5134985
surface 7 N-BK7_35C_VAC: 1.5189683 1.5396648 1.5257815 1.5149577 1.5134985
field 0: RMS wavefront error 0.20305 waves
field 0.466667: RMS wavefront error 0.18656 waves
field 0.666667: RMS wavefront error 0.16794 waves
```

The focal length, the back focus and the focus from the image plane are the module's, and the indices are its 35 °C ones. The RMS over the three fields, √((0.20305² + 0.18656² + 0.16794²)/3) = 0.1865 waves, is what `atherm image` gives (without the central obstruction, which the lens file doesn't hold):

```
atherm image itek-cassegrain.zmx --temperatures 35 --monochromatic

    T °C   RMS fixed   focus shift µm   RMS refocused
    35.0      0.1865          -53.716          0.0207
```

Warmed to 35 °C, the telescope is 60 µm out of focus at its image plane, 0.19 waves RMS. Refocused, it is back to 0.02 waves, its design performance.

**The design's own conditions.** To look at the design itself in another program, freeze it at its own conditions: `--temperatures 7.5 --write itek-design.zmx`. Then the other program sees BK7 at 7.5 °C in vacuum, as the designer intended, not the catalog's 20 °C and 1 atm. For this telescope the difference is small, because the mirrors carry the power: the BK7 index differs by 4×10⁻⁴, and the focus moves by 0.001 µm. In a lens whose power is in glass, the same index difference moves the focus by about 110 µm, as the doublet in vacuum shows above.

## How it is checked

`FrozenLensTests` writes the doublet designed in air at 20 °C as it is at −40 °C in vacuum, and reads it back with only its own catalog, through AberrationCalculator's readers, which know nothing of temperature:
- every radius and thickness is the module's;
- every relative index at every wavelength is the module's to 10⁻⁷;
- the paraxial back focus at each wavelength is the module's to 0.1 µm (it agrees to 1 nm);
- read by the module itself at 20 °C and 1 atm, where it applies TIE-19, the frozen glasses keep their indices to 10⁻⁷.

Germanium and ZnSe over 8-12 µm, frozen at 60 °C, fit within 10⁻⁷, ZnSe with the Extended 2 formula, where the Schott formula would miss by 4×10⁻⁶.

`OptilandTests` does the same in Optiland, run in process: the frozen doublet's `.optiland.json`, loaded by Optiland's reader at its defaults with only the catalogs written beside it, has the module's focal length, back focus and focus from the image plane to 1 nm, its indices to 10⁻⁷, and its RMS wavefront error at each field to 0.5 %. The Cassegrain, designed for 7.5 °C in vacuum and frozen at 35 °C, passes the same check.

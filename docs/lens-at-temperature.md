# The lens at a temperature

How the module builds a lens at a temperature from its nominal design, its materials and its mount. Everything here is for a uniform soak: every element, every mount part and the air at one temperature.

The lens at a temperature is always built afresh from the nominal design, and the nominal design is never changed. The result is a copy of AberrationCalculator's lens model with an index table for each wavelength, so every analysis AberrationCalculator has runs on it unchanged.

## In short

Each glass element grows as a whole, as if its drawing were enlarged by s = 1 + αΔT with its own α: its radii, its center thickness and its diameter all grow by s, and its shape stays the same. The air spaces are set by the mount, not the glass.

| Quantity | At the temperature | Grows with |
|---|---|---|
| Radius of curvature | × s | the element's glass; a cemented surface, the glass after it; a mirror, its substrate |
| Center thickness of a glass | × s | its glass |
| Semi-diameter and apertures | × s | as the radius |
| Conic constant | unchanged | (a conic enlarged is the same conic) |
| Even-asphere coefficient of r^(2p) | ÷ s^(2p−1) | as the radius, so the sag at s·r is s times the sag at r |
| Zernike terms and their radius | × s | as the radius |
| Air space | the mount's length | the housing, or the spacer or mount part that holds it |
| Distance to the image plane | the mount's length | the housing, unless a mount part holds the image plane |
| Object distance | unchanged | unless a mount part holds it |
| Index | the material's at the temperature | relative to the air at the temperature |

For example, the doublet in `examples` in an aluminum housing, from `atherm lens --detail`, at the design's 20 °C and at 120 °C:

```
atherm lens examples/doublet.zmx --housing aluminum-6061-t6 --temperatures 20,120 --detail

At 20 °C in air at 1 atm (the design):
  wavelength in this air, µm             0.587562     0.486133     0.656273
  wavelength in vacuum, µm               0.587722     0.486266     0.656451
  air, absolute index                  1.00027242   1.00027457   1.00027152
  SCHOTT:N-BK7, relative                1.5168000    1.5223763    1.5143223
  SCHOTT:N-BK7, absolute                1.5172132    1.5227943    1.5147335
  SCHOTT:N-SF6, relative                1.8051802    1.8278292    1.7960789
  SCHOTT:N-SF6, absolute                1.8056720    1.8283311    1.7965665

  surf        radius mm     thickness mm  n_rel 0.5876  n_rel 0.4861  n_rel 0.6563   shape grows with / thickness set by
     1        72.129000         6.000000     1.5168000     1.5223763     1.5143223   SCHOTT:N-BK7 / SCHOTT:N-BK7
     2       -79.369000         2.500000     1.8051802     1.8278292     1.7960789   SCHOTT:N-SF6 / SCHOTT:N-SF6
     3      -170.598000       117.782000     1.0000000     1.0000000     1.0000000   SCHOTT:N-SF6 / housing aluminum-6061-t6
     4         infinity         0.000000     1.0000000     1.0000000     1.0000000   image

At 120 °C in air at 1 atm:
  wavelength in this air, µm             0.587603     0.486167     0.656318
  wavelength in vacuum, µm               0.587722     0.486266     0.656451
  air, absolute index                  1.00020301   1.00020461   1.00020234
  SCHOTT:N-BK7, relative                1.5171026    1.5227176    1.5146093
  SCHOTT:N-BK7, absolute                1.5174106    1.5230292    1.5149158
  SCHOTT:N-SF6, relative                1.8052986    1.8281496    1.7961297
  SCHOTT:N-SF6, absolute                1.8056651    1.8285237    1.7964931

  surf        radius mm     thickness mm  n_rel 0.5876  n_rel 0.4862  n_rel 0.6563   shape grows with / thickness set by
     1        72.180212         6.004260     1.5171026     1.5227176     1.5146093   SCHOTT:N-BK7 / SCHOTT:N-BK7
     2       -79.440670         2.502258     1.8052986     1.8281496     1.7961297   SCHOTT:N-SF6 / SCHOTT:N-SF6
     3      -170.752050       118.059966     1.0000000     1.0000000     1.0000000   SCHOTT:N-SF6 / housing aluminum-6061-t6
     4         infinity         0.000000     1.0000000     1.0000000     1.0000000   image
```

Each block gives, at its conditions:
- the wavelengths, as measured in that air and in vacuum: the light is the same, so its vacuum wavelengths never change;
- the air's absolute index at each wavelength;
- each material's index relative to that air, as a lens file and the ray trace use it, and its absolute index, the relative times the air's;
- the prescription, surface by surface: radius, thickness, and the relative index after the surface at each wavelength.

The design's own conditions come first, so each temperature can be read against them.

- **Surface 1** grows with N-BK7, α 7.1×10⁻⁶/K: 72.129 × (1 + 7.1×10⁻⁶ × 100) = 72.180 mm. The N-BK7 thickness grows the same way, 6.000 to 6.004 mm.
- **Surface 2** is cemented, so it grows with the glass after it, N-SF6, α 9.03×10⁻⁶/K: −79.369 to −79.441 mm. So does surface 3, the back of the N-SF6 element.
- **The air space to the image** grows with the aluminum housing, α 23.6×10⁻⁶/K: 117.782 to 118.060 mm.

## What grows

With ΔT the temperature less the design's (20 °C by default, `--nominal`), and α a coefficient of thermal expansion:

- **Surfaces.** A surface's radius, its Zernike terms and their radius, and its semi-diameter and other apertures grow by s = 1 + αΔT, or, for a material whose length over temperature is known, by the ratio of its lengths at the two temperatures (see below). An even-asphere coefficient of r^(2p) is divided by s^(2p−1), so the surface keeps its shape at the larger size: its sag at height s·r is s times the nominal sag at r. The conic constant does not change.
- **Which α.** A surface between glass and air grows with its glass. A surface between two glasses grows with the glass after it, the element whose front face it is; `CementedSurface.PrecedingGlass` (`--cemented preceding`) takes the glass before it instead. A mirror grows with its substrate, which the mount names; a mirror whose substrate isn't named is taken to be of the housing's material, and the output says so. A surface with air on both sides, such as a stop in the barrel, grows with the housing.
- **Glass thicknesses** grow with their glass.
- **Expansion coefficients** are the catalog's `ED` line (its mean from −30 to +70 °C), or the measured material's, or one given (`--cte NAME=α`). A material without one is held at its nominal size, and the output says so; so is a glass whose catalog flags it not to be expanded.
- **Expansion over temperature.** For the crystals with measured lengths over temperature (see [materials.md](materials.md#expansion-over-temperature)) and the `-cryo` mount metals, a part grows by L(T)/L(T₀), the integral of its coefficient over the change, not α·ΔT with one α. That matters for a lens that works cold: from 20 °C to 77 K, silicon shrinks 230×10⁻⁶, where its coefficient at 20 °C would give 552×10⁻⁶, and 6061 aluminum 0.39 %, where its mean near room temperature would give 0.51 %. Outside a material's data its length is extrapolated, and the output says so.
- **The object distance** does not change unless a mount part holds it. An ideal (paraxial) surface keeps its focal length.

## Air spaces: the mount

The air spaces, and the distance to the image plane among them, are set by the mount, `Mount`, which has three parts.

1. **The housing.** Every air space that no other part holds grows with the housing, vertex to vertex, and so does the space after the last surface, so the image plane is held by the housing at the last surface. With nothing else given, this is the usual model of a lens in a barrel with its detector on the same structure (Jamieson 1981).
2. **Mount parts** (`MountSegment`, `--part a-b:material`). A part holds surface a to surface b a fixed length apart: a barrel section between two lens seats, or the housing from a lens to the image plane (b is then the image surface). The part's length grows with its own α. The glass thicknesses inside it grow with their glasses, the other air spaces inside it by their own rules, and one air space, by default the last one in the part, takes up the rest. A chain of parts, each of its own material, moves each element along the barrel by the sum of the growths of the parts behind it (Liu et al. 2025, eq. 5). Parts may nest, a spacer inside a barrel section say, but not overlap.
3. **Contact heights.** A part may touch its two surfaces away from the axis, at heights h_a and h_b, as a spacer touches a lens near its edge. Its length is then measured between those two points: the vertex distance, plus the sag of the second surface at h_b, less the sag of the first at h_a. At the temperature the length and both heights grow with the part, and the surfaces' sags are those of the grown surfaces, so the air space changes by the change of the sags at the contact as well as by the part's growth (Shi et al. 2020). A spacer is the part that fills one air space, `MountSegment.Spacer` (`--spacer s:material:h`). At h = 0 it is the vertex distance grown with the spacer.

Air spaces are computed from the full surface shapes, not linearized, so a spacer touching a steep surface far from the axis is modeled as it is.

## The indices and the wavelengths

The index after each surface is the material's at the temperature, relative to the air at the temperature and pressure: the design's pressure (`--pressure`), or the one given for use (`--at-pressure`, 0 for vacuum), from the models in [materials.md](materials.md). The light is held at the vacuum wavelengths of the design's wavelengths, which are taken to be in the design's air. The lens at the temperature carries the same light's wavelengths in the air at the temperature, so a result in waves is in waves of the light the lens actually sees.

## The aperture

The stop is a physical aperture, so it grows with its surface. The lens at a temperature has its aperture as an entrance pupil diameter: the one whose paraxial marginal ray fills the grown stop, whichever way the file stated the aperture.

## What is left alone

A thickness solve in the file is not re-solved at the temperature: refocusing is a compensator's job, not the temperature's. The copy has the solve flags cleared and no pickups, so nothing re-applies them.

## The command

```
atherm lens <file> --housing <material|α> [--spacer s:material:h] [--part a-b:material] [--mirror s:material]
                   [--temperatures -20,20,60] [--nominal °C] [--pressure atm] [--at-pressure atm] [--detail]
```

It prints, at each temperature, the paraxial focal length and back focal length at the primary wavelength, the paraxial focus less the distance to the image plane, and that distance's change from the design's temperature. `--nominal` and `--pressure` are the design's temperature and pressure; `--at-pressure` is the pressure at each temperature, when it differs from the design's, as for a lens designed in air and used in vacuum. `--detail` adds, first at the design's conditions and then at each temperature, the air's absolute index, each material's relative and absolute index at each wavelength, and the prescription surface by surface, with what each surface grew with and what set each thickness ([the example above](#in-short); [`doublet-vacuum-detail.txt`](../examples/doublet-vacuum-detail.txt) is the doublet designed in air at 20 °C and used in vacuum at −40 °C). A material is given by name (`atherm lens --mount-materials` lists aluminum 6061-T6, stainless steels 303 and 416, Ti-6Al-4V and Invar 36, each with its source) or by its α in 10⁻⁶/K. Published values for one alloy differ by several percent between sources, and Invar's depends on the stock, so a value measured for the actual stock is better when there is one.

`--write <file>`, with one temperature, writes the lens at it with its glasses frozen into a catalog beside it, for another program to trace at its default conditions; see [other-programs.md](other-programs.md).

## References

The sources this document draws on; [references.md](references.md) lists every source of the module, its data among them.

- T. H. Jamieson, "Thermal effects in optical systems," Opt. Eng. 20(2), 156-160 (1981).
- Y. Liu, B. Li, G. Gu, H. Li and G. Lin, "Multiple lens athermalization and achromatization by the Cartesian-product free combination method based on the thermally-induced defocus model," Opt. Express 33(23), 49404-49422 (2025), doi:10.1364/OE.569582.
- J. Shi, J. Liu, T. Chen, J. Wang and F. Yu, 12th International Conference on Intelligent Human-Machine Systems and Cybernetics (IHMSC), IEEE, 87-91 (2020), doi:10.1109/IHMSC49165.2020.10098.
- S. Xu, X. Peng, G. Tie, C. Guan, H. Hu and Y. Xiong, "Athermalization of an optical system based on lens shape and assembly method," Curr. Opt. Photon. 3(5), 429-437 (2019), doi:10.3807/COPP.2019.3.5.429.

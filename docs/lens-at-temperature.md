# The lens at a temperature

How the module builds a lens at a temperature from its nominal design, its materials and its mount. Everything here is for a uniform soak: every element, every mount part and the air at one temperature.

The lens at a temperature is always built afresh from the nominal design, and the nominal design is never changed. The result is a copy of AberrationCalculator's lens model with an index table for each wavelength, so every analysis AberrationCalculator has runs on it unchanged.

## What grows

With ΔT the temperature less the design's (20 °C by default, `--nominal`), and α a coefficient of thermal expansion:

- **Surfaces.** A surface's radius, its Zernike terms and their radius, and its semi-diameter and other apertures grow by s = 1 + αΔT. An even-asphere coefficient of r^(2p) is divided by s^(2p−1), so the surface keeps its shape at the larger size: its sag at height s·r is s times the nominal sag at r. The conic constant does not change.
- **Which α.** A surface between glass and air grows with its glass. A surface between two glasses grows with the glass after it, the element whose front face it is; `CementedSurface.PrecedingGlass` (`--cemented preceding`) takes the glass before it instead. A mirror grows with its substrate, which the mount names; a mirror whose substrate isn't named is taken to be of the housing's material, and the output says so. A surface with air on both sides, such as a stop in the barrel, grows with the housing.
- **Glass thicknesses** grow with their glass.
- **Expansion coefficients** are the catalog's `ED` line (its mean from −30 to +70 °C), or the measured material's, or one given (`--cte NAME=α`). A material without one is held at its nominal size, and the output says so; so is a glass whose catalog flags it not to be expanded.
- **The object distance** does not change unless a mount part holds it. An ideal (paraxial) surface keeps its focal length.

## Air spaces: the mount

The air spaces, and the distance to the image plane among them, are set by the mount, `Mount`, which has three parts.

1. **The housing.** Every air space that no other part holds grows with the housing, vertex to vertex, and so does the space after the last surface, so the image plane is held by the housing at the last surface. With nothing else given, this is the usual model of a lens in a barrel with its detector on the same structure (Jamieson 1981).
2. **Mount parts** (`MountSegment`, `--part a-b:material`). A part holds surface a to surface b a fixed length apart: a barrel section between two lens seats, or the housing from a lens to the image plane (b is then the image surface). The part's length grows with its own α. The glass thicknesses inside it grow with their glasses, the other air spaces inside it by their own rules, and one air space, by default the last one in the part, takes up the rest. A chain of parts, each of its own material, moves each element along the barrel by the sum of the growths of the parts behind it (Liu et al. 2025, eq. 5). Parts may nest, a spacer inside a barrel section say, but not overlap.
3. **Contact heights.** A part may touch its two surfaces away from the axis, at heights h_a and h_b, as a spacer touches a lens near its edge. Its length is then measured between those two points: the vertex distance, plus the sag of the second surface at h_b, less the sag of the first at h_a. At the temperature the length and both heights grow with the part, and the surfaces' sags are those of the grown surfaces, so the air space changes by the change of the sags at the contact as well as by the part's growth (Shi et al. 2020). A spacer is the part that fills one air space, `MountSegment.Spacer` (`--spacer s:material:h`). At h = 0 it is the vertex distance grown with the spacer.

Air spaces are computed from the full surface shapes, not linearized, so a spacer touching a steep surface far from the axis is modeled as it is.

## The indices and the wavelengths

The index after each surface is the material's at the temperature, relative to the air at the temperature and the design's pressure (`--pressure`), from the models in [materials.md](materials.md). The light is held at the vacuum wavelengths of the design's wavelengths, which are taken to be in the design's air. The lens at the temperature carries the same light's wavelengths in the air at the temperature, so a result in waves is in waves of the light the lens actually sees.

## The aperture

The stop is a physical aperture, so it grows with its surface. The lens at a temperature has its aperture as an entrance pupil diameter: the one whose paraxial marginal ray fills the grown stop, whichever way the file stated the aperture.

## What is left alone

A thickness solve in the file is not re-solved at the temperature: refocusing is a compensator's job, not the temperature's. The copy has the solve flags cleared and no pickups, so nothing re-applies them.

## The command

```
atherm lens <file> --housing <material|α> [--spacer s:material:h] [--part a-b:material] [--mirror s:material]
                   [--temperatures -20,20,60] [--detail]
```

It prints, at each temperature, the paraxial focal length and back focal length at the primary wavelength, the paraxial focus less the distance to the image plane, and that distance's change from the design's temperature. `--detail` adds the prescription at each temperature, with what each surface grew with and what set each thickness. A material is given by name (`atherm lens --mount-materials` lists aluminum 6061-T6, stainless steels 303 and 416, Ti-6Al-4V and Invar 36, each with its source) or by its α in 10⁻⁶/K. Published values for one alloy differ by several percent between sources, and Invar's depends on the stock, so a value measured for the actual stock is better when there is one.

## References

- T. H. Jamieson, "Thermal effects in optical systems," Opt. Eng. 20(2), 156-160 (1981).
- Y. Liu, B. Li, G. Gu, H. Li and G. Lin, "Multiple lens athermalization and achromatization by the Cartesian-product free combination method based on the thermally-induced defocus model," Opt. Express 33(23), 49404-49422 (2025), doi:10.1364/OE.569582.
- J. Shi, J. Liu, T. Chen, J. Wang and F. Yu, 12th International Conference on Intelligent Human-Machine Systems and Cybernetics (IHMSC), IEEE, 87-91 (2020), doi:10.1109/IHMSC49165.2020.10098.
- S. Xu, X. Peng, G. Tie, C. Guan, H. Hu and Y. Xiong, "Athermalization of an optical system based on lens shape and assembly method," Curr. Opt. Photon. 3(5), 429-437 (2019), doi:10.3807/COPP.2019.3.5.429.

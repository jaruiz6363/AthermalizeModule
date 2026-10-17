# AthermalizeModule

A general-purpose module, as a .NET library and a command-line tool, for the thermal analysis and athermalization of lenses. It is under development, and there is nothing to use yet.

Planned:

- **Materials:** the refractive index of glasses, crystals and plastics over temperature and pressure, from catalog thermal data, with the air index at the system conditions.
- **The lens at a temperature:** radii, thicknesses and aspheres that grow with each element's expansion, and air spaces set by the spacers and the housing.
- **Analysis:** focal length, focus and image quality over a temperature range, at a fixed image plane and with a focus compensator, and each element's share of the focus shift.
- **Athermalization:** materials and mounts that hold focus over the range.

It is meant for rotationally symmetric designs of spheres and even aspheres, refractive and reflective.

The optics will come from two libraries, carried in `external/` as subtrees:

- [AberrationCalculator](https://github.com/jaruiz6363/AberrationCalculator): the lens model, readers for lens files, glass catalogs, paraxial and real ray tracing.
- [WavefrontErrorCalculator](https://github.com/jaruiz6363/WavefrontErrorCalculator): wavefront error and the geometric MTF.

## License

MIT

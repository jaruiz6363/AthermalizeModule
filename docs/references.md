# References

Every source the module takes data, a method or a check from. The other documents cite these by author and year. Where a source supplies numbers, the files that carry them name the table or equation each value comes from: `src/Athermalize.Core/Materials/MeasuredMaterials.json`, `DataSheetMaterials.json`, `src/Athermalize.Core/Lens/MountMaterials.cs`, and the comments in AberrationCalculator's catalogs.

## The index model and the air

- SCHOTT, "Temperature Coefficient of the Refractive Index," Technical Information TIE-19 (2023). The thermal model of the glasses (eq. 3) and its dry air (eqs. 8 and 9).
- B. Edlén, "The dispersion of standard air," J. Opt. Soc. Am. 43, 339-344 (1953). The dispersion TIE-19's air is scaled from.
- P. E. Ciddor, "Refractive index of air: new equations for the visible and near infrared," Appl. Opt. 35, 1566-1573 (1996), doi:10.1364/AO.35.001566. The `ciddor` air model.
- J. A. Stone and J. H. Zimmerman, *Engineering Metrology Toolbox: Refractive Index of Air Calculator*, NIST, https://emtoolbox.nist.gov/wavelength/. The water-vapor terms used with Ciddor's equation, and the reference values of the air tests (Table 1).

## Material data

### Glass catalogs

The makers' catalogs in AGF format, carried in AberrationCalculator's `catalogs/Glass`. Each glass's dispersion formula, its thermal constants (`TD`) and its expansion (`ED`) are the maker's.

- SCHOTT optical glass catalog (June 2025).
- HOYA optical glass catalog (January 26, 2026).
- Sumita optical and molding glass data (November 5, 2025).
- OHARA and CDGM optical glass catalogs.
- Corning, "HPFS 7979, 7980, 8655 Fused Silica," product information (July 2015): the `CORNING_FS` catalog.
- LightPath molded-asphere glasses: the `LIGHTPATH` catalog.
- `MISC`: crystals, plastics, water and other materials from published sources, each named in the entry's comments. Its thermal constants are fitted to the measurements listed under [The MISC catalog's thermal data](#the-misc-catalogs-thermal-data).

### Measured materials

- B. J. Frey, D. B. Leviton and T. J. Madison, "Temperature-dependent refractive index of silicon and germanium," Proc. SPIE 6273, 62732J (2006), doi:10.1117/12.672850. `CHARMS:SI` (Table 5) and `CHARMS:GE` (Table 10).
- D. B. Leviton and B. J. Frey, "Temperature-dependent absolute refractive index measurements of synthetic fused silica," Proc. SPIE 6273, 62732K (2006), doi:10.1117/12.672853. `CHARMS:FS_7980` (Table 3).
- D. B. Leviton, B. J. Frey and T. J. Madison, "Temperature-dependent refractive index of CaF2 and Infrasil 301," Proc. SPIE 6692, 669204 (2007), doi:10.1117/12.735594. `CHARMS:CAF2` (Table 5) and `CHARMS:INFRASIL_301` (Table 10).
- H. H. Li, "Refractive index of silicon and germanium and its wavelength and temperature derivatives," J. Phys. Chem. Ref. Data 9, 561-658 (1980), doi:10.1063/1.555624. `JPCRD:GE` (eq. 28) and its lattice expansion.
- H. H. Li, "Refractive index of ZnS, ZnSe, and ZnTe and its wavelength and temperature derivatives," J. Phys. Chem. Ref. Data 13, 103-150 (1984), doi:10.1063/1.555705. `JPCRD:ZNS` (eq. 20) and `JPCRD:ZNSE` (eq. 23).

### Infrared glasses from data sheets

- SCHOTT, "Infrared Chalcogenide Glass IRG 22," data sheet (July 2018), https://us.shop.schott.com/medias/IRG22-July-2018-US.pdf.
- SCHOTT, "Infrared Chalcogenide Glass IRG 25," data sheet (July 2018), https://us.shop.schott.com/medias/IRG25-July-2018-US.pdf.
- SCHOTT, "Infrared Chalcogenide Glass IRG 26," data sheet (July 2018), https://us.shop.schott.com/medias/IRG26-July-2018-US.pdf.
- SCHOTT, "Infrared Chalcogenide Glass IRG 27," data sheet (September 2018), https://media.schott.com/api/public/content/23af1b23e5c74272bc3ed1f3fa554fbd.
- SCHOTT, IRG 22 to IRG 26 data sheets (October 2013). The temperature range the 2018 sheets' constants are taken to hold over, −55 to +75 °C.

### Expansion

- C. A. Swenson, "Recommended values for the thermal expansivity of silicon from 0 to 1000 K," J. Phys. Chem. Ref. Data 12, 179-182 (1983), doi:10.1063/1.555681. Silicon's coefficient and its length over temperature (Table 2).
- R. J. Corruccini and J. J. Gniewek, "Thermal expansion of technical solids at low temperatures," NBS Monograph 29 (1961). Germanium's coefficient and its contraction from 0 to 300 K (Table 2.1).
- D. F. Gibbons, "Thermal expansion of some crystals with the diamond structure," Phys. Rev. 112, 136-140 (1958), doi:10.1103/PhysRev.112.136. The germanium measurements of NBS Monograph 29's table.
- Y. S. Touloukian, R. K. Kirby, R. E. Taylor and P. D. Desai, *Thermal Expansion: Metallic Elements and Alloys*, TPRC Data Series 12 (IFI/Plenum, 1975). The lattice expansion in Li's germanium equation.
- A. Feldman, D. Horowitz, R. M. Waxler and M. J. Dodge, "Optical materials characterization: final technical report," NBS Technical Note 993 (1979). The coefficients of CaF₂, CVD ZnS and CVD ZnSe from −180 to +200 °C (Table 3).
- Heraeus, "Quartz Glass for Optics: Data and Properties" (December 2019). Infrasil 301's coefficient.
- Corning, "HPFS 7979, 7980, 8655 Fused Silica: Optical Materials Product Information" (February 2014). Corning 7980's coefficient.
- Hellma Materials, "Calcium Fluoride," data sheet (2012). CaF₂'s mean coefficient, for comparison with NBS's.
- NIST, *Cryogenic Material Properties*, https://trc.nist.gov/cryogenics/materials/materialproperties.htm: the length from 4 to 300 K of 6061-T6 aluminum, 304 stainless steel, Ti-6Al-4V and Invar (Fe-36Ni), the `-cryo` mount materials.
- The room-temperature mount materials, each as `atherm lens --mount-materials` names it: ASM Aerospace Specification Metals and MatWeb data sheets for 6061-T6 aluminum (with the Aluminum Association's typical value), 303 stainless steel and Ti-6Al-4V; TIMET for Ti-6Al-4V; Ulbrich, "416 Stainless, UNS S41600" (2015); Carpenter Technology, "Invar 36" (2025).

### The MISC catalog's thermal data

AberrationCalculator's `MISC` catalog fits its materials' thermal constants to these (AberrationCalculator PR #8):

- Feldman, Horowitz, Waxler and Dodge, NBS Technical Note 993 (1979), above: the absolute dn/dT of CaF₂, sapphire, MgF₂ and ZnSe from −180 to +200 °C (Table 4).
- Li (1980), above: silicon (eq. 22) and germanium (eq. 28).
- Leviton, Frey and Madison (2007), above: Infrasil 301.
- IAPWS, "Release on the Refractive Index of Ordinary Water Substance as a Function of Wavelength, Temperature and Pressure," R9-97 (1997), with the density of M. Tanaka, G. Girard, R. Davis, A. Peuto and N. Bignell, "Recommended table for the density of water between 0 °C and 40 °C based on recent experimental reports," Metrologia 38, 301-309 (2001), doi:10.1088/0026-1394/38/4/3. Water.
- S. N. Kasarova, N. G. Sultanova and I. D. Nikolov, "Temperature dependence of refractive characteristics of optical plastics," J. Phys.: Conf. Ser. 253, 012028 (2010), doi:10.1088/1742-6596/253/1/012028. PMMA.
- N. Sultanova, S. Kasarova and I. Nikolov, "Characteristics of optical polymers in the design of polymer and hybrid optical systems," Bulg. J. Phys. 40, 258-264 (2013). Polycarbonate and Zeonex E48R.
- J. A. Corsetti, W. E. Green, J. D. Ellis, G. R. Schmidt and D. T. Moore, "Measurement of linear coefficient of thermal expansion and temperature-dependent refractive index using interferometric system," University of Rochester, NASA NTRS 20170006479. PMMA, as a check.

## Methods

- D. S. Grey, "Athermalization of optical systems," J. Opt. Soc. Am. 38, 542-546 (1948).
- T. H. Jamieson, "Thermal effects in optical systems," Proc. SPIE 193, 101-107 (1979).
- T. H. Jamieson, "Thermal effects in optical systems," Opt. Eng. 20, 156-160 (1981). The thin lens's x_f, and the mount's role.
- C. Jiang and M. Liu, "Athermalization of a lens system by glass selection using simulated annealing with memory augmentation," Opt. Express 32, 14860-14875 (2024), doi:10.1364/OE.521560. The athermal map and the thin-lens residuals.
- Y. Liu, B. Li, G. Gu, H. Li and G. Lin, "Multiple lens athermalization and achromatization by the Cartesian-product free combination method based on the thermally-induced defocus model," Opt. Express 33, 49404-49422 (2025), doi:10.1364/OE.569582. Barrel sections in a chain.
- J. Shi, J. Liu, T. Chen, J. Wang and F. Yu, 12th International Conference on Intelligent Human-Machine Systems and Cybernetics (IHMSC), IEEE, 87-91 (2020), doi:10.1109/IHMSC49165.2020.10098. Spacers touching the glass off the axis.
- N. Schuster and J. Franks, "Passive athermalization of two-lens-designs in 8-12 micron waveband," Proc. SPIE 8353, 835325 (2012), doi:10.1117/12.918112.
- Y. Zhang, J.-Y. Shang, Y. Xu and W.-S. Wang, "Design of cooled athermalized infrared telephoto lens," Proc. SPIE 8557, 855716 (2012), doi:10.1117/12.999327.
- H. H. Hopkins, "The frequency response of a defocused optical system," Proc. R. Soc. Lond. A 231, 91-103 (1955), doi:10.1098/rspa.1955.0158.
- H. H. Hopkins, "Calculation of the aberrations and image assessment for a general optical system," Optica Acta 28, 667-714 (1981).
- W. T. Welford, *Aberrations of Optical Systems* (Adam Hilger, 1986).
- G. W. Forbes, "Optical system assessment for design: numerical ray tracing in the Gaussian pupil," J. Opt. Soc. Am. A 5, 1943-1956 (1988).
- M. J. Kidger, *Fundamental Optical Design* (SPIE Press, 2002), §4.5.3. The geometric MTF.

## Worked examples and checks

- R. E. Fischer, "Optical design for the infrared," Proc. SPIE 531, 82-120 (1985). A germanium soak, and the Ge/ZnSe doublet of the examples.
- R. Bardazzi, "Designing a 1.7-5.3 µm athermalized lens," Proc. SPIE 7113, 71130W (2008), doi:10.1117/12.799917. A germanium singlet.
- Itek Corporation, "Outer Planets Mission Television Subsystem Optics Study," Final Report 72-9320-1 for the Jet Propulsion Laboratory, NASA NAS7-100 (July 28, 1972), NASA N72-33381. The B4 Cassegrain of the examples, and its soak tables.
- S. Xu, X. Peng, G. Tie, C. Guan, H. Hu and Y. Xiong, "Athermalization of an optical system based on lens shape and assembly method," Curr. Opt. Photon. 3, 429-437 (2019), doi:10.3807/COPP.2019.3.5.429. An aluminum telescope.
- SCHOTT optical glass data sheets, "Temperature Coefficients of the Refractive Index," for N-FK51A, N-BK7, N-LASF9, N-SF6 and SF57, https://www.schott.com/en-us/products/optical-glass-p1000267/downloads.
- HOYA, NBFD27 data sheet (HOYA20250701M), https://www.hoya-opticalworld.com/common/pdf2021/NBFD27.pdf.
- Optiland 0.6.3, Kramer Harrison and contributors, https://github.com/HarrisonKramer/optiland (MIT license). An independent implementation of TIE-19, with the glasses of the refractiveindex.info database.

## Software

- AberrationCalculator, https://github.com/jaruiz6363/AberrationCalculator: the lens model, the lens-file and catalog readers, and paraxial and real ray tracing.
- WavefrontErrorCalculator, https://github.com/jaruiz6363/WavefrontErrorCalculator: the wavefront error and the diffraction and geometric MTF.

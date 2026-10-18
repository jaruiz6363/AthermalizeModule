# Theory

What the module computes, and the conventions it uses. The refractive index is in [materials.md](materials.md), and how a lens is built at a temperature is in [lens-at-temperature.md](lens-at-temperature.md). This document covers what is computed from that lens.

## Signs

- **Temperature.** ΔT is the temperature less the design's.
- **Focus.** The defocus is the paraxial focus less the image plane, along the axis, at the primary wavelength: positive when the image falls beyond the plane, away from the lens. With the image plane held by the mount, a negative change means the image moves toward the lens.
- **Thermal coefficients.**
  - x_f = α − (dn_rel/dT)/(n_rel − 1), the relative change of a thin lens's focal length per kelvin, positive when the focal length grows (Jamieson 1981).
  - γ = −x_f, the relative change of its power, as later authors write it (Liu et al. 2025).
  - ν_T = −1/x_f, Grey's (1948) thermal ν value, the thermal counterpart of the Abbe number.

  Each is quoted with its sign stated, because published tables use all three.

## The first-order report

At each temperature the report gives:
- the paraxial focal length and back focal length at the primary wavelength;
- the defocus, and its change from the design's temperature.

It sets the change against the depth of focus, ±2λN², a quarter wave of defocus. Here N is the working F-number, 1/(2n′|u′|) of the paraxial marginal ray in image space, and λ is the primary wavelength in the image space.

**Each part's share.** The rate of change of the defocus and of the focal length at the design's temperature is found with each part of the lens warmed alone, the rest held as designed. A part is:
- a glass, with its index, its thickness and the surfaces that grow with it;
- a mirror's substrate;
- the housing;
- a mount part.

Each rate is a central difference of ±0.5 K. The shares are exact derivatives of the full paraxial model, not a thin-lens estimate. To first order they add up to the whole lens's rate, which the report prints beside their sum.

For a thin lens the shares reduce to the classical ones: the glass's f·x_f, and the housing's −L·α_h, L being the length of housing between the lens and the image plane. A system of thin lenses gives Grey's sum of the shares weighted by the squared marginal-ray heights.

**First order and real rays.** The paraxial focus is not the best focus of an aberrated lens. Liu et al. (2025) found their paraxial model's defocus half the real-ray best focus's on a seven-element lens. The module therefore also gives the best focus by real rays, below, so the two can be compared at every temperature.

## Image quality

The wavefront error is computed by WavefrontErrorCalculator, as Hopkins and Welford define it: the chief ray's optical path minus each ray's, between the entrance and exit reference spheres, with real rays aimed at the real stop. It uses the lens at the temperature and its index tables.

- **The pupil integral** is Gauss quadrature (Forbes 1988): Gauss-Legendre in ρ² on 8 rings of 24 rays by default. It is exact for a wavefront polynomial up to ρ³⁰ and an azimuthal order of 23.
- **A central obstruction** of ε turns the quadrature onto the annulus ε ≤ ρ ≤ 1. The chief ray is still the ray through the pupil's center. The obstruction applies to the wavefront only; the geometric MTF is of the whole pupil.
- **RMS.** For each field and wavelength the RMS is the standard deviation of W over the pupil. They combine as √(Σ w_f w_λ W²_rms / Σ w_f w_λ), with the lens's own field and wavelength weights.
- **The RMS change** is the RMS of the difference between the wavefronts at the temperature and at the design's temperature, sample by sample. It leaves out the design's own aberrations.
- **The focus compensator** moves the image plane along the axis to the least RMS. Brent's method finds the minimum in a bracket a few depths of focus wide around the paraxial focus, widening the bracket while the minimum sits on its edge.
- **The geometric MTF** is WavefrontErrorCalculator's: the spot's Fourier transform along each azimuth, from Kidger's pupil pattern, weighted over the wavelengths. It is given at the fixed image plane and refocused.
- **The temperature range** over which the lowest MTF, over the fields and both azimuths at a frequency, stays at or above a limit (Schuster and Franks 2012). It is searched outward from the design's temperature in steps, and each end is found by linear interpolation. It is given at the fixed image plane and with the focus compensated at each temperature.

## References

- G. W. Forbes, "Optical system assessment for design: numerical ray tracing in the Gaussian pupil," J. Opt. Soc. Am. A 5(11), 1943-1956 (1988).
- D. S. Grey, "Athermalization of optical systems," J. Opt. Soc. Am. 38(6), 542-546 (1948).
- H. H. Hopkins, "Calculation of the aberrations and image assessment for a general optical system," Optica Acta 28(5), 667-714 (1981).
- T. H. Jamieson, "Thermal effects in optical systems," Opt. Eng. 20(2), 156-160 (1981).
- Y. Liu et al., Opt. Express 33(23), 49404-49422 (2025), doi:10.1364/OE.569582.
- N. Schuster and J. Franks, "Passive athermalization of two-lens-designs in 8-12 micron waveband," Proc. SPIE 8353, 835325 (2012), doi:10.1117/12.918112.
- W. T. Welford, *Aberrations of Optical Systems* (Adam Hilger, 1986).

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
- **A central obstruction** of ε turns the quadrature onto the annulus ε ≤ ρ ≤ 1. The chief ray is still the ray through the pupil's center. The obstruction applies to the wavefront only; the MTF is of the whole pupil.
- **RMS.** For each field and wavelength the RMS is the standard deviation of W over the pupil. They combine as √(Σ w_f w_λ W²_rms / Σ w_f w_λ), with the lens's own field and wavelength weights.
- **The RMS change** is the RMS of the difference between the wavefronts at the temperature and at the design's temperature, sample by sample. It leaves out the design's own aberrations.
- **The focus compensator** moves the image plane along the axis to the least RMS. Brent's method finds the minimum in a bracket a few depths of focus wide around the paraxial focus, widening the bracket while the minimum sits on its edge.
- **The MTF** is the diffraction MTF by default, WavefrontErrorCalculator's:
  - **The method.** It is the autocorrelation of the pupil function P = J^(−1/2) e^(−2πiW) in the image's spatial frequencies q = n′u⊥/λ, from the Debye integral. Each pupil point is paired with the point whose q is q − ν, found by tracing rays onto it, and whether it transmits by tracing it through the apertures. So off-axis and vignetted pupils need no rescaling of the frequency axis.
  - **Wavelengths.** Each wavelength's OTF is summed by its weight as complex numbers about the primary wavelength's image point, so lateral color lowers the MTF.
  - **The grid.** The outer integral is over a 64×64 grid of the pupil by default (`--grid`). The Debye integral needs a large Fresnel number, and a result below 10 is flagged.
  - **Why it matters.** In the thermal infrared, at 10 µm and F/1 to F/2, the blur is mostly diffraction, and the geometric MTF, which leaves diffraction out, reads near 1 where the true MTF is about half that.
  - **The geometric MTF** (`MtfKind.Geometric`, `--mtf geometric`) is the spot's Fourier transform along each azimuth, from Kidger's pupil pattern, weighted over the wavelengths: right only when the aberrations are large against the wavelength.

  Either MTF is given at the fixed image plane and refocused.
- **The temperature range** over which the lowest MTF, over the fields and both azimuths at a frequency, stays at or above a limit (Schuster and Franks 2012). It is searched outward from the design's temperature in steps, and each end is found by linear interpolation. It is given at the fixed image plane and with the focus compensated at each temperature.

## Athermalization

**The athermal map.** Over a band of three wavelengths, short, center and long, each material has:
- a chromatic power ω = (n_short − n_long)/(n_center − 1) = 1/ν;
- a partial dispersion P = (n_center − n_long)/(n_short − n_long);
- its γ and x_f at the center.

All are relative to the air, at fixed vacuum wavelengths. The map plots x_f against ω.

For thin lenses in contact, in a housing of α_h that holds the image plane at the focal distance, the lens is (Jiang and Liu 2024, eqs. 5-7):
- achromatic when Σ ω φ = 0;
- athermal when Σ (γ + α_h) φ = 0, that is Σ (x_f − α_h) φ = 0.

So an athermal achromatic doublet's two points lie on a line through the housing's point, (ω = 0, x_f = α_h).

- **The doublet.** The achromat of power φ from materials a and b has φ_a = φ ω_b/(ω_b − ω_a) and φ_b = −φ ω_a/(ω_b − ω_a). Its thermal residual R_th = Σ (γ + α_h) φ moves the focus from the image plane by −R_th/φ² per kelvin.
- **The triplet.** Three materials fix all three conditions: Σ φ = φ, Σ ω φ = 0 and Σ (γ + α_h) φ = 0, which Cramer's rule solves (Zhang et al. 2012, eqs. 6-8, there without the housing). There is no solution when the three points lie on one line.
- **Feasibility.** Lenses of given materials and power signs can be made achromatic and athermal only if the origin lies in the convex hull of their composite points (sign(φ) ω, sign(φ)(γ + α_h)). This is a necessary condition, not a sufficient one (Jiang and Liu 2024).
- **Jiang and Liu's objective** ranks glass sets at fixed powers (eqs. 15-19). Each residual is divided by an acceptable size and weighted by τ: thermal and chromatic, Petzval, and secondary spectrum. Their sum is multiplied by 1 + W_power Σ|Δφ′|/φ_T for the power a glass swap changes at fixed shape. The thermal residual uses φ′ = φh/h₁ and the chromatic φ″ = φ(h/h₁)², as the paper writes them.

**The search.** `MaterialSearch` tries every combination of candidate glasses, for chosen elements, and candidate mount materials, for the housing and each mount part (Liu et al. 2025's Cartesian product). It scores each on the full lens model at the design's shapes, not on a thin-lens table:
- the focus rate times the half-range ΔT, plus the paraxial axial color, both in depths of focus;
- times 1 + W_power |ΔEFL|/EFL, for the focal length the swap changes.

A swap changes the design, so the best combinations are starting points to optimize. Glass candidates can be drawn from a catalog near a glass, within |Δn_d| and |Δν_d|, as Jiang and Liu filter them.

**The temperature for one depth of focus.** The focus report also gives 2λN²/|d(defocus)/dT|, the temperature change that moves the focus by one depth of focus at the nominal rate: Schuster and Franks's (2012) eq. 4, 2λN²/(f(α_H + γ_T)) for a thin lens.

## References

- G. W. Forbes, "Optical system assessment for design: numerical ray tracing in the Gaussian pupil," J. Opt. Soc. Am. A 5(11), 1943-1956 (1988).
- D. S. Grey, "Athermalization of optical systems," J. Opt. Soc. Am. 38(6), 542-546 (1948).
- H. H. Hopkins, "Calculation of the aberrations and image assessment for a general optical system," Optica Acta 28(5), 667-714 (1981).
- T. H. Jamieson, "Thermal effects in optical systems," Opt. Eng. 20(2), 156-160 (1981).
- C. Jiang and M. Liu, "Athermalization of a lens system by glass selection using simulated annealing with memory augmentation," Opt. Express 32(8), 14860-14875 (2024), doi:10.1364/OE.521560.
- Y. Liu et al., Opt. Express 33(23), 49404-49422 (2025), doi:10.1364/OE.569582.
- N. Schuster and J. Franks, "Passive athermalization of two-lens-designs in 8-12 micron waveband," Proc. SPIE 8353, 835325 (2012), doi:10.1117/12.918112.
- W. T. Welford, *Aberrations of Optical Systems* (Adam Hilger, 1986).
- Y. Zhang, J.-Y. Shang, Y. Xu and W.-S. Wang, "Design of cooled athermalized infrared telephoto lens," Proc. SPIE 8557, 855716 (2012), doi:10.1117/12.999327.

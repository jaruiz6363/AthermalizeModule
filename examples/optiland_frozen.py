"""Open a frozen lens from `atherm lens --write ... .optiland.json` in Optiland, and analyze it.

    python optiland_frozen.py doublet-cold-vacuum.optiland.json

The lens is already at its temperature and pressure: its geometry grown, its wavelengths those
in its air, its glasses frozen at those conditions. So Optiland is used as it is, at its
defaults, with nothing to set. The glass catalogs written beside the lens, in the
.optiland_glass folder, are loaded into this session first; nothing is installed.
"""

import sys
from pathlib import Path

import numpy as np
import optiland.fileio as fio
from optiland.materials.registry import MaterialRegistry
from optiland.wavefront import Wavefront

sys.stdout.reconfigure(encoding="utf-8")   # for µ on any console
lens = Path(sys.argv[1])
glass = lens.with_name(lens.name.replace(".optiland.json", ".optiland_glass"))
for folder in glass.iterdir():
    if folder.is_dir():
        MaterialRegistry.instance().load_catalog(str(folder))   # once per session
optic = fio.load_optiland_file(str(lens))


def scalar(v):
    return float(np.ravel(v)[0])


z = [scalar(s.geometry.cs.z) for s in optic.surfaces.surfaces]
focus = scalar(optic.paraxial.F2())   # the paraxial focus, from the image surface
print(f"EFL {scalar(optic.paraxial.f2()):.6f} mm")
print(f"back focal distance {focus + z[-1] - z[-2]:.6f} mm")
print(f"paraxial focus from the image plane {focus * 1e3:.3f} µm")

waves = [w.value for w in optic.wavelengths.wavelengths]
print("wavelengths, in the lens's air, µm:", " ".join(f"{w:.6f}" for w in waves))
for i, s in enumerate(optic.surfaces.surfaces[1:-1], start=1):
    name = getattr(s.material_post, "name", None)
    if name:
        print(f"surface {i} {name}:", " ".join(f"{scalar(s.material_post.n(w)):.7f}" for w in waves))

# The RMS wavefront error at the primary wavelength at each of the lens's fields, at the image
# plane the mount holds, on a uniform pupil grid fine enough to have converged.
top = max(abs(f.y) for f in optic.fields.fields) or 1.0
fields = [(0.0, f.y / top) for f in optic.fields.fields]   # normalized, as Optiland takes them
primary = optic.primary_wavelength
wavefront = Wavefront(optic, fields=fields, wavelengths=[primary], num_rays=512, distribution="uniform")
for f in fields:
    opd = np.ravel(wavefront.get_data(f, primary).opd)
    print(f"field {f[1] * top:g}: RMS wavefront error {np.sqrt(np.mean((opd - opd.mean()) ** 2)):.5f} waves")

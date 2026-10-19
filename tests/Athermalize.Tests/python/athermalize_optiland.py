"""Optiland's index of a glass over temperature, for the cross-check in OptilandTests.cs.

Optiland reads its glasses from the refractiveindex.info database it ships with, as
MaterialFile objects; those with a SCHOTT thermal_dispersion block change with temperature by
SCHOTT's model (TIE-19). The C# side calls these functions through Python.NET, passing and
getting JSON so that the Python.NET surface stays small. Nothing in Optiland is modified.
"""

from __future__ import annotations

import glob
import json
import os

import numpy as np
import optiland
from optiland.materials import MaterialFile

_ROOT = os.path.join(os.path.dirname(optiland.__file__), "database", "data-nk", "glass")
_CACHE: dict[str, MaterialFile] = {}


def _material(path: str) -> MaterialFile:
    m = _CACHE.get(path)
    if m is None:
        m = MaterialFile(path)
        _CACHE[path] = m
    return m


def _scalar(value) -> float:
    """One index as a float; Optiland keeps the thermal constants as a column, so a thermal
    index comes back as a one-element array."""
    return float(np.ravel(np.asarray(value))[0])


def glasses() -> str:
    """Every database glass that Optiland changes with temperature: maker, name, file, its
    reference temperature, the six constants (D0, D1, D2, E0, E1, lambda_tk) and its
    wavelength range in micrometers."""
    out = []
    for path in sorted(glob.glob(os.path.join(_ROOT, "*", "*.yml"))):
        try:
            m = _material(path)
        except Exception:  # a file Optiland itself cannot read is not part of the check
            continue
        t0 = getattr(m, "_t0", None)
        coef = getattr(m, "thermdispcoef", None)
        if t0 is None or coef is None:
            continue
        c = [float(x[0]) for x in coef]
        if not any(c):
            continue
        lo, hi = getattr(m, "_formula_wavelength_range", None) or (0.0, 0.0)
        out.append({
            "maker": os.path.basename(os.path.dirname(path)),
            "name": os.path.splitext(os.path.basename(path))[0],
            "path": path,
            "t0": float(t0),
            "coefficients": c,
            "range": [float(lo), float(hi)],
        })
    return json.dumps(out)


def index(path: str, wavelengths_json: str, temperatures_json: str, pressure: float) -> str:
    """Optiland's index relative to the air at each temperature and the pressure (atm), for
    each wavelength (micrometers, in that air): a list per temperature."""
    m = _material(path)
    wavelengths = json.loads(wavelengths_json)
    return json.dumps([[_scalar(m.n(w, temperature=t, pressure=pressure)) for w in wavelengths]
                       for t in json.loads(temperatures_json)])


def catalog_index(path: str, wavelengths_json: str) -> str:
    """The catalog index, relative to air at the reference conditions, with no temperature
    given, at each wavelength (micrometers)."""
    m = _material(path)
    return json.dumps([_scalar(m.n(w)) for w in json.loads(wavelengths_json)])


def frozen_lens(path: str, rays: int = 512) -> str:
    """A frozen lens written by `atherm lens --write`, loaded by Optiland's own reader.

    Its glass catalogs, in the .optiland_glass folder beside it, are loaded into this session
    first; nothing is installed. Returns the focal length, the paraxial focus from the image
    plane, the back focal distance, the index after each surface at each wavelength, and the RMS
    wavefront error at the primary wavelength at each of the lens's fields, on a uniform pupil grid.
    """
    from pathlib import Path

    import optiland.fileio as fio
    from optiland.materials.registry import MaterialRegistry
    from optiland.wavefront import Wavefront

    lens = Path(path)
    glass = lens.with_name(lens.name.replace(".optiland.json", ".optiland_glass"))
    for d in glass.iterdir():
        if d.is_dir():
            MaterialRegistry.instance().load_catalog(str(d))
    optic = fio.load_optiland_file(str(lens))
    surfaces = optic.surfaces.surfaces
    z = [float(np.ravel(s.geometry.cs.z)[0]) for s in surfaces]
    focus = float(np.ravel(optic.paraxial.F2())[0])   # from the image surface
    waves = [w.value for w in optic.wavelengths.wavelengths]
    indices = [[float(np.ravel(s.material_post.n(w))[0]) for w in waves] for s in surfaces[1:-1]]
    top = max(abs(f.y) for f in optic.fields.fields) or 1.0
    fields = [(0.0, f.y / top) for f in optic.fields.fields]   # normalized, as Optiland takes them
    wl = optic.primary_wavelength
    wf = Wavefront(optic, fields=fields, wavelengths=[wl], num_rays=rays, distribution="uniform")
    rms = []
    for f in fields:
        opd = np.ravel(wf.get_data(f, wl).opd)
        rms.append(float(np.sqrt(np.mean((opd - opd.mean()) ** 2))))
    return json.dumps({
        "efl": float(np.ravel(optic.paraxial.f2())[0]),
        "focus": focus,
        "bfl": focus + z[-1] - z[-2],
        "indices": indices,
        "rms": rms,
    })

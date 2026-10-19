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

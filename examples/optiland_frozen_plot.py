"""Plot frozen lenses from `atherm lens --write ... .optiland.json`, analyzed in Optiland.

    python optiland_frozen_plot.py out.svg design.optiland.json "7.5 °C" warm.optiland.json "35 °C"

Each lens is opened in Optiland at its defaults, with nothing set, as optiland_frozen.py does. Two
panels, at the primary wavelength on a uniform pupil grid:
- the RMS wavefront error across the field, at the image plane the mount holds;
- through focus: the RMS over the lens's fields, by their weights, as the image plane moves, with
  each lens's best plane marked.
"""

import sys
from pathlib import Path

import matplotlib

matplotlib.use("svg")
import matplotlib.pyplot as plt
import numpy as np
import optiland.fileio as fio
from optiland.materials.registry import MaterialRegistry
from optiland.wavefront import Wavefront

sys.stdout.reconfigure(encoding="utf-8")
RAYS = 512


def load(path):
    lens = Path(path)
    glass = lens.with_name(lens.name.replace(".optiland.json", ".optiland_glass"))
    for folder in glass.iterdir():
        if folder.is_dir():
            MaterialRegistry.instance().load_catalog(str(folder))   # once per session
    return fio.load_optiland_file(str(lens))


def rms(optic, fields):
    """The RMS wavefront error at the primary wavelength at each field (normalized), in waves."""
    primary = optic.primary_wavelength
    wavefront = Wavefront(optic, fields=fields, wavelengths=[primary], num_rays=RAYS, distribution="uniform")
    out = []
    for f in fields:
        opd = np.ravel(wavefront.get_data(f, primary).opd)
        out.append(float(np.sqrt(np.mean((opd - opd.mean()) ** 2))))
    return np.array(out)


out = sys.argv[1]
lenses = [(load(p), label) for p, label in zip(sys.argv[2::2], sys.argv[3::2])]

plt.rcParams.update({"svg.fonttype": "none", "font.size": 10, "font.family": "sans-serif"})
fig, (across, through) = plt.subplots(1, 2, figsize=(10, 4.9))
colors = ["#2f6f99", "#c0392b", "#2e8b57", "#8e44ad"]

for (optic, label), color in zip(lenses, colors):
    top = max(abs(f.y) for f in optic.fields.fields) or 1.0
    weights = np.array([f.weight for f in optic.fields.fields], dtype=float)
    own = [(0.0, f.y / top) for f in optic.fields.fields]

    # Across the field, at the image plane the mount holds.
    sweep = np.linspace(0.0, 1.0, 11)
    across.plot(sweep * top, rms(optic, [(0.0, h) for h in sweep]), color=color, label=label)
    across.plot([f[1] * top for f in own], rms(optic, own), "o", color=color, markersize=5)

    # Through focus: the image plane moved by dz, the RMS over the fields by their weights.
    # The image surface alone is moved, along the axis; the rest of the lens is left as it is.
    plane = optic.surfaces.surfaces[-1].geometry.cs
    z0 = float(np.ravel(plane.z)[0])
    shifts = np.linspace(-120.0, 60.0, 19)   # µm
    total = []
    for dz in shifts:
        plane.z = z0 + dz * 1e-3
        total.append(float(np.sqrt(np.sum(weights * rms(optic, own) ** 2) / weights.sum())))
    plane.z = z0
    total = np.array(total)
    a, b, c = np.polyfit(shifts, total ** 2, 2)   # the squared RMS is quadratic in focus
    best = -b / (2 * a)
    least = float(np.sqrt(max(c - b * b / (4 * a), 0.0)))
    fine = np.linspace(shifts[0], shifts[-1], 200)
    through.plot(fine, np.sqrt(np.maximum(np.polyval([a, b, c], fine), 0.0)), color=color,
                 label=f"{label}: best plane {best:+.1f} µm, {least:.4f} waves")
    through.plot(shifts, total, "o", color=color, markersize=3)
    through.axvline(best, color=color, linestyle="--", linewidth=0.8)
    print(f"{label}: at the image plane {total[np.argmin(np.abs(shifts))]:.4f} waves; "
          f"best plane {best:+.1f} µm, {least:.4f} waves")

across.set_xlabel("field, degrees")
across.set_ylabel("RMS wavefront error, waves")
across.set_title("Across the field, at the image plane", fontsize=10)
across.set_ylim(bottom=0)
across.grid(True, color="#dddddd")
across.legend(frameon=False, loc="upper center", bbox_to_anchor=(0.5, -0.16), ncol=2, fontsize=9)
through.axvline(0.0, color="#888888", linewidth=0.8)
through.set_xlabel("image plane moved, µm")
through.set_ylabel("RMS wavefront error over the fields, waves")
through.set_title("Through focus", fontsize=10)
through.set_ylim(bottom=0)
through.grid(True, color="#dddddd")
through.legend(frameon=False, loc="upper center", bbox_to_anchor=(0.5, -0.16), fontsize=9)
fig.suptitle(f"Frozen lenses, opened in Optiland at its defaults: RMS at {float(lenses[0][0].primary_wavelength):g} µm", fontsize=11)
fig.tight_layout()
fig.savefig(out, facecolor="white", metadata={"Date": None})
print(f"Plot written to {out}.")

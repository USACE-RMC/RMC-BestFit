"""Render the verification report's explanatory plot from its saved R reference.

This plots independent reference calculations, not newly executed BestFit results.
Requires Python with NumPy and Matplotlib. The resulting SVG is committed with the report.
"""

import json
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
reference = json.loads(
    (ROOT / "verification/data/model-estimation/profile-likelihood-oracle.json")
    .read_text(encoding="utf-8")
)
rho = reference["fixture"]["rho"]
grid = reference["profile_grid"]
cutoff = reference["intervals"]["target_log_likelihood"]
x = np.linspace(-3.65, 3.65, 600)

plt.rcParams.update({
    "font.family": "DejaVu Sans", "font.size": 10,
    "axes.spines.top": False, "axes.spines.right": False,
    "svg.fonttype": "path", "svg.hashsalt": "bestfit-profile-reference",
})
fig, axes = plt.subplots(1, 2, figsize=(9.0, 3.35), gridspec_kw={"width_ratios": [1.65, 1]})
axes[0].plot(x, -0.5 * x**2, color="#245d7a", linewidth=2, label="Refit the second parameter")
axes[0].plot(x, -0.5 * x**2 / (1-rho**2), color="#9a5c30", linewidth=1.6,
             linestyle="--", label="Hold the second parameter fixed")
axes[0].scatter([p["theta1"] for p in grid], [p["true_profile_log_likelihood"] for p in grid],
                color="#245d7a", edgecolor="white", linewidth=0.5, s=25, zorder=3)
axes[0].axhline(cutoff, color="#687480", linewidth=1, linestyle=":")
axes[0].text(-3.55, cutoff + 0.14, "90% interval cutoff", color="#4b5964", fontsize=9)
axes[0].set(xlim=(-3.8, 3.8), ylim=(-7, 0.45), xlabel="First parameter",
            ylabel="Log likelihood relative to optimum")
axes[0].legend(loc="lower center", fontsize=8.4, frameon=False)
axes[0].grid(axis="y", color="#dce2e6", linewidth=0.6)

for y, key, label, color in [
    (1, "true_profile", "Refit", "#245d7a"),
    (0, "conditional_slice", "Hold fixed", "#9a5c30"),
]:
    low, high = reference["intervals"][key]
    axes[1].plot([low, high], [y, y], linewidth=4, color=color, solid_capstyle="round")
    axes[1].scatter([0], [y], s=28, facecolor="white", edgecolor=color, zorder=3)
    axes[1].text(0, y + 0.16, f"{low:.3f} to {high:.3f}", ha="center", fontsize=9, color=color)
axes[1].set(xlim=(-2, 2), ylim=(-0.5, 1.55), xlabel="First parameter", title="90% interval")
axes[1].set_yticks([0, 1], ["Hold fixed", "Refit"])
axes[1].spines["left"].set_visible(False)
axes[1].tick_params(axis="y", length=0)
fig.tight_layout(pad=1.2, w_pad=1.5)
destination = ROOT / "docs/verification/report/figures/profile-likelihood-reference.svg"
fig.savefig(destination, format="svg", metadata={"Date": None, "Creator": "RMC.BestFit report reference-figure builder"})
# Matplotlib leaves spaces at SVG path line endings; keep the committed asset clean.
destination.write_text(
    "\n".join(line.rstrip() for line in destination.read_text(encoding="utf-8").splitlines()) + "\n",
    encoding="utf-8", newline="\n",
)
plt.close(fig)
print(destination.relative_to(ROOT))

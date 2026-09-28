"""Render docs/images/site_plan.png from site_layout.json (single source of site coordinates)."""
import json
from pathlib import Path
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle

ROOT = Path(__file__).resolve().parents[2]
L = json.loads((Path(__file__).with_name("site_layout.json")).read_text())
FILL = {"asphalt": "#3a3d40", "concrete": "#b8b8b2", "stone": "#9a958c", "gravel": "#b3a58c", "trailer": "#e8e6df",
        "welfare": "#6f9fd8", "electrical": "#e0b000", "dumpster": "#2f6f4f", "stockpile": "#8a6a45", "building": "#c9c3b5",
        "mats": "#7a5c3a", "trench": "#2b2118", "laydown": "#d8cfbf", "locates": "#ffffff", "conex": "#4f6d8f", "fuel": "#c0392b"}

fig, ax = plt.subplots(figsize=(13, 10.5))
ax.add_patch(Rectangle((0, 0), *L["parcel"], facecolor="#a8916f", edgecolor="#1f7a3a", lw=3, zorder=0))  # graded dirt + fence
for r in L["rects"]:
    ax.add_patch(Rectangle((r["x"], r["y"]), r["w"], r["h"], facecolor=FILL[r["kind"]], edgecolor="#222", lw=0.8, zorder=2,
                           hatch="xx" if r["kind"] == "locates" else None))
    ax.text(r["x"] + r["w"] / 2, r["y"] + r["h"] / 2, r["label"], ha="center", va="center", fontsize=7.5, zorder=5,
            color="white" if r["kind"] in ("asphalt", "trench", "dumpster", "conex", "fuel") else "#111")
xs, ys = zip(*L["haul_road"] + [L["haul_road"][0]])
ax.plot(xs, ys, color="#6b5a45", lw=14, alpha=0.55, zorder=1, solid_capstyle="round")
ax.plot(*zip(*L["walkway"]), color="#ff7a00", lw=2, ls="--", zorder=3, label="Pedestrian walkway")
line = L["power_line"]
ax.plot([-5, 95], [line["y"]] * 2, color="#111", lw=1.5, zorder=4, label="Overhead line (~13 kV)")
ax.scatter(line["poles_x"], [line["y"]] * len(line["poles_x"]), s=40, color="#5b3a1a", zorder=5)
ax.plot(*zip(*L["silt_fence"]), color="#222", lw=1, ls=":", zorder=4, label="Silt fence")
s = L["spawn"]; ax.scatter([s["x"]], [s["y"]], marker="^", s=120, color="#e0342c", zorder=6, label="Spawn (gate)")
ax.set_xlim(-10, 100); ax.set_ylim(-12, 74); ax.set_aspect("equal")
ax.set_title("Municipal Pump Station & Stormwater Tie-in — Site Logistics Plan (m)", fontsize=12)
ax.legend(loc="upper left", fontsize=8, framealpha=0.9); ax.grid(alpha=0.2)
out = ROOT / "docs" / "images" / "site_plan.png"; out.parent.mkdir(exist_ok=True)
fig.savefig(out, dpi=130, bbox_inches="tight"); print(out)

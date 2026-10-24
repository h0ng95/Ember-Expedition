"""Mirror ForestEnemyArtValidation.BuildManifest without changing source pixels."""

import json
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / "unity/Assets/Resources/Art/Enemies/v029"
SPECIES = ("frost-guard", "frost-acolyte", "frost-cantor", "frost-boss")
CUTOFF = 102


def clear_gap(profile, desired, radius, label):
    start = max(1, round(desired) - radius)
    stop = min(len(profile) - 2, round(desired) + radius)
    candidates = []
    position = start
    while position <= stop:
        if profile[position] != 0:
            position += 1
            continue
        first = position
        while position <= stop and profile[position] == 0:
            position += 1
        last = position - 1
        candidates.append((first, last))
    if not candidates:
        raise ValueError(f"{label}: no transparent gutter near {desired}")
    first, last = min(candidates, key=lambda run: (
        -(run[1] - run[0]), abs((run[0] + run[1]) / 2 - desired)))
    return round((first + last) / 2)


def inspect(species):
    path = FOLDER / f"{species}.png"
    with Image.open(path) as source:
        if source.mode != "RGBA":
            raise ValueError(f"{path.name}: expected RGBA")
        pixels = np.asarray(source)
    height, width = pixels.shape[:2]
    mask = pixels[:, :, 3] > CUTOFF
    row_profile = mask.sum(axis=1)
    rows = [0] + [clear_gap(row_profile, height * row / 4, height // 12,
        f"{species} row {row}") for row in range(1, 4)] + [height]
    columns = []
    frames = []
    centers = []
    for row in range(4):
        top, bottom = rows[row:row + 2]
        profile = mask[top:bottom].sum(axis=0)
        cuts = [0] + [clear_gap(profile, width * col / 6, width // 24,
            f"{species} row {row} col {col}") for col in range(1, 6)] + [width]
        columns.extend(cuts)
        for col in range(6):
            left, right = cuts[col:col + 2]
            bottom_y, top_y = height - bottom, height - top
            # Source pixel array is top-origin; returned frame coordinates are bottom-origin.
            ys, xs = np.nonzero(mask[top:bottom, left:right])
            index = row * 6 + col
            if len(xs) == 0:
                raise ValueError(f"{species} frame {index}: empty")
            min_x, max_x = left + int(xs.min()), left + int(xs.max())
            min_y, max_y = bottom_y + int((bottom - top - 1) - ys.max()), \
                bottom_y + int((bottom - top - 1) - ys.min())
            if (col > 0 and min_x == left or col < 5 and max_x == right - 1 or
                row > 0 and max_y == top_y - 1 or row < 3 and min_y == bottom_y):
                raise ValueError(f"{species} frame {index}: touches occupied seam")
            frames.append({"x": min_x, "y": min_y, "width": max_x - min_x + 1,
                "height": max_y - min_y + 1})
            if index < 4 or 6 <= index <= 11:
                centers.append((min_x + max_x + 1) / 2 - col * width / 6)
    centers.sort()
    return {"id": species, "resourcePath": f"Art/Enemies/v029/{species}",
        "width": width, "height": height, "anchorX": (centers[4] + centers[5]) / 2,
        "referenceHeight": float(frames[0]["height"]), "rowCuts": rows,
        "columnCuts": columns, "frames": frames}, float(np.mean(pixels[:, :, 3] == 0))


def main():
    sheets = []
    for species in SPECIES:
        sheet, transparent = inspect(species)
        sheets.append(sheet)
        print(f"{species}: {sheet['width']}x{sheet['height']}, "
            f"alpha0={transparent:.3f}, rows={sheet['rowCuts']}, "
            f"idleHeight={sheet['referenceHeight']}, anchorX={sheet['anchorX']}")
    manifest = {"version": "0.29", "sheets": sheets}
    path = FOLDER / "frost-manifest.json"
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=4) + "\n", encoding="utf-8")
    print(path)


if __name__ == "__main__":
    main()

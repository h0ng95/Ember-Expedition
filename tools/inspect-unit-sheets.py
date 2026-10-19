"""Inspect generated RGBA sheets and write crop/pivot metadata; never edit pixels.

Usage: python tools/inspect-unit-sheets.py
Each source sheet is 6 columns / 4 rows: idle4, hit2, walk6, attack6, death4.
Generation can shift the gutters; detect clear bands, then preserve original pixels.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'unity/Assets/Resources/Art/Units/v010'
DOC = ROOT / 'docs/design-v0.10'


def gap(profile, desired, radius):
    low = max(1, int(desired - radius))
    high = min(len(profile) - 1, int(desired + radius))
    section = profile[low:high]
    minimum = int(section.min())
    candidates = np.flatnonzero(section == minimum) + low
    runs = np.split(candidates, np.flatnonzero(np.diff(candidates) > 1) + 1)
    # Favor a wide empty band over a single accidental minimum inside a sprite.
    chosen = min(runs, key=lambda run: (-(len(run) if minimum == 0 else 0), abs(float(run.mean()) - desired)))
    return int(round(float(chosen.mean()))), minimum


def inspect(path):
    with Image.open(path) as image:
        image.load()
        width, height = image.size
        if image.mode != 'RGBA':
            raise ValueError(f'{path.name}: RGBA required')
        pixels = np.array(image)
    alpha = pixels[:, :, 3]
    mask = alpha > 32
    yprofile = mask.sum(axis=1)
    ycuts = [0]
    seams = []
    for row in range(1, 4):
        cut, occupied = gap(yprofile, height * row / 4, height / 16)
        ycuts.append(cut)
        seams.append({'axis': 'y', 'cut': cut, 'occupied': occupied})
    ycuts.append(height)
    frames = []
    cells = []
    problems = []
    hashes = []
    for row in range(4):
        top, bottom = ycuts[row:row + 2]
        profile = mask[top:bottom].sum(axis=0)
        xcuts = [0]
        for column in range(1, 6):
            cut, occupied = gap(profile, width * column / 6, width / 24)
            xcuts.append(cut)
            seams.append({'axis': 'x', 'row': row, 'cut': cut, 'occupied': occupied})
        xcuts.append(width)
        for column in range(6):
            left, right = xcuts[column:column + 2]
            local = mask[top:bottom, left:right]
            yy, xx = np.where(local)
            index = row * 6 + column
            count = len(xx)
            cell = {'index': index, 'pixels': count}
            cells.append(cell)
            if index >= 22:
                if count > 30:
                    problems.append(f'Unused cell {index} contains {count} visible pixels')
                continue
            if count < 100:
                problems.append(f'Missing frame {index}: only {count} visible pixels')
                frames.append({'x': left, 'y': height-bottom, 'width': right-left, 'height': bottom-top, 'pivotX': .5, 'pivotY': .16})
                continue
            x0 = max(left, left + int(xx.min()) - 2)
            x1 = min(right, left + int(xx.max()) + 3)
            y0 = max(top, top + int(yy.min()) - 2)
            y1 = min(bottom, top + int(yy.max()) + 3)
            if int(xx.min()) == 0 and column > 0 or int(xx.max()) == right-left-1 and column < 5:
                problems.append(f'Frame {index} reaches horizontal crop boundary')
            if int(yy.min()) == 0 and row > 0 or int(yy.max()) == bottom-top-1 and row < 3:
                problems.append(f'Frame {index} reaches vertical crop boundary')
            anchor_x = width * (column + .5) / 6
            foot_y = top + int(yy.max())
            frames.append({
                'x': x0, 'y': height-y1, 'width': x1-x0, 'height': y1-y0,
                'pivotX': round((anchor_x-x0)/(x1-x0), 6),
                'pivotY': round((y1-foot_y)/(y1-y0), 6)
            })
            import hashlib
            hashes.append(hashlib.sha256(pixels[y0:y1,x0:x1].tobytes()).hexdigest())
    if not (alpha.min() == 0 and np.mean(alpha == 0) > .2):
        problems.append('Insufficient fully transparent background')
    if len(set(hashes)) < 18:
        problems.append('Many exact duplicate frame crops; inspect motion')
    entry = {
        'id': path.stem, 'resourcePath': 'Art/Units/v010/'+path.stem,
        'width': width, 'height': height, 'frames': frames,
        'status': 'generated-needs-motion-review' if not problems else 'needs-crop-review'
    }
    report = {'id': path.stem, 'size': [width,height], 'transparentFraction': round(float(np.mean(alpha == 0)),4),
              'alphaRange': [int(alpha.min()),int(alpha.max())], 'frameCount': len(frames),
              'uniqueExactCrops': len(set(hashes)), 'rowCuts': ycuts, 'cells': cells,
              'occupiedSeams': [s for s in seams if s['occupied'] > 0], 'issues': problems}
    return entry, report


def main():
    ART.mkdir(parents=True, exist_ok=True)
    DOC.mkdir(parents=True, exist_ok=True)
    entries, reports = [], []
    for path in sorted(ART.glob('*.png')):
        entry, report = inspect(path)
        entries.append(entry)
        reports.append(report)
    payload = {'version':'0.10-art-pass1', 'frameOrder':{'idle':[0,1,2,3], 'hit':[4,5], 'walk':[6,7,8,9,10,11], 'attack':[12,13,14,15,16,17], 'death':[18,19,20,21]}, 'entries':entries}
    for destination in [ART/'art-manifest.json', DOC/'art-manifest.json']:
        destination.write_text(json.dumps(payload, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    (DOC/'art-inspection.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'sheets':len(entries),'frames':sum(len(e['frames']) for e in entries),'flagged':[{r['id']:r['issues']} for r in reports if r['issues']]},ensure_ascii=False))


if __name__ == '__main__':
    main()

"""Generate original, seamlessly tiling road detail textures for Enhanced Graphics and Textures.

Run with Python, NumPy, SciPy and Pillow. The generated PNGs are embedded in
the mod assembly; no game textures are copied or altered on disk.
"""

from pathlib import Path
import numpy as np
from scipy.ndimage import gaussian_filter
from PIL import Image, ImageDraw

SIZE = 1024
OUT = Path(__file__).resolve().parents[1] / "Enhanced Graphics and Textures" / "Assets" / "Textures" / "Road"
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(712094)


def noise(sigma):
    field = gaussian_filter(rng.normal(size=(SIZE, SIZE)).astype(np.float32), sigma, mode="wrap")
    return field / (field.std() + 1e-6)


fine = noise(0.65)
medium = noise(4)
large = noise(35)


def cracks(count):
    canvas = Image.new("L", (SIZE, SIZE))
    draw = ImageDraw.Draw(canvas)
    for _ in range(count):
        x = float(rng.integers(0, SIZE))
        y = float(rng.integers(0, SIZE))
        angle = rng.uniform(0, 2 * np.pi)
        path = [(x, y)]
        for _ in range(int(rng.integers(7, 19))):
            angle += rng.normal(0, 0.23)
            length = rng.uniform(5, 14)
            x += np.cos(angle) * length
            y += np.sin(angle) * length
            path.append((x, y))
        shade = int(rng.integers(160, 240))
        width = int(rng.integers(1, 3))
        for offset_x in (-SIZE, 0, SIZE):
            for offset_y in (-SIZE, 0, SIZE):
                shifted = [(px + offset_x, py + offset_y) for px, py in path]
                draw.line(shifted, fill=shade, width=width, joint="curve")
    return gaussian_filter(np.asarray(canvas, dtype=np.float32) / 255, 0.65, mode="wrap")


def save(level, count, crack_depth):
    crack = cracks(count)
    grain = 5.0 * fine + 3.5 * medium + 3.0 * large
    base = np.clip(86 + grain - crack * (20 + level * 10), 1, 250)
    # Subtle stone flecks; no painted lane markings, curbs, or sidewalk edges.
    stone = (fine > 1.6) * (1.7 + level * 0.35)
    base = np.clip(base + stone, 0, 255).astype(np.uint8)
    albedo = np.stack([base, np.clip(base + 1, 0, 255), np.clip(base + 2, 0, 255),
                       np.full_like(base, 255)], axis=-1)

    height = 0.72 * fine + 0.42 * medium - crack * crack_depth
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.13
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.13
    normal = np.stack([-dx, -dy, np.ones_like(dx)], axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    normal = np.clip((normal * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
    normal = np.concatenate([normal, np.full((SIZE, SIZE, 1), 255, dtype=np.uint8)], axis=-1)
    Image.fromarray(albedo, "RGBA").save(OUT / f"RoadAsphalt{level}_BaseColor.png")
    Image.fromarray(normal, "RGBA").save(OUT / f"RoadAsphalt{level}_Normal.png")


for strength, lines, depth in ((1, 22, 2.0), (2, 42, 3.0), (3, 68, 4.0)):
    save(strength, lines, depth)


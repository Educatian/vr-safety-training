"""Turn a Higgsfield 'tileable' albedo into a truly seamless 1K albedo + normal map.
Seam fix: blend the image with a half-offset copy using a centre-weighted mask, so the
original's edge seams are replaced by the copy's (seam-free) middle. Normal from luminance (Sobel)."""
import sys
import numpy as np
from PIL import Image

src, name = sys.argv[1], sys.argv[2]
strength = float(sys.argv[3]) if len(sys.argv) > 3 else 3.0
img = np.asarray(Image.open(src).convert("RGB").resize((1024, 1024), Image.LANCZOS)).astype(np.float32)
h, w, _ = img.shape
rolled = np.roll(np.roll(img, h // 2, 0), w // 2, 1)
yy, xx = np.mgrid[0:h, 0:w]
wx = 1 - np.abs(xx - w / 2) / (w / 2); wy = 1 - np.abs(yy - h / 2) / (h / 2)
mask = np.clip(np.minimum(wx, wy) * 3, 0, 1)[..., None]      # 1 in the middle, 0 at the edges
albedo = img * mask + rolled * (1 - mask)
Image.fromarray(albedo.clip(0, 255).astype(np.uint8)).save(f"Assets/_Game/Art/Textures/HF/T_{name}_BaseMap.png")

lum = (albedo @ np.array([0.299, 0.587, 0.114])) / 255.0
gx = np.roll(lum, -1, 1) - np.roll(lum, 1, 1); gy = np.roll(lum, -1, 0) - np.roll(lum, 1, 0)
n = np.dstack([-gx * strength, gy * strength, np.ones_like(lum)])
n /= np.linalg.norm(n, axis=2, keepdims=True)
Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8)).save(f"Assets/_Game/Art/Textures/HF/T_{name}_Normal.png")
print("ok", name)

# Rug textures from photos of real carpets (museum and archive photos, public domain or CC0).
#
# Every texture is a real carpet: the photo is cut out of its background, turned upright if it is
# wider than tall, and fitted inside the rug's shape without stretching. The carpet's main field
# colour (the most common colour in its middle) is tinted with the rug's material: in the mask
# (<name>_m.png) red = tinted, black = the carpet's own colours. Nothing is drawn or invented.
#   python3 make_rugs_photo.py <picks_resolved.json> <Textures/IranianRugs>
import json, os, sys
import numpy as np
from PIL import Image, ImageFilter

# texture size and the box the carpet must fit in (same boxes as the drawn versions)
SIZES = {"Small": ((256, 256), (67, 54, 189, 202)),
         "Medium": ((256, 256), (2, 2, 254, 254)),
         "Large": ((384, 384), (66, 3, 319, 381))}
WORK = 1200                                   # longest side while cutting out


def cut_out(im):
    """Carpet without the photo's background: (cropped RGB, alpha).

    Woven pile is busy at every scale while backdrops, frames and margins are smooth, so the carpet is
    the longest run of rows (and of columns) where most pixels have strong local detail."""
    g = np.asarray(im.convert("L"), dtype=np.float32)
    gr = np.zeros_like(g)
    gr[1:-1, 1:-1] = np.abs(g[2:, 1:-1] - g[:-2, 1:-1]) + np.abs(g[1:-1, 2:] - g[1:-1, :-2])
    busy = np.asarray(Image.fromarray(np.clip(gr, 0, 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(6)), dtype=np.float32)
    on = busy > max(6.0, np.percentile(busy, 60) * 0.35)
    # plain, unpatterned stretches of field are smooth too: they still differ in colour from the backdrop
    a = np.asarray(im, dtype=np.float32)
    edge = np.concatenate([a[:4].reshape(-1, 3), a[-4:].reshape(-1, 3), a[:, :4].reshape(-1, 3), a[:, -4:].reshape(-1, 3)])
    on |= np.linalg.norm(a - np.median(edge, axis=0), axis=2) > 45

    def run(frac):
        best, start, cur = (0, 0), None, 0
        frac = np.convolve(frac, np.ones(25) / 25, mode="same")   # bridge worn or plain stretches
        for i, v in enumerate(list(frac > 0.4) + [False]):
            if v and start is None: start = i
            if not v and start is not None:
                if i - start > best[1] - best[0]: best = (start, i)
                start = None
        return best

    y0, y1 = run(on.mean(axis=1))
    x0, x1 = run(on[y0:y1].mean(axis=0))
    if y1 - y0 < g.shape[0] * 0.3 or x1 - x0 < g.shape[1] * 0.3:
        return im, Image.new("L", im.size, 255)
    pad = 7                                                  # the blur spreads detail a few pixels outward
    box = (x0 + pad, y0 + pad, x1 - pad, y1 - pad)
    crop = np.asarray(im.crop(box), dtype=np.float32)
    # leftover strips of backdrop or frame at the edges are one flat colour; woven edges never are
    flat = lambda line: line.std(axis=0).mean() < 10
    t, b, l, r = 0, crop.shape[0], 0, crop.shape[1]
    for _ in range(60):
        moved = False
        if flat(crop[t, l:r]): t += 1; moved = True
        if flat(crop[b - 1, l:r]): b -= 1; moved = True
        if flat(crop[t:b, l]): l += 1; moved = True
        if flat(crop[t:b, r - 1]): r -= 1; moved = True
        if not moved: break
    crop = im.crop((box[0] + l, box[1] + t, box[0] + r, box[1] + b))
    return crop, Image.new("L", crop.size, 255)


def field_mask(rgb):
    """0..1 weight of how close each pixel is to the carpet's main field colour."""
    a = np.asarray(rgb, dtype=np.float32) / 255
    h, w, _ = a.shape
    mid = a[h // 4: 3 * h // 4, w // 4: 3 * w // 4].reshape(-1, 3)
    q = (mid * 15).round().astype(int)                       # coarse colour bins, take the biggest
    keys, counts = np.unique(q[:, 0] * 256 + q[:, 1] * 16 + q[:, 2], return_counts=True)
    top = keys[counts.argmax()]
    sel = mid[(q[:, 0] * 256 + q[:, 1] * 16 + q[:, 2]) == top]
    centre = sel.mean(axis=0)
    for _ in range(4):                                        # settle on the field's real mean
        d = np.linalg.norm(mid - centre, axis=1)
        centre = mid[d < 0.12].mean(axis=0)
    # A field keeps its hue across the carpet while wear and the photo's lighting shift its brightness and
    # saturation, so match hue closely and the other two loosely, then clean up the speckle.
    def hsv(x):
        mx, mn = x.max(axis=-1), x.min(axis=-1)
        d = mx - mn + 1e-6
        r, g, b = x[..., 0], x[..., 1], x[..., 2]
        hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) / 6
        return hue, d / (mx + 1e-6), mx
    H, Sa, V = hsv(a)
    core = mid[np.linalg.norm(mid - centre, axis=1) < 0.12]             # the field's own pixels, to learn its spread
    ch, cs, cv = hsv(core)
    hc, sc_, vc = hsv(centre[None])
    hc, sc_, vc = float(hc[0]), float(sc_[0]), float(vc[0])
    dh = lambda x: np.abs((x - hc + 0.5) % 1 - 0.5)
    tol_h = dh(ch).std() * 3 + 0.02
    tol_s = cs.std() * 3 + 0.15
    tol_v = cv.std() * 3 + 0.15
    weight_h = 1.0 if sc_ > 0.15 else 0.0                                 # hue means nothing on greys
    score = weight_h * dh(H) / tol_h + np.abs(Sa - sc_) / tol_s + np.abs(V - vc) / tol_v
    raw = Image.fromarray((np.clip(1.6 - score, 0, 1) * 255).astype(np.uint8))
    clean = raw.filter(ImageFilter.MedianFilter(5)).point(lambda v: 255 if v > 110 else 0).filter(ImageFilter.GaussianBlur(0.6))
    wgt = np.asarray(clean, dtype=np.float32) / 255
    return wgt, centre


def build(photo, size, box, tint=True):
    im = Image.open(photo).convert("RGB")
    im.thumbnail((WORK, WORK), Image.LANCZOS)
    rgb, alpha = cut_out(im)
    if rgb.width > rgb.height and box[2] - box[0] < box[3] - box[1] + 1:
        rgb, alpha = rgb.rotate(90, expand=True), alpha.rotate(90, expand=True)
    bw, bh = box[2] - box[0], box[3] - box[1]
    s = min(bw / rgb.width, bh / rgb.height)
    nw, nh = max(1, round(rgb.width * s)), max(1, round(rgb.height * s))
    rgb, alpha = rgb.resize((nw, nh), Image.LANCZOS), alpha.resize((nw, nh), Image.LANCZOS)
    wgt, centre = field_mask(rgb)
    if not tint: wgt = np.zeros_like(wgt)                      # field can't be told apart cleanly: real colours only
    a = np.asarray(rgb, dtype=np.float32) / 255
    lum = a @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    flum = float(centre @ np.array([0.299, 0.587, 0.114]))
    shade = float(np.clip(flum * 1.6, 0.4, 0.92))             # the field keeps the carpet's own lightness
    grey = np.clip(shade + (lum / max(flum, 0.05) - 1) * shade * 0.45, 0, 1)   # wear and weave kept, softened
    out = a * (1 - wgt[..., None]) + grey[..., None] * wgt[..., None]
    tex = Image.new("RGBA", size, (0, 0, 0, 0))
    msk = Image.new("RGBA", size, (0, 0, 0, 0))
    ox, oy = box[0] + (bw - nw) // 2, box[1] + (bh - nh) // 2
    al = np.asarray(alpha, dtype=np.uint8)
    t = np.dstack([(out * 255).round().astype(np.uint8), al])
    m = np.dstack([(wgt * 255).round().astype(np.uint8), np.zeros_like(al), np.zeros_like(al), al])
    tex.paste(Image.fromarray(t, "RGBA"), (ox, oy))
    msk.paste(Image.fromarray(m, "RGBA"), (ox, oy))
    return tex, msk, float(wgt.mean())


if __name__ == "__main__":
    picks = json.load(open(sys.argv[1]))
    out = sys.argv[2]
    for name, (size, box) in SIZES.items():
        d = os.path.join(out, name)
        os.makedirs(d, exist_ok=True)
        for f in os.listdir(d):
            if f.endswith(".png"): os.remove(os.path.join(d, f))
        for p in picks:
            tex, msk, share = build(p["file"], size, box, p.get("tint", True))
            base = os.path.join(d, f"Rug{name}_{p['n']:02d}_{p['tradition']}.png")
            tex.save(base)
            msk.save(base.replace(".png", "_m.png"))
            print(f"{name} {p['n']:02d} {p['tradition']:<9} field tinted: {share:.0%}")

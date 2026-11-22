#!/usr/bin/env python3
"""Icône d'application de KevChess, dessinée par code (à valider par Kevin).

Un roi en cristal néon (contour cyan, reflet magenta) sur un fond nuit avec une grille d'échiquier en
perspective, dans la direction artistique « cristal / néon » du jeu. Tout est calculé en champs de
distance signée, sans aucune image importée : relancer le script redonne exactement les mêmes PNG.

Sorties (dans Assets/KevChess/AppIcon/) :
  - icon-legacy-<taille>.png    icône classique (carré arrondi), 192/144/96/72/48/36 px
  - icon-round-<taille>.png     icône ronde, mêmes tailles
  - icon-adaptive-bg-<taille>.png / icon-adaptive-fg-<taille>.png
                                calques de l'icône adaptative Android (fond / premier plan transparent),
                                432/324/216/162/108/81 px ; le premier plan tient dans la zone sûre
                                (cercle de 66/108 du côté)
  - icon-store-512.png          icône de la fiche Google Play (512 px, carré plein)
  - icon-preview.png            planche de contrôle : classique, ronde, adaptative vue en cercle (non utilisée par le build)
Pour chaque PNG, un .meta Unity est créé s'il manque, avec un GUID dérivé du nom du fichier (stable),
que ProjectSettings/ProjectSettings.asset référence pour l'icône Android.

Usage : python3 tools/generate-app-icon.py   (Python 3, numpy et Pillow)
"""
import hashlib
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "KevChess", "AppIcon")
MASTER = 1024          # côté du rendu de référence, réduit ensuite par moyenne de blocs
SSAA = 2               # suréchantillonnage du rendu de référence

CYAN = np.array([0.30, 0.92, 1.00])     # UIKit.Cyan
MAGENTA = np.array([1.00, 0.30, 0.85])  # UIKit.Magenta
NIGHT = np.array([0.02, 0.02, 0.05])    # fond de l'écran de démarrage
VIOLET = np.array([0.16, 0.08, 0.30])

LEGACY_SIZES = [192, 144, 96, 72, 48, 36]
ADAPTIVE_SIZES = [432, 324, 216, 162, 108, 81]
ADAPTIVE_SCALE = 0.78  # la pièce entière (croix et socle) tient dans le cercle sûr de 66/108


# ---------------------------------------------------------------- champs de distance (coordonnées unité, y vers le haut)

def sd_box(x, y, cx, cy, hw, hh, r=0.0):
    dx = np.abs(x - cx) - hw + r
    dy = np.abs(y - cy) - hh + r
    return np.hypot(np.maximum(dx, 0), np.maximum(dy, 0)) + np.minimum(np.maximum(dx, dy), 0) - r


def sd_circle(x, y, cx, cy, r):
    return np.hypot(x - cx, y - cy) - r


def sd_trapezoid(x, y, cy, h, w_bottom, w_top, cx=0.5):
    """Trapèze symétrique centré en (cx, cy), demi-hauteur h, demi-largeurs en bas et en haut."""
    px = np.abs(x - cx)
    py = y - cy
    t = np.clip((py + h) / (2 * h), 0, 1)
    edge = w_bottom + (w_top - w_bottom) * t
    # distance approchée, suffisante pour un rendu lissé (pente faible)
    slope = (w_top - w_bottom) / (2 * h)
    dx = (px - edge) / np.sqrt(1 + slope * slope)
    dy = np.abs(py) - h
    return np.hypot(np.maximum(dx, 0), np.maximum(dy, 0)) + np.minimum(np.maximum(dx, dy), 0)


def smooth_union(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def king(x, y):
    """Silhouette du roi, centrée, dans [0.27, 0.73] × [0.17, 0.85] (zone sûre de l'icône adaptative)."""
    base = sd_box(x, y, 0.5, 0.215, 0.205, 0.045, 0.03)
    plinth = sd_box(x, y, 0.5, 0.285, 0.165, 0.03, 0.02)
    body = sd_trapezoid(x, y, 0.445, 0.14, 0.135, 0.07)
    collar = sd_box(x, y, 0.5, 0.6, 0.13, 0.026, 0.02)
    head = sd_trapezoid(x, y, 0.675, 0.055, 0.085, 0.125)
    cap = sd_circle(x, y, 0.5, 0.735, 0.055)
    cross_v = sd_box(x, y, 0.5, 0.8, 0.022, 0.06, 0.012)
    cross_h = sd_box(x, y, 0.5, 0.815, 0.058, 0.02, 0.012)
    d = smooth_union(base, plinth, 0.02)
    d = smooth_union(d, body, 0.03)
    d = smooth_union(d, collar, 0.015)
    d = smooth_union(d, head, 0.02)
    d = np.minimum(d, cap)
    d = np.minimum(d, np.minimum(cross_v, cross_h))
    return d


def grid(x, y):
    """Grille d'échiquier en perspective sur le bas de l'icône : intensité des lignes et damier."""
    horizon = 0.42
    below = np.clip((horizon - y) / horizon, 0, 1)            # 0 à l'horizon, 1 en bas
    depth = 1.0 / np.maximum(below, 1e-3)                    # profondeur projetée
    u = (x - 0.5) * depth * 1.6
    v = depth * 0.9
    fu = np.abs(u - np.round(u))
    fv = np.abs(v - np.round(v))
    width = 0.03 * depth                                     # lignes plus fines au loin
    lines = np.exp(-(np.minimum(fu, fv) / np.maximum(width, 1e-4)) ** 2)
    checker = ((np.floor(u) + np.floor(v)) % 2 == 0).astype(float)
    fade = below ** 1.4
    return lines * fade * (y < horizon), checker * fade * (y < horizon)


# ---------------------------------------------------------------- rendu

def coords(n):
    t = (np.arange(n) + 0.5) / n
    x, y = np.meshgrid(t, 1.0 - t)
    return x, y


def render_background(n):
    x, y = coords(n)
    r = np.hypot(x - 0.5, y - 0.55)
    glow = np.clip(1.0 - r / 0.75, 0, 1) ** 1.6
    rgb = NIGHT[None, None, :] + VIOLET[None, None, :] * glow[..., None]
    lines, checker = grid(x, y)
    rgb = rgb + 0.035 * checker[..., None] * CYAN
    t = np.clip((x - 0.15) / 0.7, 0, 1)[..., None]
    line_color = CYAN * (1 - t) + MAGENTA * t
    rgb = rgb + 0.55 * lines[..., None] * line_color
    # halo sous la pièce, comme une case éclairée
    pool = np.exp(-(((x - 0.5) / 0.26) ** 2 + ((y - 0.2) / 0.06) ** 2))
    rgb = rgb + 0.35 * pool[..., None] * CYAN
    return np.clip(rgb, 0, 1)


def render_foreground(n, scale=1.0):
    """Pièce seule, en RGBA (calque transparent), réduite de `scale` autour du centre."""
    x, y = coords(n)
    x = 0.5 + (x - 0.5) / scale
    y = 0.5 + (y - 0.5) / scale
    d = king(x, y) * scale
    px = 1.0 / n
    inside = np.clip(0.5 - d / px, 0, 1)
    # verre : dégradé vertical sombre, reflet magenta sur le flanc droit, éclat cyan à gauche
    glass = np.zeros(x.shape + (3,))
    shade = np.clip((y - 0.15) / 0.7, 0, 1)[..., None]
    glass += (0.08 + 0.16 * shade) * CYAN + 0.04
    side = np.clip((x - 0.5) / 0.2, 0, 1)[..., None]
    glass += 0.45 * side * MAGENTA * np.clip(-d / 0.04, 0, 1)[..., None]
    sheen = np.exp(-((x - 0.43) / 0.025) ** 2) * (y > 0.3) * (y < 0.72)
    glass += 0.45 * sheen[..., None] * np.array([0.8, 0.97, 1.0])
    # contour néon : trait fin net + halo
    stroke = np.exp(-(np.abs(d) / 0.0075) ** 2)
    halo = np.exp(-np.maximum(d, 0) / 0.03) * (d > 0)
    t = np.clip((y - 0.2) / 0.65, 0, 1)[..., None]
    neon = CYAN * (1 - 0.35 * t) + MAGENTA * 0.35 * t
    color = glass * inside[..., None] + neon * (stroke[..., None] * 1.1)
    alpha = np.clip(inside * 0.92 + stroke + 0.55 * halo, 0, 1)
    halo_rgb = neon * 0.55 * halo[..., None]
    premul = np.clip(color + halo_rgb, 0, None)
    rgb = np.where(alpha[..., None] > 1e-4, premul / np.maximum(alpha[..., None], 1e-4), 0)
    return np.clip(rgb, 0, 1), alpha


def composite(bg, fg_rgb, fg_a):
    return bg * (1 - fg_a[..., None]) + fg_rgb * fg_a[..., None]


def mask(n, kind):
    x, y = coords(n)
    px = 1.0 / n
    if kind == "round":
        d = sd_circle(x, y, 0.5, 0.5, 0.5 - px)
    else:  # carré arrondi des icônes classiques (marge de 4/48, rayon 6/48 environ)
        d = sd_box(x, y, 0.5, 0.5, 0.5 - 2 / 48, 0.5 - 2 / 48, 0.11)
    return np.clip(0.5 - d / px, 0, 1)


def downscale(img, size):
    """Réduction par moyenne de blocs (exacte pour un facteur entier, sinon via Pillow en flottant)."""
    n = img.shape[0]
    if n % size == 0:
        f = n // size
        shape = (size, f, size, f) + img.shape[2:]
        return img.reshape(shape).mean(axis=(1, 3))
    chans = [np.asarray(Image.fromarray(img[..., c].astype(np.float32), "F").resize((size, size), Image.BOX))
             for c in range(img.shape[2])]
    return np.stack(chans, axis=-1)


def to_png(rgba, path):
    data = np.round(np.clip(rgba, 0, 1) * 255).astype(np.uint8)
    mode = "RGBA" if data.shape[2] == 4 else "RGB"
    Image.fromarray(data, mode).save(path, optimize=True)


def guid_for(name):
    return hashlib.md5(("kevchess/appicon/" + name).encode("utf-8")).hexdigest()


def write_meta(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        return
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write("fileFormatVersion: 2\nguid: %s\n" % guid_for(os.path.basename(path)))


def main():
    os.makedirs(OUT, exist_ok=True)
    folder_meta = OUT + ".meta"
    if not os.path.exists(folder_meta):
        with open(folder_meta, "w", encoding="utf-8", newline="\n") as f:
            f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid_for("folder"))

    n = MASTER * SSAA
    bg = render_background(n)
    fg_rgb, fg_a = render_foreground(n)
    full = composite(bg, fg_rgb, fg_a)
    del fg_rgb, fg_a
    # premier plan adaptatif, prémultiplié pour réduire sans franges sombres
    ad_rgb, ad_a = render_foreground(n, ADAPTIVE_SCALE)
    fg_pre = np.concatenate([ad_rgb * ad_a[..., None], ad_a[..., None]], axis=-1)
    del ad_rgb, ad_a

    def fg_at(size):
        p = downscale(fg_pre, size)
        a = p[..., 3:4]
        rgb = np.where(a > 1e-4, p[..., :3] / np.maximum(a, 1e-4), 0)
        return np.concatenate([np.clip(rgb, 0, 1), a], axis=-1)

    written = []

    def save(img, name):
        path = os.path.join(OUT, name)
        to_png(img, path)
        write_meta(path)
        written.append(name)

    for s in LEGACY_SIZES:
        small = downscale(full, s)
        save(np.concatenate([small, mask(s, "legacy")[..., None]], axis=-1), "icon-legacy-%d.png" % s)
        save(np.concatenate([small, mask(s, "round")[..., None]], axis=-1), "icon-round-%d.png" % s)
    for s in ADAPTIVE_SIZES:
        save(downscale(bg, s), "icon-adaptive-bg-%d.png" % s)
        save(fg_at(s), "icon-adaptive-fg-%d.png" % s)
    save(downscale(full, 512), "icon-store-512.png")

    # planche de contrôle : icône classique, ronde, et masque adaptatif « goutte » simulé
    sheet = np.full((300, 1190, 3), 0.92)
    s_ad = 288  # adaptative : calques de 108 dp, le lanceur montre le cercle central de 72 dp
    ad = downscale(bg, s_ad) * (1 - fg_at(s_ad)[..., 3:4]) + fg_at(s_ad)[..., :3] * fg_at(s_ad)[..., 3:4]
    crop = ad[48:240, 48:240]
    adaptive = (crop, mask(192, "round"))
    for i, (kind, s) in enumerate([("legacy", 192), ("round", 192), ("adaptive", 192), ("legacy", 96), ("round", 48)]):
        small, m = adaptive if kind == "adaptive" else (downscale(full, s), mask(s, kind))
        m = m[..., None]
        x0 = 30 + i * 230
        y0 = (300 - s) // 2
        sheet[y0:y0 + s, x0:x0 + s] = small * m + sheet[y0:y0 + s, x0:x0 + s] * (1 - m)
    preview = os.path.join(OUT, "icon-preview.png")
    to_png(sheet, preview)
    write_meta(preview)
    written.append("icon-preview.png")
    print("%d PNG écrits dans %s" % (len(written), os.path.relpath(OUT, ROOT)))


if __name__ == "__main__":
    sys.exit(main())

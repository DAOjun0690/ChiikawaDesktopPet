"""Shared helpers for the per-character asset-build scripts (build_*_assets.py).

ponytail: only the logic that was byte-identical (or a strict superset) across
build_dog_assets.py / build_emperor_assets.py / build_nailong_assets.py /
build_linedog_assets.py lives here. Per-character source resolution (sticker
IDs, download URLs, keyframe tables) stays in each script.
"""
import os
import numpy as np
from PIL import Image, ImageOps


def load_rgba(path):
    return Image.open(path).convert("RGBA")


def make_left_facing(img, split_y=46):
    """Mirror the body horizontally to face left, keeping the region above
    split_y (typically overlaid text) unmirrored and upright."""
    arr = np.array(img)
    arr_text = arr.copy()
    arr_text[split_y:, :, 3] = 0
    arr_body = arr.copy()
    arr_body[0:split_y, :, 3] = 0

    im_body_mirrored = ImageOps.mirror(Image.fromarray(arr_body))
    im_text = Image.fromarray(arr_text)

    res = Image.new("RGBA", img.size, (0, 0, 0, 0))
    res.paste(im_body_mirrored, (0, 0), im_body_mirrored)
    res.paste(im_text, (0, 0), im_text)
    return res


def fit_to_canvas(img, target_size=(270, 240), scale=1.0, scale_x=1.0, scale_y=1.0,
                   angle=0, dx=0, dy=0, flip=False, anchor_bottom=True,
                   bottom_margin=4, expand_on_rotate=True):
    w, h = img.size
    sx = scale * scale_x
    sy = scale * scale_y
    new_w = max(1, int(w * sx))
    new_h = max(1, int(h * sy))
    scaled = img.resize((new_w, new_h), Image.Resampling.LANCZOS)
    if angle != 0:
        scaled = scaled.rotate(angle, resample=Image.Resampling.BICUBIC, expand=expand_on_rotate)
        if expand_on_rotate:
            new_w, new_h = scaled.size
    if flip:
        scaled = ImageOps.mirror(scaled)

    canvas = Image.new("RGBA", target_size, (0, 0, 0, 0))
    if anchor_bottom:
        arr = np.array(scaled)
        nz = np.where(arr[:, :, 3] > 10)
        if len(nz[0]) > 0:
            content_bottom = nz[0].max()
            target_bottom = target_size[1] - bottom_margin + dy
            y = target_bottom - content_bottom
            x = (target_size[0] - new_w) // 2 + dx
        else:
            x = (target_size[0] - new_w) // 2 + dx
            y = (target_size[1] - new_h) // 2 + dy
    else:
        x = (target_size[0] - new_w) // 2 + dx
        y = (target_size[1] - new_h) // 2 + dy

    canvas.paste(scaled, (x, y), scaled)
    return canvas


def build_animation(anim_dir, anim_name, base_img, frame_params, target_size=(270, 240),
                     scale=1.0, anchor_bottom=True, bottom_margin=4, expand_on_rotate=True):
    """Render a procedural keyframe animation (per-frame dx/dy/scale/angle overrides).
    Each frame dict may set an "img" key to switch source sprite mid-animation."""
    out_dir = os.path.join(anim_dir, anim_name)
    os.makedirs(out_dir, exist_ok=True)
    for idx, params in enumerate(frame_params, 1):
        frame = fit_to_canvas(
            params.get("img", base_img),
            target_size=target_size,
            scale=params.get("scale", scale),
            scale_x=params.get("sx", 1.0),
            scale_y=params.get("sy", 1.0),
            angle=params.get("ang", 0),
            dx=params.get("dx", 0),
            dy=params.get("dy", 0),
            flip=params.get("flip", False),
            anchor_bottom=params.get("anchor_bottom", anchor_bottom),
            bottom_margin=bottom_margin,
            expand_on_rotate=expand_on_rotate,
        )
        frame.save(os.path.join(out_dir, f"{idx}.png"))


def export_frame_sequence(anim_dir, anim_name, frame_paths, target_size=(270, 240),
                           scale=1.0, anchor_bottom=True, bottom_margin=4):
    """Fit an already-extracted sequence of frame images onto the canvas, one file per frame."""
    out_dir = os.path.join(anim_dir, anim_name)
    os.makedirs(out_dir, exist_ok=True)
    for idx, path in enumerate(frame_paths, 1):
        canvas = fit_to_canvas(load_rgba(path), target_size=target_size, scale=scale,
                                anchor_bottom=anchor_bottom, bottom_margin=bottom_margin)
        canvas.save(os.path.join(out_dir, f"{idx}.png"))


def _demo():
    """ponytail self-check: exercises every shared function on a synthetic sprite."""
    import tempfile

    src = Image.new("RGBA", (60, 100), (0, 0, 0, 0))
    for y in range(10, 90):
        for x in range(10, 50):
            src.putpixel((x, y), (255, 0, 0, 255))

    canvas = fit_to_canvas(src, target_size=(270, 240), scale=1.0, anchor_bottom=True)
    assert canvas.size == (270, 240)
    arr = np.array(canvas)
    content_rows = np.where(arr[:, :, 3] > 10)[0]
    assert content_rows.max() == 240 - 4, "anchor_bottom should sit content 4px above the bottom edge"

    centered = fit_to_canvas(src, target_size=(270, 240), scale=1.0, anchor_bottom=False)
    assert centered.size == (270, 240)

    flipped = make_left_facing(src, split_y=20)
    assert np.array(flipped)[5, :, 3].sum() == np.array(src)[5, :, 3].sum(), "text band above split_y must stay unmirrored"

    with tempfile.TemporaryDirectory() as tmp:
        build_animation(tmp, "wave", src, [{"dx": 1}, {"dx": -1}], target_size=(270, 240))
        assert os.path.exists(os.path.join(tmp, "wave", "1.png"))
        assert os.path.exists(os.path.join(tmp, "wave", "2.png"))

        frame_path = os.path.join(tmp, "frame_src.png")
        src.save(frame_path)
        export_frame_sequence(tmp, "seq", [frame_path, frame_path], target_size=(270, 240))
        assert os.path.exists(os.path.join(tmp, "seq", "2.png"))

    print("asset_common self-check passed.")


if __name__ == "__main__":
    _demo()

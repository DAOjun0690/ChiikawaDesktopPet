import os
import math
import numpy as np
from PIL import Image, ImageOps

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUTPUT_DIR = os.path.join(REPO_ROOT, "assets", "optimized", "chesthair_emperor")
ANIM_DIR = os.path.join(OUTPUT_DIR, "animations")
SPRITES_DIR = os.path.join(OUTPUT_DIR, "sprites")
ICONS_DIR = os.path.join(OUTPUT_DIR, "icons")

SCRATCH_STICKERS_DIR = os.path.join(
    os.environ.get("USERPROFILE", ""),
    ".gemini", "antigravity", "brain",
    "af32e271-f32d-4918-bf81-84cb62196693", "scratch", "stickers"
)

os.makedirs(ANIM_DIR, exist_ok=True)
os.makedirs(SPRITES_DIR, exist_ok=True)
os.makedirs(ICONS_DIR, exist_ok=True)

def get_sticker_path(num):
    prefix = f"{num:02d}_"
    for f in os.listdir(SCRATCH_STICKERS_DIR):
        if f.startswith(prefix):
            return os.path.join(SCRATCH_STICKERS_DIR, f)
    raise FileNotFoundError(f"Sticker {num} not found in {SCRATCH_STICKERS_DIR}")

def load_rgba(path):
    return Image.open(path).convert("RGBA")

def make_left_facing(img, split_y=46):
    """
    Mirrors the character body horizontally so it faces left,
    while keeping the Chinese text in the top portion unmirrored and readable left-to-right.
    """
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
                  angle=0, dx=0, dy=0, flip=False, anchor_bottom=True):
    """
    Fits the original sticker with full typography onto target canvas.
    Preserves 100% of original sticker resolution and layout.
    """
    w, h = img.size
    sx = scale * scale_x
    sy = scale * scale_y
    new_w = max(1, int(w * sx))
    new_h = max(1, int(h * sy))
    scaled = img.resize((new_w, new_h), Image.Resampling.LANCZOS)
    if angle != 0:
        scaled = scaled.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
        new_w, new_h = scaled.size
    if flip:
        scaled = ImageOps.mirror(scaled)
    
    canvas = Image.new("RGBA", target_size, (0, 0, 0, 0))
    if anchor_bottom:
        arr = np.array(scaled)
        nz = np.where(arr[:, :, 3] > 10)
        if len(nz[0]) > 0:
            content_bottom = nz[0].max()
            target_bottom = target_size[1] - 4 + dy
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

print("1. Building Static Sprites (Fully preserving original typography)...")
raw_01 = load_rgba(get_sticker_path(1))
im_01_right = raw_01
im_01_left = make_left_facing(raw_01)

# spawn.png (Sticker 01 - 陪朕出去走走)
im_spawn = fit_to_canvas(im_01_right, (270, 240), scale=1.0)
im_spawn.save(os.path.join(SPRITES_DIR, "spawn.png"))

# grabbed.png: Sticker 15 (不許對朕說謊 - full typography)
raw_15 = load_rgba(get_sticker_path(15))
im_grabbed = fit_to_canvas(raw_15, (270, 240), scale=1.0, dy=-6)
im_grabbed.save(os.path.join(SPRITES_DIR, "grabbed.png"))

# grabbed1.png: Sticker 25 (都放肆 - full typography)
raw_25 = load_rgba(get_sticker_path(25))
im_grabbed1 = fit_to_canvas(raw_25, (270, 240), scale=1.0, dy=-6)
im_grabbed1.save(os.path.join(SPRITES_DIR, "grabbed1.png"))

# shaken.png: Sticker 05 (龍顏大怒 - full typography)
raw_05 = load_rgba(get_sticker_path(5))
im_shaken = fit_to_canvas(raw_05, (270, 240), scale=1.0, anchor_bottom=False)
im_shaken.save(os.path.join(SPRITES_DIR, "shaken.png"))

# falling.png: Sticker 16 (竟敢欺君 - full typography, angle=0 to keep text level)
raw_16 = load_rgba(get_sticker_path(16))
im_falling = fit_to_canvas(raw_16, (270, 240), scale=1.0, angle=0, dy=-10)
im_falling.save(os.path.join(SPRITES_DIR, "falling.png"))

# fallingend.png: Sticker 23 (朕乏了 - full typography)
raw_23 = load_rgba(get_sticker_path(23))
im_fallingend = fit_to_canvas(raw_23, (270, 240), scale=1.0, scale_x=1.05, scale_y=0.95, dy=4)
im_fallingend.save(os.path.join(SPRITES_DIR, "fallingend.png"))

# jumpleft.png (Facing LEFT!) & jumpright.png (Facing RIGHT!) - angle=0 so text does not tilt
im_jumpleft = fit_to_canvas(im_01_left, (270, 240), scale=1.0, angle=0, dx=0, dy=-12)
im_jumpright = fit_to_canvas(im_01_right, (270, 240), scale=1.0, angle=0, dx=0, dy=-12)
im_jumpleft.save(os.path.join(SPRITES_DIR, "jumpleft.png"))
im_jumpright.save(os.path.join(SPRITES_DIR, "jumpright.png"))

print("2. Building Menu/Tray Icon...")
# icon.png (256x256)
im_icon = fit_to_canvas(raw_01, (256, 256), scale=1.0, anchor_bottom=False)
im_icon.save(os.path.join(ICONS_DIR, "icon.png"))

print("3. Building Walk Cycles (walkleft -> Faces LEFT, walkright -> Faces RIGHT, angle=0)...")
walk_dir_left = os.path.join(ANIM_DIR, "walkleft")
walk_dir_right = os.path.join(ANIM_DIR, "walkright")
os.makedirs(walk_dir_left, exist_ok=True)
os.makedirs(walk_dir_right, exist_ok=True)

# 8-step walking waddle cycle without text tilt
walk_steps = [
    (0, 0, 1.0, 1.0),
    (-3, -3, 0.98, 1.02),
    (-6, -6, 0.96, 1.04),
    (-3, -3, 0.98, 1.02),
    (0, 0, 1.02, 0.98),
    (3, -3, 0.98, 1.02),
    (6, -6, 0.96, 1.04),
    (3, -3, 0.98, 1.02)
]

for idx, (dx, dy, sx, sy) in enumerate(walk_steps, 1):
    wl = fit_to_canvas(im_01_left, (270, 240), scale=1.0, angle=0, dx=dx, dy=dy, scale_x=sx, scale_y=sy)
    wr = fit_to_canvas(im_01_right, (270, 240), scale=1.0, angle=0, dx=-dx, dy=dy, scale_x=sx, scale_y=sy)
    wl.save(os.path.join(walk_dir_left, f"{idx}.png"))
    wr.save(os.path.join(walk_dir_right, f"{idx}.png"))

print("4. Building Character Animations (Full typography preserved)...")

def build_animation(anim_name, base_img, frame_params, target_size=(270, 240), scale=1.0, anchor_bottom=True):
    out_dir = os.path.join(ANIM_DIR, anim_name)
    os.makedirs(out_dir, exist_ok=True)
    for idx, params in enumerate(frame_params, 1):
        ang = params.get("ang", 0)
        dx = params.get("dx", 0)
        dy = params.get("dy", 0)
        sx = params.get("sx", 1.0)
        sy = params.get("sy", 1.0)
        sc = params.get("scale", scale)
        flip = params.get("flip", False)
        ab = params.get("anchor_bottom", anchor_bottom)
        frame = fit_to_canvas(base_img, target_size=target_size, scale=sc,
                              scale_x=sx, scale_y=sy, angle=ang, dx=dx, dy=dy,
                              flip=flip, anchor_bottom=ab)
        frame.save(os.path.join(out_dir, f"{idx}.png"))

# 1. bounce (Sticker 02 - 看朕打下的「江山」 full)
raw_02 = load_rgba(get_sticker_path(2))
bounce_frames = [
    {"dy": 0, "sy": 1.0, "sx": 1.0},
    {"dy": -2, "sy": 1.02, "sx": 0.99},
    {"dy": -4, "sy": 1.04, "sx": 0.98},
    {"dy": -5, "sy": 1.05, "sx": 0.97},
    {"dy": -4, "sy": 1.04, "sx": 0.98},
    {"dy": -2, "sy": 1.02, "sx": 0.99},
    {"dy": 0, "sy": 1.0, "sx": 1.0},
    {"dy": 1, "sy": 0.98, "sx": 1.02},
    {"dy": 2, "sy": 0.97, "sx": 1.03},
    {"dy": 1, "sy": 0.98, "sx": 1.02}
]
build_animation("bounce", raw_02, bounce_frames, scale=1.0, anchor_bottom=True)

# 2. angry (Sticker 05 - 龍顏大怒: violent screen-shake & rage pulsing)
raw_05 = load_rgba(get_sticker_path(5))
angry_frames = [
    {"dx": -3, "dy": -2, "scale": 0.98},
    {"dx": 4, "dy": 3, "scale": 1.02},
    {"dx": -4, "dy": 1, "scale": 0.98},
    {"dx": 3, "dy": -3, "scale": 1.03},
    {"dx": -2, "dy": 2, "scale": 0.98},
    {"dx": 4, "dy": -1, "scale": 1.03},
    {"dx": -3, "dy": 3, "scale": 0.98},
    {"dx": 2, "dy": -2, "scale": 1.02},
    {"dx": -4, "dy": -1, "scale": 0.98},
    {"dx": 3, "dy": 2, "scale": 1.03},
    {"dx": -1, "dy": -1, "scale": 0.99},
    {"dx": 1, "dy": 0, "scale": 1.0}
]
build_animation("angry", raw_05, angry_frames, scale=1.0, anchor_bottom=False)

# 3. panic (Sticker 31 - 來人 護駕: rapid trembling & shivering)
raw_31 = load_rgba(get_sticker_path(31))
panic_frames = [
    {"dx": -2, "dy": -1, "ang": -1},
    {"dx": 3, "dy": 2, "ang": 1},
    {"dx": -3, "dy": 1, "ang": -1},
    {"dx": 2, "dy": -2, "ang": 2},
    {"dx": -2, "dy": 2, "ang": -2},
    {"dx": 3, "dy": -1, "ang": 1},
    {"dx": -3, "dy": 1, "ang": -1},
    {"dx": 2, "dy": -2, "ang": 2},
    {"dx": -2, "dy": 2, "ang": -2},
    {"dx": 3, "dy": -1, "ang": 1},
    {"dx": -1, "dy": 1, "ang": 0},
    {"dx": 0, "dy": 0, "ang": 0}
]
build_animation("panic", raw_31, panic_frames, scale=1.0, anchor_bottom=False)

# 4. stamp (Sticker 37 - 朕已閱: Giant imperial seal slam!)
raw_37 = load_rgba(get_sticker_path(37))
stamp_frames = [
    {"dy": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -6, "sx": 0.98, "sy": 1.02},
    {"dy": -14, "sx": 0.95, "sy": 1.05},
    {"dy": -20, "sx": 0.94, "sy": 1.06},  # apex
    {"dy": -10, "sx": 0.96, "sy": 1.04},
    {"dy": 4, "sx": 1.15, "sy": 0.88},   # SLAM! impact
    {"dy": 5, "sx": 1.18, "sy": 0.85},   # max squash
    {"dy": -4, "sx": 1.04, "sy": 0.96},  # rebound
    {"dy": 1, "sx": 1.02, "sy": 0.98},
    {"dy": -1, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("stamp", raw_37, stamp_frames, scale=1.0, anchor_bottom=True)

# 5. dismiss (Sticker 04 - 眾卿 退朝吧: gracious sleeve wave)
raw_04 = load_rgba(get_sticker_path(4))
dismiss_frames = [
    {"ang": 0, "dx": 0, "dy": 0},
    {"ang": 1, "dx": 2, "dy": -1},
    {"ang": 3, "dx": 4, "dy": -2},
    {"ang": 4, "dx": 6, "dy": -3},
    {"ang": 3, "dx": 4, "dy": -2},
    {"ang": 1, "dx": 2, "dy": -1},
    {"ang": -1, "dx": -1, "dy": 0},
    {"ang": -2, "dx": -3, "dy": 0},
    {"ang": -1, "dx": -1, "dy": 0},
    {"ang": 0, "dx": 0, "dy": 0}
]
build_animation("dismiss", raw_04, dismiss_frames, scale=1.0, anchor_bottom=True)

# 6. lazy (Sticker 23 - 朕乏了: slow comfortable breathing on royal couch)
raw_23 = load_rgba(get_sticker_path(23))
lazy_frames = [
    {"dy": 0, "sy": 1.0, "sx": 1.0},
    {"dy": -1, "sy": 1.01, "sx": 0.995},
    {"dy": -2, "sy": 1.02, "sx": 0.99},
    {"dy": -3, "sy": 1.03, "sx": 0.985},
    {"dy": -3, "sy": 1.035, "sx": 0.98},
    {"dy": -2, "sy": 1.025, "sx": 0.985},
    {"dy": -1, "sy": 1.015, "sx": 0.99},
    {"dy": 0, "sy": 1.0, "sx": 1.0},
    {"dy": 1, "sy": 0.985, "sx": 1.01},
    {"dy": 2, "sy": 0.975, "sx": 1.02},
    {"dy": 1, "sy": 0.985, "sx": 1.01},
    {"dy": 0, "sy": 1.0, "sx": 1.0}
]
build_animation("lazy", raw_23, lazy_frames, scale=1.0, anchor_bottom=True)

# 7. suspicious (Sticker 40 - 總有小人欲加害朕: glancing suspiciously side-to-side)
raw_40 = load_rgba(get_sticker_path(40))
suspicious_frames = [
    {"dx": 0, "dy": 0, "ang": 0},
    {"dx": -3, "dy": 0, "ang": -1},
    {"dx": -6, "dy": -1, "ang": -2},
    {"dx": -6, "dy": -1, "ang": -2},
    {"dx": -2, "dy": 0, "ang": -1},
    {"dx": 2, "dy": 0, "ang": 1},
    {"dx": 6, "dy": -1, "ang": 2},
    {"dx": 6, "dy": -1, "ang": 2},
    {"dx": 3, "dy": 0, "ang": 1},
    {"dx": 0, "dy": 0, "ang": 0},
    {"dx": 0, "dy": -2, "ang": 0, "sy": 1.02},
    {"dx": 0, "dy": 0, "ang": 0}
]
build_animation("suspicious", raw_40, suspicious_frames, scale=1.0, anchor_bottom=True)

# 8. reward (Sticker 14 - 這個 賞: presenting royal reward)
raw_14 = load_rgba(get_sticker_path(14))
reward_frames = [
    {"dy": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -2, "sx": 0.99, "sy": 1.01},
    {"dy": -5, "sx": 0.97, "sy": 1.03},
    {"dy": -7, "sx": 0.95, "sy": 1.05},
    {"dy": -7, "sx": 0.95, "sy": 1.05},
    {"dy": -4, "sx": 0.98, "sy": 1.02},
    {"dy": -1, "sx": 1.0, "sy": 1.0},
    {"dy": 1, "sx": 1.01, "sy": 0.99},
    {"dy": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("reward", raw_14, reward_frames, scale=1.0, anchor_bottom=True)

# 9. read (Sticker 35 - 此事不必上本: reading imperial scroll & nodding)
raw_35 = load_rgba(get_sticker_path(35))
read_frames = [
    {"ang": 0, "dy": 0},
    {"ang": -1, "dy": -1},
    {"ang": -2, "dy": -2},
    {"ang": -1, "dy": -1},
    {"ang": 0, "dy": 0},
    {"ang": 2, "dy": 1},
    {"ang": 3, "dy": 2},
    {"ang": 2, "dy": 1},
    {"ang": 0, "dy": 0},
    {"ang": 0, "dy": 0}
]
build_animation("read", raw_35, read_frames, scale=1.0, anchor_bottom=True)

# 10. inspect (Sticker 11 - 還有多少驚喜是朕不知道的: inspecting hot roast potato)
raw_11 = load_rgba(get_sticker_path(11))
inspect_frames = [
    {"dy": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -2, "sx": 0.99, "sy": 1.02},
    {"dy": -4, "sx": 0.98, "sy": 1.03},
    {"dy": -3, "sx": 0.99, "sy": 1.02},
    {"dy": -1, "sx": 1.0, "sy": 1.0},
    {"dy": -2, "sx": 0.99, "sy": 1.02},
    {"dy": -4, "sx": 0.98, "sy": 1.03},
    {"dy": -3, "sx": 0.99, "sy": 1.02},
    {"dy": -1, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("inspect", raw_11, inspect_frames, scale=1.0, anchor_bottom=True)

# 11. worship (Sticker 29 - 免禮: gracious royal wave)
raw_29 = load_rgba(get_sticker_path(29))
worship_frames = [
    {"dy": 0, "ang": 0},
    {"dy": -2, "ang": 1},
    {"dy": -5, "ang": 3},
    {"dy": -7, "ang": 4},
    {"dy": -6, "ang": 3},
    {"dy": -4, "ang": 2},
    {"dy": -2, "ang": 1},
    {"dy": 0, "ang": 0},
    {"dy": 1, "ang": -1},
    {"dy": 0, "ang": 0}
]
build_animation("worship", raw_29, worship_frames, scale=1.0, anchor_bottom=True)

print(f"All assets successfully generated in {OUTPUT_DIR}!")

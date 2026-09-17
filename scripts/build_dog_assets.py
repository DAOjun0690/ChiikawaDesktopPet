import os
import math
import numpy as np
from PIL import Image, ImageOps, ImageDraw

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUTPUT_DIR = os.path.join(REPO_ROOT, "assets", "optimized", "chesthair_dog")
ANIM_DIR = os.path.join(OUTPUT_DIR, "animations")
SPRITES_DIR = os.path.join(OUTPUT_DIR, "sprites")
ICONS_DIR = os.path.join(OUTPUT_DIR, "icons")

SCRATCH_DIR = r"C:\Users\JEFF WANG\.gemini\antigravity\brain\46e43fe3-974a-452b-836d-3afa38f8fe20\scratch\dog_stickers"

os.makedirs(ANIM_DIR, exist_ok=True)
os.makedirs(SPRITES_DIR, exist_ok=True)
os.makedirs(ICONS_DIR, exist_ok=True)

def get_sticker(rel_path):
    p = os.path.join(SCRATCH_DIR, rel_path)
    if not os.path.exists(p):
        raise FileNotFoundError(f"Missing {p}")
    return Image.open(p).convert("RGBA")

def make_left_facing(img, split_ratio=0.22):
    """
    Mirrors body horizontally so it faces left, while keeping top text unmirrored.
    """
    arr = np.array(img)
    h = arr.shape[0]
    split_y = int(h * split_ratio)
    
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

def fit_to_canvas(img, target_size=(270, 240), scale=0.74, scale_x=1.0, scale_y=1.0,
                  angle=0, dx=0, dy=0, flip=False, anchor_bottom=True):
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
            target_bottom = target_size[1] - 6 + dy
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

def build_animation(anim_name, base_img, frame_params, target_size=(270, 240), scale=0.74, anchor_bottom=True):
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
        img_src = params.get("img", base_img)
        frame = fit_to_canvas(img_src, target_size=target_size, scale=sc,
                              scale_x=sx, scale_y=sy, angle=ang, dx=dx, dy=dy,
                              flip=flip, anchor_bottom=ab)
            
        frame.save(os.path.join(out_dir, f"{idx}.png"))

print("1. Generating Base Sprites...")
raw_spawn = get_sticker("lv76_33346223/01_828016993.png")     # 汪！歡呼
raw_grabbed = get_sticker("lv76_33346223/12_828017004.png")   # 被抱起
raw_grabbed1 = get_sticker("lv46_22487600/19_573397240.png")  # 吊在半空放手吧
raw_shaken = get_sticker("lv76_33346223/27_828017019.png")    # 啊！啊！暴躁踢腿
raw_falling = get_sticker("lv69_30973429/06_779529758.png")   # 跌坐落體
raw_fallingend = get_sticker("lv76_33346223/18_828017010.png")# 失去精神融化
raw_jump = get_sticker("lv76_33346223/11_828017003.png")      # 奔跑跳躍

# spawn.png
im_spawn = fit_to_canvas(raw_spawn, (270, 240), scale=0.74)
im_spawn.save(os.path.join(SPRITES_DIR, "spawn.png"))

# grabbed.png
im_grabbed = fit_to_canvas(raw_grabbed, (270, 240), scale=0.74, dy=-6)
im_grabbed.save(os.path.join(SPRITES_DIR, "grabbed.png"))

# grabbed1.png
im_grabbed1 = fit_to_canvas(raw_grabbed1, (270, 240), scale=0.72, dy=-8)
im_grabbed1.save(os.path.join(SPRITES_DIR, "grabbed1.png"))

# shaken.png
im_shaken = fit_to_canvas(raw_shaken, (270, 240), scale=0.74, anchor_bottom=False)
im_shaken.save(os.path.join(SPRITES_DIR, "shaken.png"))

# falling.png
im_falling = fit_to_canvas(raw_falling, (270, 240), scale=0.74, dy=-12)
im_falling.save(os.path.join(SPRITES_DIR, "falling.png"))

# fallingend.png
im_fallingend = fit_to_canvas(raw_fallingend, (270, 240), scale=0.74, scale_x=1.06, scale_y=0.92, dy=4)
im_fallingend.save(os.path.join(SPRITES_DIR, "fallingend.png"))

# jumpleft.png & jumpright.png
im_jumpleft = fit_to_canvas(raw_jump, (270, 240), scale=0.74, angle=6, dx=-4, dy=-14, flip=True)
im_jumpright = fit_to_canvas(raw_jump, (270, 240), scale=0.74, angle=-6, dx=4, dy=-14, flip=False)
im_jumpleft.save(os.path.join(SPRITES_DIR, "jumpleft.png"))
im_jumpright.save(os.path.join(SPRITES_DIR, "jumpright.png"))

print("2. Generating Menu/Tray Icon...")
# 256x256 icon
im_icon = fit_to_canvas(raw_spawn, (256, 256), scale=0.72, anchor_bottom=False)
im_icon.save(os.path.join(ICONS_DIR, "icon.png"))

print("3. Synthesizing Walk Loops (walkleft & walkright)...")
walk_dir_left = os.path.join(ANIM_DIR, "walkleft")
walk_dir_right = os.path.join(ANIM_DIR, "walkright")
os.makedirs(walk_dir_left, exist_ok=True)
os.makedirs(walk_dir_right, exist_ok=True)

raw_walk = get_sticker("lv76_33346223/11_828017003.png")

# 10-step fluid waddle walk cycle
walk_params = [
    (0, 0, 1.0, 1.0, 0),
    (-3, -3, 0.98, 1.02, 2),
    (-6, -6, 0.95, 1.05, 4),
    (-4, -4, 0.97, 1.03, 2),
    (0, 1, 1.02, 0.98, 0),
    (3, -3, 0.98, 1.02, -2),
    (6, -6, 0.95, 1.05, -4),
    (4, -4, 0.97, 1.03, -2),
    (0, 1, 1.02, 0.98, 0),
    (-1, 0, 1.0, 1.0, 1)
]

for idx, (dx, dy, sx, sy, ang) in enumerate(walk_params, 1):
    # walkright faces right (raw sticker faces right)
    wr = fit_to_canvas(raw_walk, (270, 240), scale=0.74, angle=ang, dx=dx, dy=dy, scale_x=sx, scale_y=sy, flip=False)
    # walkleft faces left (flip=True)
    wl = fit_to_canvas(raw_walk, (270, 240), scale=0.74, angle=-ang, dx=-dx, dy=dy, scale_x=sx, scale_y=sy, flip=True)
    wr.save(os.path.join(walk_dir_right, f"{idx}.png"))
    wl.save(os.path.join(walk_dir_left, f"{idx}.png"))

print("4. Building Specialized Action Animations (Including user-preferred swat & reactions)...")

# --- A. 打狗勾 swat (828017006: 拿報紙打狗勾 *啪*啪*啪 連續拍打) ---
raw_swat = get_sticker("lv76_33346223/14_828017006.png")
swat_frames = [
    # Ready / wind up 1
    {"dy": -4, "ang": -2, "sx": 0.98, "sy": 1.02},
    {"dy": 1, "ang": 1, "sx": 1.01, "sy": 0.99},
    # SWING 1 impact!
    {"dy": 6, "ang": 5, "sx": 1.15, "sy": 0.82, "dx": 3},
    {"dy": 7, "ang": 4, "sx": 1.16, "sy": 0.80, "dx": -2},
    {"dy": 2, "ang": 1, "sx": 1.04, "sy": 0.96},
    # Wind up 2
    {"dy": -5, "ang": -3, "sx": 0.97, "sy": 1.03},
    {"dy": -8, "ang": -5, "sx": 0.95, "sy": 1.05},
    {"dy": 2, "ang": 2, "sx": 1.02, "sy": 0.98},
    # SWING 2 maximum impact!
    {"dy": 8, "ang": 6, "sx": 1.20, "sy": 0.78, "dx": 4},
    {"dy": 8, "ang": 5, "sx": 1.22, "sy": 0.76, "dx": -3},
    {"dy": 3, "ang": 2, "sx": 1.06, "sy": 0.94, "dx": 2},
    {"dy": -1, "ang": -1, "sx": 0.98, "sy": 1.02},
    {"dy": 1, "ang": 1, "sx": 1.02, "sy": 0.98},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("swat", raw_swat, swat_frames, scale=0.74, anchor_bottom=True)

# --- B. 反應1: tantrum (828017019: 啊！啊！暴躁踢腿抗議) ---
raw_tantrum = get_sticker("lv76_33346223/27_828017019.png")
tantrum_frames = [
    {"dx": -4, "dy": -2, "ang": -3, "sx": 0.97, "sy": 1.03},
    {"dx": 4, "dy": 2, "ang": 3, "sx": 1.03, "sy": 0.97},
    {"dx": -5, "dy": -3, "ang": -4, "sx": 0.96, "sy": 1.04},
    {"dx": 5, "dy": 3, "ang": 4, "sx": 1.04, "sy": 0.96},
    {"dx": -4, "dy": 2, "ang": -3, "sx": 0.97, "sy": 1.03},
    {"dx": 4, "dy": -2, "ang": 3, "sx": 1.03, "sy": 0.97},
    {"dx": -3, "dy": 3, "ang": -2, "sx": 0.98, "sy": 1.02},
    {"dx": 3, "dy": -3, "ang": 2, "sx": 1.02, "sy": 0.98},
    {"dx": -4, "dy": 1, "ang": -3, "sx": 0.97, "sy": 1.03},
    {"dx": 4, "dy": -1, "ang": 3, "sx": 1.03, "sy": 0.97},
    {"dx": -2, "dy": 0, "ang": -1, "sx": 0.99, "sy": 1.01},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("tantrum", raw_tantrum, tantrum_frames, scale=0.74, anchor_bottom=True)

# --- C. 反應2: fume (779529755: 頭頂冒煙生悶氣) ---
raw_fume = get_sticker("lv69_30973429/03_779529755.png")
fume_frames = [
    {"dy": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 1, "sx": 1.02, "sy": 0.98},
    {"dy": 2, "sx": 1.05, "sy": 0.95},
    {"dy": 3, "sx": 1.06, "sy": 0.94},
    {"dy": 1, "sx": 1.03, "sy": 0.97, "dx": -2},
    {"dy": 0, "sx": 1.0, "sy": 1.0, "dx": 2},
    {"dy": -2, "sx": 0.97, "sy": 1.03, "dx": -1},
    {"dy": -3, "sx": 0.96, "sy": 1.04, "dx": 1},
    {"dy": -1, "sx": 0.99, "sy": 1.01},
    {"dy": 1, "sx": 1.03, "sy": 0.97},
    {"dy": 2, "sx": 1.04, "sy": 0.96},
    {"dy": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("fume", raw_fume, fume_frames, scale=0.74, anchor_bottom=True)

# --- D. 反應3: cry (779529761: 委屈掉眼淚發抖) ---
raw_cry = get_sticker("lv69_30973429/09_779529761.png")
cry_frames = [
    {"dx": -1, "dy": 0, "ang": 0, "sy": 1.0, "sx": 1.0},
    {"dx": 1, "dy": -1, "ang": 1, "sy": 1.01, "sx": 0.99},
    {"dx": -2, "dy": -2, "ang": -1, "sy": 1.02, "sx": 0.98},
    {"dx": 2, "dy": -1, "ang": 1, "sy": 1.01, "sx": 0.99},
    {"dx": -1, "dy": 0, "ang": 0, "sy": 1.0, "sx": 1.0},
    {"dx": 1, "dy": 1, "ang": -1, "sy": 0.98, "sx": 1.02},
    {"dx": -2, "dy": 2, "ang": 1, "sy": 0.97, "sx": 1.03},
    {"dx": 2, "dy": 1, "ang": -1, "sy": 0.98, "sx": 1.02},
    {"dx": -1, "dy": 0, "ang": 0, "sy": 1.0, "sx": 1.0},
    {"dx": 1, "dy": -1, "ang": 1, "sy": 1.01, "sx": 0.99},
    {"dx": -1, "dy": 0, "ang": 0, "sy": 1.0, "sx": 1.0},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("cry", raw_cry, cry_frames, scale=0.74, anchor_bottom=True)

# --- E. 打狗勾連鎖大戲: beat_combo (36-frame multi-stage drama: 挨打 ➔ 暴怒 ➔ 冒煙 ➔ 哭哭) ---
# Stage 1: Quick slap trigger (Frames 1-4)
quick_slap = [
    {"img": raw_swat, "dy": -6, "ang": -4, "sx": 0.96, "sy": 1.04},
    {"img": raw_swat, "dy": 7, "ang": 6, "sx": 1.20, "sy": 0.78, "dx": 4}, # POW!
    {"img": raw_swat, "dy": 5, "ang": 3, "sx": 1.10, "sy": 0.90, "dx": -2},
    {"img": raw_swat, "dy": 0, "ang": -1, "sx": 1.0, "sy": 1.0}
]

beat_combo_frames = []
# Part 1: Sudden whack (4 frames)
beat_combo_frames.extend(quick_slap)

# Part 2: Tantrum explosion kicking (11 frames)
for p in tantrum_frames[:11]:
    p_copy = p.copy()
    p_copy["img"] = raw_tantrum
    beat_combo_frames.append(p_copy)

# Part 3: Drop down sulking & fuming steam (10 frames)
for p in fume_frames[:10]:
    p_copy = p.copy()
    p_copy["img"] = raw_fume
    beat_combo_frames.append(p_copy)

# Part 4: Curled up weeping tears (11 frames)
for p in cry_frames[:11]:
    p_copy = p.copy()
    p_copy["img"] = raw_cry
    beat_combo_frames.append(p_copy)

build_animation("beat_combo", raw_swat, beat_combo_frames, scale=0.74, anchor_bottom=True)

# --- F. skid (573397256: 剎車) ---
raw_skid = get_sticker("lv46_22487600/35_573397256.png")
# Sliding from right to left with friction deceleration
skid_frames = [
    {"dx": 28, "dy": -2, "sx": 0.96, "sy": 1.04, "ang": 2},
    {"dx": 20, "dy": -1, "sx": 0.97, "sy": 1.03, "ang": 2},
    {"dx": 13, "dy": 0, "sx": 0.98, "sy": 1.02, "ang": 1},
    {"dx": 8, "dy": 1, "sx": 1.02, "sy": 0.98, "ang": 0},
    {"dx": 4, "dy": 2, "sx": 1.05, "sy": 0.95, "ang": -1},
    {"dx": 1, "dy": 3, "sx": 1.08, "sy": 0.92, "ang": -1},
    {"dx": 0, "dy": 2, "sx": 1.06, "sy": 0.94, "ang": 0},
    {"dx": -1, "dy": 1, "sx": 1.03, "sy": 0.97, "ang": 0},
    {"dx": 0, "dy": 0, "sx": 1.01, "sy": 0.99, "ang": 0},
    {"dx": 0, "dy": 0, "sx": 1.0, "sy": 1.0, "ang": 0},
    {"dx": 0, "dy": 0, "sx": 1.0, "sy": 1.0, "ang": 0},
    {"dx": 0, "dy": 0, "sx": 1.0, "sy": 1.0, "ang": 0}
]
build_animation("skid", raw_skid, skid_frames, scale=0.74, anchor_bottom=True)

# --- G. resist (573397257: 全身抗拒拉牽繩) ---
raw_resist = get_sticker("lv46_22487600/36_573397257.png")
resist_frames = [
    {"dx": 0, "dy": 0, "sx": 1.0, "sy": 1.0, "ang": 0},
    {"dx": -3, "dy": -1, "sx": 1.02, "sy": 0.98, "ang": -1},
    {"dx": -6, "dy": -2, "sx": 1.04, "sy": 0.96, "ang": -2},
    {"dx": -9, "dy": -3, "sx": 1.06, "sy": 0.94, "ang": -3},
    {"dx": -8, "dy": 1, "sx": 1.05, "sy": 0.95, "ang": -2},
    {"dx": -5, "dy": 2, "sx": 1.03, "sy": 0.97, "ang": -1},
    {"dx": -2, "dy": 1, "sx": 1.01, "sy": 0.99, "ang": 0},
    {"dx": -4, "dy": -1, "sx": 1.03, "sy": 0.97, "ang": -1},
    {"dx": -7, "dy": -2, "sx": 1.05, "sy": 0.95, "ang": -2},
    {"dx": -5, "dy": 0, "sx": 1.03, "sy": 0.97, "ang": -1},
    {"dx": -2, "dy": 0, "sx": 1.01, "sy": 0.99, "ang": 0},
    {"dx": 0, "dy": 0, "sx": 1.0, "sy": 1.0, "ang": 0}
]
build_animation("resist", raw_resist, resist_frames, scale=0.74, anchor_bottom=True)

# --- H. knife (573397261: 叼菜刀 我去去就回) ---
raw_knife = get_sticker("lv46_22487600/40_573397261.png")
knife_frames = [
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -2, "ang": 2, "sx": 0.99, "sy": 1.01},
    {"dy": -5, "ang": 4, "sx": 0.97, "sy": 1.03},
    {"dy": -7, "ang": 5, "sx": 0.96, "sy": 1.04},
    {"dy": -5, "ang": 3, "sx": 0.97, "sy": 1.03},
    {"dy": -2, "ang": 1, "sx": 0.99, "sy": 1.01},
    {"dy": 1, "ang": -1, "sx": 1.02, "sy": 0.98},
    {"dy": 2, "ang": -3, "sx": 1.03, "sy": 0.97},
    {"dy": 1, "ang": -2, "sx": 1.02, "sy": 0.98},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -1, "ang": 1, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("knife", raw_knife, knife_frames, scale=0.74, anchor_bottom=True)

# --- I. bounce (828016993: 歡呼雀躍) ---
raw_bounce = get_sticker("lv76_33346223/01_828016993.png")
bounce_frames = [
    {"dy": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 2, "sx": 1.05, "sy": 0.95},   # squat
    {"dy": -6, "sx": 0.96, "sy": 1.04},  # launch
    {"dy": -14, "sx": 0.94, "sy": 1.06}, # ascend
    {"dy": -18, "sx": 0.93, "sy": 1.07}, # apex
    {"dy": -14, "sx": 0.95, "sy": 1.05}, # descend
    {"dy": -4, "sx": 0.98, "sy": 1.02},
    {"dy": 4, "sx": 1.08, "sy": 0.92},   # land squash
    {"dy": 2, "sx": 1.03, "sy": 0.97},   # recover
    {"dy": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("bounce", raw_bounce, bounce_frames, scale=0.74, anchor_bottom=True)

# --- J. praise (573397223: 快點誇獎我) ---
raw_praise = get_sticker("lv46_22487600/02_573397223.png")
praise_frames = [
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -2, "ang": 2, "sx": 0.98, "sy": 1.02},
    {"dy": -4, "ang": 3, "sx": 0.97, "sy": 1.03},
    {"dy": -2, "ang": 1, "sx": 0.98, "sy": 1.02},
    {"dy": 1, "ang": -1, "sx": 1.02, "sy": 0.98},
    {"dy": 2, "ang": -3, "sx": 1.04, "sy": 0.96},
    {"dy": 1, "ang": -2, "sx": 1.02, "sy": 0.98},
    {"dy": -2, "ang": 1, "sx": 0.98, "sy": 1.02},
    {"dy": -4, "ang": 3, "sx": 0.97, "sy": 1.03},
    {"dy": -2, "ang": 1, "sx": 0.98, "sy": 1.02},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("praise", raw_praise, praise_frames, scale=0.74, anchor_bottom=True)

# --- K. galaxy (828017013: 宇宙思考放空) ---
raw_galaxy = get_sticker("lv76_33346223/21_828017013.png")
galaxy_frames = [
    {"dy": 0, "ang": 0, "scale": 0.74},
    {"dy": -2, "ang": 1, "scale": 0.745},
    {"dy": -4, "ang": 1, "scale": 0.75},
    {"dy": -6, "ang": 0, "scale": 0.755},
    {"dy": -5, "ang": -1, "scale": 0.75},
    {"dy": -3, "ang": -1, "scale": 0.745},
    {"dy": 0, "ang": 0, "scale": 0.74},
    {"dy": 2, "ang": 1, "scale": 0.735},
    {"dy": 3, "ang": 1, "scale": 0.73},
    {"dy": 2, "ang": 0, "scale": 0.735},
    {"dy": 1, "ang": -1, "scale": 0.74},
    {"dy": 0, "ang": 0, "scale": 0.74}
]
build_animation("galaxy", raw_galaxy, galaxy_frames, scale=0.74, anchor_bottom=True)

# --- L. work (828017012: 戴眼鏡鍵盤加班) ---
raw_work = get_sticker("lv76_33346223/20_828017012.png")
work_frames = [
    {"dy": 0, "dx": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -2, "dx": 1, "ang": 1, "sx": 0.99, "sy": 1.01},
    {"dy": 1, "dx": -1, "ang": -1, "sx": 1.02, "sy": 0.98},
    {"dy": -3, "dx": 2, "ang": 2, "sx": 0.98, "sy": 1.02},
    {"dy": 2, "dx": -2, "ang": -1, "sx": 1.03, "sy": 0.97},
    {"dy": -1, "dx": 1, "ang": 1, "sx": 0.99, "sy": 1.01},
    {"dy": 1, "dx": -1, "ang": -1, "sx": 1.01, "sy": 0.99},
    {"dy": -2, "dx": 2, "ang": 1, "sx": 0.98, "sy": 1.02},
    {"dy": 1, "dx": -2, "ang": -2, "sx": 1.02, "sy": 0.98},
    {"dy": -1, "dx": 1, "ang": 0, "sx": 0.99, "sy": 1.01},
    {"dy": 0, "dx": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "dx": 0, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("work", raw_work, work_frames, scale=0.74, anchor_bottom=True)

# --- M. melt (營業疲勞融化成一坨) ---
raw_melt_up = get_sticker("lv76_33346223/18_828017010.png")   # 失去精神
raw_melt_flat = get_sticker("lv46_22487600/32_573397253.png") # 營業疲勞大扁平
melt_frames = [
    # Deflating from upright
    {"img": raw_melt_up, "dy": 0, "sx": 1.0, "sy": 1.0},
    {"img": raw_melt_up, "dy": 2, "sx": 1.03, "sy": 0.97},
    {"img": raw_melt_up, "dy": 5, "sx": 1.08, "sy": 0.92},
    {"img": raw_melt_up, "dy": 8, "sx": 1.14, "sy": 0.86},
    # Transition to completely flat puddle
    {"img": raw_melt_flat, "dy": 6, "sx": 1.02, "sy": 0.98},
    {"img": raw_melt_flat, "dy": 7, "sx": 1.05, "sy": 0.95},
    {"img": raw_melt_flat, "dy": 8, "sx": 1.08, "sy": 0.92},
    {"img": raw_melt_flat, "dy": 8, "sx": 1.09, "sy": 0.91},
    {"img": raw_melt_flat, "dy": 7, "sx": 1.06, "sy": 0.94},
    {"img": raw_melt_flat, "dy": 6, "sx": 1.03, "sy": 0.97},
    {"img": raw_melt_flat, "dy": 5, "sx": 1.01, "sy": 0.99},
    {"img": raw_melt_flat, "dy": 5, "sx": 1.0, "sy": 1.0}
]
build_animation("melt", raw_melt_up, melt_frames, scale=0.74, anchor_bottom=True)

# --- N. sleep (779529780: 安 蓋被被) ---
raw_sleep = get_sticker("lv69_30973429/28_779529780.png")
sleep_frames = [
    {"dy": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -1, "sx": 0.995, "sy": 1.01},
    {"dy": -2, "sx": 0.99, "sy": 1.02},
    {"dy": -3, "sx": 0.985, "sy": 1.03},
    {"dy": -3, "sx": 0.985, "sy": 1.03},
    {"dy": -2, "sx": 0.99, "sy": 1.02},
    {"dy": -1, "sx": 0.995, "sy": 1.01},
    {"dy": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 1, "sx": 1.01, "sy": 0.99},
    {"dy": 2, "sx": 1.02, "sy": 0.98},
    {"dy": 1, "sx": 1.01, "sy": 0.99},
    {"dy": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("sleep", raw_sleep, sleep_frames, scale=0.74, anchor_bottom=True)

# --- O. peek (573397248: (盯) 桌邊探頭) ---
raw_peek = get_sticker("lv46_22487600/27_573397248.png")
peek_frames = [
    {"dy": 22, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 16, "ang": 1, "sx": 0.99, "sy": 1.01},
    {"dy": 10, "ang": 2, "sx": 0.98, "sy": 1.02},
    {"dy": 4, "ang": 1, "sx": 0.99, "sy": 1.01},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -2, "ang": -1, "sx": 1.01, "sy": 0.99},
    {"dy": -2, "ang": -1, "sx": 1.01, "sy": 0.99},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 6, "ang": 1, "sx": 0.99, "sy": 1.01},
    {"dy": 14, "ang": 1, "sx": 0.98, "sy": 1.02},
    {"dy": 20, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 24, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("peek", raw_peek, peek_frames, scale=0.74, anchor_bottom=True)

# --- P. fly (779529776: 披風超狗飛行) ---
raw_fly = get_sticker("lv69_30973429/24_779529776.png")
fly_frames = [
    {"dy": 0, "dx": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -4, "dx": -2, "ang": -2, "sx": 1.02, "sy": 0.98},
    {"dy": -8, "dx": -4, "ang": -4, "sx": 1.04, "sy": 0.96},
    {"dy": -12, "dx": -2, "ang": -2, "sx": 1.03, "sy": 0.97},
    {"dy": -10, "dx": 1, "ang": 0, "sx": 1.01, "sy": 0.99},
    {"dy": -6, "dx": 3, "ang": 2, "sx": 0.99, "sy": 1.01},
    {"dy": -2, "dx": 4, "ang": 3, "sx": 0.98, "sy": 1.02},
    {"dy": 2, "dx": 3, "ang": 2, "sx": 0.99, "sy": 1.01},
    {"dy": 4, "dx": 1, "ang": 1, "sx": 1.0, "sy": 1.0},
    {"dy": 2, "dx": 0, "ang": 0, "sx": 1.01, "sy": 0.99},
    {"dy": 0, "dx": -1, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "dx": 0, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("fly", raw_fly, fly_frames, scale=0.74, anchor_bottom=False)

# --- Q. eat (573397258: 趴在碗前大吃) ---
raw_eat = get_sticker("lv46_22487600/37_573397258.png")
eat_frames = [
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 2, "ang": -1, "sx": 1.02, "sy": 0.98},
    {"dy": 4, "ang": -2, "sx": 1.05, "sy": 0.95},
    {"dy": 2, "ang": -1, "sx": 1.02, "sy": 0.98},
    {"dy": -1, "ang": 0, "sx": 0.99, "sy": 1.01},
    {"dy": -3, "ang": 1, "sx": 0.97, "sy": 1.03},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 3, "ang": -2, "sx": 1.04, "sy": 0.96},
    {"dy": 5, "ang": -3, "sx": 1.06, "sy": 0.94},
    {"dy": 2, "ang": -1, "sx": 1.02, "sy": 0.98},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("eat", raw_eat, eat_frames, scale=0.74, anchor_bottom=True)

# --- R. roll (779529757: 打滾翻轉 zoomies) ---
raw_roll = get_sticker("lv69_30973429/05_779529757.png")
roll_frames = [
    {"dy": 0, "dx": -10, "ang": 0, "scale": 0.72},
    {"dy": -4, "dx": -6, "ang": 30, "scale": 0.72},
    {"dy": -8, "dx": -2, "ang": 65, "scale": 0.72},
    {"dy": -10, "dx": 2, "ang": 105, "scale": 0.72},
    {"dy": -12, "dx": 6, "ang": 150, "scale": 0.72},
    {"dy": -10, "dx": 10, "ang": 195, "scale": 0.72},
    {"dy": -8, "dx": 12, "ang": 240, "scale": 0.72},
    {"dy": -4, "dx": 10, "ang": 285, "scale": 0.72},
    {"dy": 0, "dx": 6, "ang": 330, "scale": 0.72},
    {"dy": 2, "dx": 2, "ang": 360, "scale": 0.72},
    {"dy": 1, "dx": -2, "ang": 360, "scale": 0.72},
    {"dy": 0, "dx": 0, "ang": 0, "scale": 0.72}
]
build_animation("roll", raw_roll, roll_frames, scale=0.72, anchor_bottom=False)

# --- S. beg (573397231: 我要摸摸) ---
raw_beg = get_sticker("lv46_22487600/10_573397231.png")
beg_frames = [
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": -3, "ang": 2, "sx": 0.98, "sy": 1.02},
    {"dy": -6, "ang": 3, "sx": 0.96, "sy": 1.04},
    {"dy": -7, "ang": 2, "sx": 0.95, "sy": 1.05},
    {"dy": -4, "ang": 0, "sx": 0.98, "sy": 1.02},
    {"dy": 0, "ang": -1, "sx": 1.01, "sy": 0.99},
    {"dy": 2, "ang": -2, "sx": 1.03, "sy": 0.97},
    {"dy": -2, "ang": 1, "sx": 0.98, "sy": 1.02},
    {"dy": -5, "ang": 3, "sx": 0.96, "sy": 1.04},
    {"dy": -3, "ang": 1, "sx": 0.98, "sy": 1.02},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0},
    {"dy": 0, "ang": 0, "sx": 1.0, "sy": 1.0}
]
build_animation("beg", raw_beg, beg_frames, scale=0.74, anchor_bottom=True)

print(f"All assets for chesthair_dog generated successfully in {OUTPUT_DIR}!")

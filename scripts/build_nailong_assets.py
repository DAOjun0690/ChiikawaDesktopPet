import os
import glob
import json
import zipfile
import subprocess
import urllib.request
import numpy as np
from PIL import Image, ImageOps

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUTPUT_DIR = os.path.join(REPO_ROOT, "assets", "optimized", "nailong")
ANIM_DIR = os.path.join(OUTPUT_DIR, "animations")
SPRITES_DIR = os.path.join(OUTPUT_DIR, "sprites")
ICONS_DIR = os.path.join(OUTPUT_DIR, "icons")
PACKS_DIR = os.path.join(REPO_ROOT, "assets", "packs")

SCRATCH_DIR = os.path.join(REPO_ROOT, "scratch_nailong")
if not os.path.exists(SCRATCH_DIR):
    SCRATCH_DIR = os.path.join(os.environ.get("USERPROFILE", ""), ".gemini", "antigravity", "brain", "3fb7ee75-d098-4e44-b2fe-72fc9596c985", "scratch")

APNG_DIR = os.path.join(SCRATCH_DIR, "nailong_apng")
FRAMES_DIR = os.path.join(SCRATCH_DIR, "nailong_frames")

FFMPEG = r"C:\Users\JEFF WANG\AppData\Local\ffmpegio\ffmpeg-downloader\ffmpeg\bin\ffmpeg.exe"
if not os.path.exists(FFMPEG):
    FFMPEG = "ffmpeg"

os.makedirs(ANIM_DIR, exist_ok=True)
os.makedirs(SPRITES_DIR, exist_ok=True)
os.makedirs(ICONS_DIR, exist_ok=True)
os.makedirs(PACKS_DIR, exist_ok=True)
os.makedirs(APNG_DIR, exist_ok=True)
os.makedirs(FRAMES_DIR, exist_ok=True)

TARGET_CANVAS = (280, 240)

def ensure_sticker_frames(idx):
    sub_dir = os.path.join(FRAMES_DIR, f"{idx:02d}")
    existing = sorted(glob.glob(os.path.join(sub_dir, "*.png")))
    if existing:
        return existing
    
    sid = 649651646 + (idx - 1)
    apng_file = os.path.join(APNG_DIR, f"{idx:02d}_{sid}.png")
    if not os.path.exists(apng_file):
        url = f"https://stickershop.line-scdn.net/stickershop/v1/sticker/{sid}/iPhone/sticker_animation@2x.png?v=1"
        print(f"Downloading #{idx:02d} ({sid})...")
        urllib.request.urlretrieve(url, apng_file)
    
    os.makedirs(sub_dir, exist_ok=True)
    subprocess.run([FFMPEG, "-y", "-i", apng_file, os.path.join(sub_dir, "%02d.png")],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
    return sorted(glob.glob(os.path.join(sub_dir, "*.png")))

def load_rgba(path):
    return Image.open(path).convert("RGBA")

def fit_to_canvas(img, target_size=TARGET_CANVAS, scale=0.78, scale_x=1.0, scale_y=1.0,
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

print("1. Ensuring all 24 sticker frames are extracted...")
frames = {}
for i in range(1, 25):
    frames[i] = ensure_sticker_frames(i)

print("2. Building Static Sprites...")
# spawn.png (Sticker 13: HI~ 揮手)
im_spawn_raw = load_rgba(frames[13][0])
im_spawn = fit_to_canvas(im_spawn_raw, scale=0.78, dy=0)
im_spawn.save(os.path.join(SPRITES_DIR, "spawn.png"))

# grabbed.png (Sticker 07: 嘴饞流口水)
im_grabbed_raw = load_rgba(frames[7][0])
im_grabbed = fit_to_canvas(im_grabbed_raw, scale=0.78, dy=-6)
im_grabbed.save(os.path.join(SPRITES_DIR, "grabbed.png"))

# grabbed1.png (Sticker 06: 大花棉襖眨眼)
im_grabbed1_raw = load_rgba(frames[6][0])
im_grabbed1 = fit_to_canvas(im_grabbed1_raw, scale=0.78, dy=-6)
im_grabbed1.save(os.path.join(SPRITES_DIR, "grabbed1.png"))

# shaken.png (Sticker 10: 鬼臉略略略)
im_shaken_raw = load_rgba(frames[10][0])
im_shaken = fit_to_canvas(im_shaken_raw, scale=0.78, dy=-4, anchor_bottom=False)
im_shaken.save(os.path.join(SPRITES_DIR, "shaken.png"))

# falling.png (Sticker 12: 超人俯衝)
im_falling_raw = load_rgba(frames[12][15])
im_falling = fit_to_canvas(im_falling_raw, scale=0.78, angle=-10, dy=-15, anchor_bottom=False)
im_falling.save(os.path.join(SPRITES_DIR, "falling.png"))

# fallingend.png (Sticker 16: 屁股朝天著地)
im_fallingend_raw = load_rgba(frames[16][3])
im_fallingend = fit_to_canvas(im_fallingend_raw, scale=0.78, dy=6)
im_fallingend.save(os.path.join(SPRITES_DIR, "fallingend.png"))

# jumpleft.png & jumpright.png (Sticker 20: 步步高昇騰躍)
im_jump_raw = load_rgba(frames[20][4])
im_jumpleft = fit_to_canvas(im_jump_raw, scale=0.78, angle=8, dx=-8, dy=-20, flip=True, anchor_bottom=False)
im_jumpright = fit_to_canvas(im_jump_raw, scale=0.78, angle=-8, dx=8, dy=-20, flip=False, anchor_bottom=False)
im_jumpleft.save(os.path.join(SPRITES_DIR, "jumpleft.png"))
im_jumpright.save(os.path.join(SPRITES_DIR, "jumpright.png"))

print("3. Building Menu/Tray Icon...")
# icon.png (256x256)
im_icon = fit_to_canvas(im_spawn_raw, target_size=(256, 256), scale=0.82, anchor_bottom=False)
im_icon.save(os.path.join(ICONS_DIR, "icon.png"))

print("4. Synthesizing Walk Loops (walkleft & walkright)...")
walk_dir_left = os.path.join(ANIM_DIR, "walkleft")
walk_dir_right = os.path.join(ANIM_DIR, "walkright")
os.makedirs(walk_dir_left, exist_ok=True)
os.makedirs(walk_dir_right, exist_ok=True)

# Use Sticker 22 frame 0 as neutral base
stand_base = load_rgba(frames[22][0])
walk_steps = [
    (0, 0, 1.0, 1.0, 0),
    (-3, -3, 0.98, 1.02, -2.5),
    (-6, -6, 0.96, 1.04, -4.5),
    (-3, -3, 0.98, 1.02, -2.0),
    (0, 0, 1.02, 0.98, 0),
    (3, -3, 0.98, 1.02, 2.5),
    (6, -6, 0.96, 1.04, 4.5),
    (3, -3, 0.98, 1.02, 2.0)
]

for idx, (dx, dy, sx, sy, ang) in enumerate(walk_steps, 1):
    # For walkleft, faces left (flip=True)
    wl = fit_to_canvas(stand_base, scale=0.78, angle=-ang, dx=dx, dy=dy, scale_x=sx, scale_y=sy, flip=True)
    # For walkright, faces right (flip=False)
    wr = fit_to_canvas(stand_base, scale=0.78, angle=ang, dx=dx, dy=dy, scale_x=sx, scale_y=sy, flip=False)
    wl.save(os.path.join(walk_dir_left, f"{idx}.png"))
    wr.save(os.path.join(walk_dir_right, f"{idx}.png"))

print("5. Exporting Specialized Action Animations...")
def export_anim(name, frame_list, scale=0.78, anchor_bottom=True):
    out_path = os.path.join(ANIM_DIR, name)
    os.makedirs(out_path, exist_ok=True)
    for idx, fpath in enumerate(frame_list, 1):
        im = load_rgba(fpath)
        canvas = fit_to_canvas(im, scale=scale, anchor_bottom=anchor_bottom)
        canvas.save(os.path.join(out_path, f"{idx}.png"))

# 1. bounce (Sticker 04: 笑口常開搓手肚肚晃動)
export_anim("bounce", frames[4])

# 2. fly (Sticker 12: 我來啦～超人飛撲)
export_anim("fly", frames[12], anchor_bottom=False)

# 3. watermelon (Sticker 08: 吃西瓜)
export_anim("watermelon", frames[8])

# 4. chicken (Sticker 14: 啃大雞腿)
export_anim("chicken", frames[14])

# 5. sleep (Sticker 11: 側躺睡覺打呼 Zzz)
export_anim("sleep", frames[11])

# 6. tease (Sticker 10: 鬼臉吐舌頭略略略)
export_anim("tease", frames[10])

# 7. cry (Sticker 09: 仰天噴淚大哭)
export_anim("cry", frames[9])

# 8. snort (Sticker 22: 傲嬌轉身哼～)
export_anim("snort", frames[22])

# 9. dance (Sticker 16: THANKS 感謝魔性晃臀鞠躬舞)
export_anim("dance", frames[16])

# 10. laugh (Sticker 17: 仰天哈哈大笑 HA HA)
export_anim("laugh", frames[17])

# 11. drool (Sticker 07: 嘴饞流口水)
export_anim("drool", frames[7])

# 12. liondance (Sticker 21: 新春舞獅恭喜發財)
export_anim("liondance", frames[21])

# 13. salute (Sticker 01: 恭喜拱手抱拳搓手)
export_anim("salute", frames[1])

# 14. pet (Sticker 23: 撫摸破殼小恐龍)
export_anim("pet", frames[23])

# 15. bye (Sticker 15: 轉身走開揮手 BYE BYE)
export_anim("bye", frames[15])

# 16. nod (Sticker 18: 歪頭認可嗯～)
export_anim("nod", frames[18])

# 17. bag (Sticker 02: 福袋探頭祈福)
export_anim("bag", frames[2])

# 18. cny (Sticker 06: 大花棉襖呆萌眨眼)
export_anim("cny", frames[6])

# 19. hop (Sticker 20: 步步高昇騰躍)
export_anim("hop", frames[20])

# 20. gasp (Sticker 19: 捂嘴偷笑)
export_anim("gasp", frames[19])

# 21. hi (Sticker 13: 揮手打招呼 HI~)
export_anim("hi", frames[13])

print("6. Creating assets/packs/nailong.zip...")
zip_path = os.path.join(PACKS_DIR, "nailong.zip")
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
    for root, dirs, files in os.walk(OUTPUT_DIR):
        for f in files:
            full_path = os.path.join(root, f)
            rel_path = os.path.relpath(full_path, OUTPUT_DIR)
            zf.write(full_path, rel_path)

print(f"Zip created successfully: {zip_path} ({os.path.getsize(zip_path)} bytes)")

# Also copy zip to output bin folders if they exist
for bin_dir in glob.glob(os.path.join(REPO_ROOT, "src", "**", "bin", "**", "assets"), recursive=True):
    if os.path.isdir(bin_dir):
        dest_zip = os.path.join(bin_dir, "nailong.zip")
        with open(zip_path, "rb") as sf, open(dest_zip, "wb") as df:
            df.write(sf.read())
        print(f"Copied to {dest_zip}")

print("Nailong asset pipeline finished successfully!")

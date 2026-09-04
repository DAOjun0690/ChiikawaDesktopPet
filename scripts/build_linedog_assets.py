import os
import glob
import urllib.request
import subprocess
from PIL import Image, ImageOps

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUTPUT_DIR = os.path.join(REPO_ROOT, "assets", "optimized", "linedog")
ANIM_DIR = os.path.join(OUTPUT_DIR, "animations")
SPRITES_DIR = os.path.join(OUTPUT_DIR, "sprites")
ICONS_DIR = os.path.join(OUTPUT_DIR, "icons")

SCRATCH_DIR = os.path.join(os.environ.get("USERPROFILE", ""), ".gemini", "antigravity", "brain", "330e335c-0932-4bf7-8343-4fb00ac1c6ce", "scratch")
STICON_ANIM_DIR = os.path.join(SCRATCH_DIR, "sticons_anim")
STICON_FRAMES_DIR = os.path.join(SCRATCH_DIR, "sticons_frames")

FFMPEG = r"C:\Users\JEFF WANG\AppData\Local\ffmpegio\ffmpeg-downloader\ffmpeg\bin\ffmpeg.exe"
if not os.path.exists(FFMPEG):
    FFMPEG = "ffmpeg"

os.makedirs(ANIM_DIR, exist_ok=True)
os.makedirs(SPRITES_DIR, exist_ok=True)
os.makedirs(ICONS_DIR, exist_ok=True)
os.makedirs(STICON_ANIM_DIR, exist_ok=True)
os.makedirs(STICON_FRAMES_DIR, exist_ok=True)

def ensure_sticon_frames(num_str):
    dest_sub = os.path.join(STICON_FRAMES_DIR, num_str)
    existing = glob.glob(os.path.join(dest_sub, "*.png"))
    if existing:
        return sorted(existing)
    
    apng_path = os.path.join(STICON_ANIM_DIR, f"{num_str}_anim.png")
    if not os.path.exists(apng_path):
        url = f"https://stickershop.line-scdn.net/sticonshop/v1/sticon/666177dd45171c6ef90f8160/iPhone/{num_str}_animation.png?v=3"
        print(f"Downloading {num_str} APNG...")
        urllib.request.urlretrieve(url, apng_path)
    
    os.makedirs(dest_sub, exist_ok=True)
    subprocess.run([FFMPEG, "-y", "-i", apng_path, os.path.join(dest_sub, "%02d.png")],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
    return sorted(glob.glob(os.path.join(dest_sub, "*.png")))

def load_rgba(path):
    return Image.open(path).convert("RGBA")

def fit_to_canvas(img, target_size=(240, 240), scale=1.2, angle=0, dx=0, dy=0, flip=False):
    w, h = img.size
    new_w = int(w * scale)
    new_h = int(h * scale)
    scaled = img.resize((new_w, new_h), Image.Resampling.LANCZOS)
    if angle != 0:
        scaled = scaled.rotate(angle, resample=Image.Resampling.BICUBIC, expand=False)
    if flip:
        scaled = ImageOps.mirror(scaled)
    canvas = Image.new("RGBA", target_size, (0, 0, 0, 0))
    x = (target_size[0] - new_w) // 2 + dx
    y = (target_size[1] - new_h) // 2 + dy
    canvas.paste(scaled, (x, y), scaled)
    return canvas

print("1. Preparing source sticon frames...")
for i in [1, 3, 5, 6, 7, 9, 10, 11, 13, 14, 15, 17, 18, 21, 23, 25, 26, 28, 29, 30, 31, 33, 38, 40]:
    ensure_sticon_frames(f"{i:03d}")

print("2. Building Static Sprites...")
# spawn.png (001 first frame)
f001 = ensure_sticon_frames("001")
im_spawn = fit_to_canvas(load_rgba(f001[0]), (240, 240), scale=1.2)
im_spawn.save(os.path.join(SPRITES_DIR, "spawn.png"))

# grabbed.png (011 surprise)
f011 = ensure_sticon_frames("011")
im_grabbed = fit_to_canvas(load_rgba(f011[4]), (240, 240), scale=1.2)
im_grabbed.save(os.path.join(SPRITES_DIR, "grabbed.png"))

# grabbed1.png (006 shocked gold dog)
f006 = ensure_sticon_frames("006")
im_grabbed1 = fit_to_canvas(load_rgba(f006[4]), (240, 240), scale=1.2)
im_grabbed1.save(os.path.join(SPRITES_DIR, "grabbed1.png"))

# shaken.png (025 frozen in ice shivering)
f025 = ensure_sticon_frames("025")
im_shaken = fit_to_canvas(load_rgba(f025[1]), (240, 240), scale=1.2)
im_shaken.save(os.path.join(SPRITES_DIR, "shaken.png"))

# falling.png (015 sad gloom falling)
f015 = ensure_sticon_frames("015")
im_falling = fit_to_canvas(load_rgba(f015[5]), (240, 240), scale=1.2, dy=-4)
im_falling.save(os.path.join(SPRITES_DIR, "falling.png"))

# fallingend.png (030 flat round mop blob)
f030 = ensure_sticon_frames("030")
im_fallingend = fit_to_canvas(load_rgba(f030[3]), (240, 240), scale=1.2, dy=10)
im_fallingend.save(os.path.join(SPRITES_DIR, "fallingend.png"))

# jumpleft.png & jumpright.png
im_jump_base = load_rgba(f001[0])
im_jumpleft = fit_to_canvas(im_jump_base, (240, 240), scale=1.2, angle=12, dx=-6, dy=-14, flip=False)
im_jumpright = fit_to_canvas(im_jump_base, (240, 240), scale=1.2, angle=-12, dx=6, dy=-14, flip=True)
im_jumpleft.save(os.path.join(SPRITES_DIR, "jumpleft.png"))
im_jumpright.save(os.path.join(SPRITES_DIR, "jumpright.png"))

print("3. Building Menu/Tray Icon...")
# icon.png (256x256)
im_icon = fit_to_canvas(load_rgba(f001[0]), (256, 256), scale=1.28)
im_icon.save(os.path.join(ICONS_DIR, "icon.png"))

print("4. Synthesizing Walk Loops (walkleft & walkright)...")
# Using active frames of 001 with tilt and vertical bobbing
walk_dir_left = os.path.join(ANIM_DIR, "walkleft")
walk_dir_right = os.path.join(ANIM_DIR, "walkright")
os.makedirs(walk_dir_left, exist_ok=True)
os.makedirs(walk_dir_right, exist_ok=True)

# 8-step fluid waddle cycle
walk_steps = [
    (load_rgba(f001[0]), 0, 0, 0),
    (load_rgba(f001[1]), 3, -2, -4),
    (load_rgba(f001[3]), 5, -4, -8),
    (load_rgba(f001[4]), 2, -2, -3),
    (load_rgba(f001[0]), 0, 0, 0),
    (load_rgba(f001[5]), -3, 2, -4),
    (load_rgba(f001[7]), -5, 4, -8),
    (load_rgba(f001[8]), -2, 2, -3)
]

for idx, (base_im, ang, dx, dy) in enumerate(walk_steps, 1):
    wl = fit_to_canvas(base_im, (240, 240), scale=1.2, angle=ang, dx=dx, dy=dy, flip=False)
    wr = fit_to_canvas(base_im, (240, 240), scale=1.2, angle=-ang, dx=-dx, dy=dy, flip=True)
    wl.save(os.path.join(walk_dir_left, f"{idx}.png"))
    wr.save(os.path.join(walk_dir_right, f"{idx}.png"))

print("5. Exporting Specialized Action Animations...")

def export_animation(anim_name, frame_files, max_frames=None, scale=1.2, target_size=(240, 240)):
    out_anim_dir = os.path.join(ANIM_DIR, anim_name)
    os.makedirs(out_anim_dir, exist_ok=True)
    files = frame_files[:max_frames] if max_frames else frame_files
    for idx, f in enumerate(files, 1):
        im = load_rgba(f)
        canvas = fit_to_canvas(im, target_size=target_size, scale=scale)
        canvas.save(os.path.join(out_anim_dir, f"{idx}.png"))

# bounce (001: 10 active frames)
export_animation("bounce", [ensure_sticon_frames("001")[i] for i in range(10)])

# cheer (021: 10 frames pompom cheer)
export_animation("cheer", [ensure_sticon_frames("021")[i] for i in range(10)])

# peek (033: 10 frames peeking over ledge)
export_animation("peek", [ensure_sticon_frames("033")[i] for i in range(10)])

# sleep (040: 15 frames nightcap sleeping)
export_animation("sleep", [ensure_sticon_frames("040")[i] for i in range(15)])

# freeze (025: 10 frames frozen ice shivering)
export_animation("freeze", [ensure_sticon_frames("025")[i] for i in range(10)])

# storm (038: 10 frames rainstorm scream)
export_animation("storm", [ensure_sticon_frames("038")[i] for i in range(10)])

# cry (007: 10 frames crying tears)
export_animation("cry", [ensure_sticon_frames("007")[i] for i in range(10)])

# heart (028: 10 frames floating hearts)
export_animation("heart", [ensure_sticon_frames("028")[i] for i in range(10)])

# fever (017: 10 frames ice pack shivering)
export_animation("fever", [ensure_sticon_frames("017")[i] for i in range(10)])

# stone (018: 10 frames stone petrified)
export_animation("stone", [ensure_sticon_frames("018")[i] for i in range(10)])

# clap (013: 10 frames clapping paws)
export_animation("clap", [ensure_sticon_frames("013")[i] for i in range(10)])

# party (014: 11 frames party hat & blower)
export_animation("party", [ensure_sticon_frames("014")[i] for i in range(11)])

# sunglasses (029: 20 frames gold puppy sunglasses)
export_animation("sunglasses", ensure_sticon_frames("029"))

# angry (005: 10 frames angry steaming)
export_animation("angry", [ensure_sticon_frames("005")[i] for i in range(10)])

# think (026: 10 frames pondering chin rub)
export_animation("think", [ensure_sticon_frames("026")[i] for i in range(10)])

# sparkle (023: 15 frames tongue out star)
export_animation("sparkle", [ensure_sticon_frames("023")[i] for i in range(15)])

# cuddle (009 + 010: 24 frames side-by-side composite)
f009 = ensure_sticon_frames("009")
f010 = ensure_sticon_frames("010")
cuddle_dir = os.path.join(ANIM_DIR, "cuddle")
os.makedirs(cuddle_dir, exist_ok=True)
for idx in range(1, 25):
    im9 = load_rgba(f009[idx - 1])
    im10 = load_rgba(f010[idx - 1])
    # Composite side-by-side on 320x240 canvas
    comb = Image.new("RGBA", (320, 240), (0, 0, 0, 0))
    # Scale each by 1.15
    w9, h9 = int(im9.width * 1.15), int(im9.height * 1.15)
    w10, h10 = int(im10.width * 1.15), int(im10.height * 1.15)
    im9_s = im9.resize((w9, h9), Image.Resampling.LANCZOS)
    im10_s = im10.resize((w10, h10), Image.Resampling.LANCZOS)
    comb.paste(im9_s, (0, 240 - h9), im9_s)
    comb.paste(im10_s, (w9 - 20, 240 - h10), im10_s)
    comb.save(os.path.join(cuddle_dir, f"{idx}.png"))

print("All linedog assets built successfully in assets/optimized/linedog!")

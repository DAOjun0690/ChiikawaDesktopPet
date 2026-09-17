# scripts/build_ditto_assets.py
import os
import re
import html
import json
import urllib.request
import numpy as np
from PIL import Image, ImageSequence, ImageOps

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUTPUT_DIR = os.path.join(REPO_ROOT, "assets", "optimized", "ditto")
ANIM_DIR = os.path.join(OUTPUT_DIR, "animations")
SPRITES_DIR = os.path.join(OUTPUT_DIR, "sprites")
ICONS_DIR = os.path.join(OUTPUT_DIR, "icons")

SCRATCH_DIR = os.environ.get(
    "DITTO_STICKER_SCRATCH_DIR",
    os.path.join(REPO_ROOT, "scratch", "ditto_raw")
)

os.makedirs(ANIM_DIR, exist_ok=True)
os.makedirs(SPRITES_DIR, exist_ok=True)
os.makedirs(ICONS_DIR, exist_ok=True)
os.makedirs(SCRATCH_DIR, exist_ok=True)

TARGET_SIZE = (320, 300)
GROUND_Y = 294

# 24 Sticker metadata mapping
# (Sticker ID, Internal Action Name, Chinese Description)
STICKER_MAP = [
    ("871611897", "wave", "雙手熱情打招呼"),
    ("871611898", "stretch", "萬歲大伸展"),
    ("871611899", "surprise", "嚇一大跳"),
    ("871611900", "waddle", "左右搖曳晃動"),
    ("871611901", "sparkle", "崇拜星星眼"),
    ("871611902", "heart", "抱著大愛心"),
    ("871611903", "bounce", "原地疑惑彈跳"),
    ("871611904", "dance", "快樂扭扭舞"),
    ("871611905", "shiver", "瑟瑟發抖"),
    ("871611906", "giggle", "掩嘴偷笑"),
    ("871611907", "squish", "像麻糬壓扁拉長"),
    ("871611908", "bye", "單手揮手掰掰"),
    ("871611909", "sing", "引吭高歌"),
    ("871611910", "melt", "融化成史萊姆"),
    ("871611911", "clone", "影分身百變怪"),
    ("871611912", "peek", "歪頭暗中觀察"),
    ("871611913", "sleep", "抱枕頭安穩睡覺"),
    ("871611914", "sigh", "吹泡泡放鬆"),
    ("871611915", "cry", "瀑布大噴淚"),
    ("871611916", "sweat", "滿頭大汗三條線"),
    ("871611917", "mood", "晴雨雙重心情"),
    ("871611918", "grumpy", "憋氣忍耐生悶氣"),
    ("871611919", "pikachu", "變身皮卡丘"),
    ("871611920", "pokeball", "伸手抓精靈球"),
]

def ensure_stickers():
    """Ensure all 24 raw APNG stickers are downloaded."""
    for idx, (sid, name, desc) in enumerate(STICKER_MAP, 1):
        target_path = os.path.join(SCRATCH_DIR, f"{idx:02d}_{sid}.png")
        if not os.path.exists(target_path):
            url = f"https://stickershop.line-scdn.net/stickershop/v1/sticker/{sid}/iPhone/sticker_animation@2x.png?v=1"
            print(f"Downloading {idx:02d} ({name}: {sid})...")
            urllib.request.urlretrieve(url, target_path)

def fit_to_ground(im_frame, orig_size, max_bottom):
    """Pastes an APNG frame onto the 320x300 canvas aligned to the ground level."""
    canvas = Image.new("RGBA", TARGET_SIZE, (0, 0, 0, 0))
    x_offset = (TARGET_SIZE[0] - orig_size[0]) // 2
    y_offset = GROUND_Y - max_bottom
    canvas.paste(im_frame, (x_offset, y_offset), im_frame)
    return canvas

def export_animation(idx, sid, name):
    """Exports all frames of an APNG sticker into the animation directory."""
    raw_path = os.path.join(SCRATCH_DIR, f"{idx:02d}_{sid}.png")
    out_dir = os.path.join(ANIM_DIR, name)
    os.makedirs(out_dir, exist_ok=True)
    
    with Image.open(raw_path) as im:
        orig_size = im.size
        # Find global max_bottom across all frames
        max_bottom = 0
        frames = []
        for frame in ImageSequence.Iterator(im):
            frame_rgba = frame.convert("RGBA")
            frames.append(frame_rgba)
            arr = np.array(frame_rgba)
            if arr.shape[2] == 4:
                nz = np.where(arr[:, :, 3] > 10)
                if len(nz[0]) > 0:
                    max_bottom = max(max_bottom, nz[0].max())
        
        for f_idx, frame_rgba in enumerate(frames, 1):
            grounded = fit_to_ground(frame_rgba, orig_size, max_bottom)
            grounded.save(os.path.join(out_dir, f"{f_idx}.png"))
    print(f"  [+] Exported animation: {name} ({len(frames)} frames)")

def build_base_sprites():
    """Build core sprites: spawn, grabbed, grabbed1, shaken, falling, fallingend, jumpleft, jumpright, icon."""
    print("Building Base Sprites...")
    # Base clean Ditto from sticker 11 (squish) frame 3
    squish_path = os.path.join(SCRATCH_DIR, "11_871611907.png")
    with Image.open(squish_path) as im:
        frames = [f.convert("RGBA") for f in ImageSequence.Iterator(im)]
        raw_ditto = frames[2]  # frame 3 (0-indexed 2)
    
    # Base shaken from sticker 09 (shiver) frame 11
    shiver_path = os.path.join(SCRATCH_DIR, "09_871611905.png")
    with Image.open(shiver_path) as im:
        frames_shiver = [f.convert("RGBA") for f in ImageSequence.Iterator(im)]
        raw_shaken = frames_shiver[10]

    # Base fallingend from sticker 14 (melt) last frame
    melt_path = os.path.join(SCRATCH_DIR, "14_871611910.png")
    with Image.open(melt_path) as im:
        frames_melt = [f.convert("RGBA") for f in ImageSequence.Iterator(im)]
        raw_fallingend = frames_melt[-1]

    # Helper to fit a single image with transform onto 320x300
    def fit_single(img, sx=1.0, sy=1.0, ang=0, dx=0, dy=0, flip=False, anchor_ground=True):
        w, h = img.size
        new_w = max(1, int(w * sx))
        new_h = max(1, int(h * sy))
        scaled = img.resize((new_w, new_h), Image.Resampling.LANCZOS)
        if ang != 0:
            scaled = scaled.rotate(ang, resample=Image.Resampling.BICUBIC, expand=True)
            new_w, new_h = scaled.size
        if flip:
            scaled = ImageOps.mirror(scaled)
        
        canvas = Image.new("RGBA", TARGET_SIZE, (0, 0, 0, 0))
        arr = np.array(scaled)
        nz = np.where(arr[:, :, 3] > 10)
        if anchor_ground and len(nz[0]) > 0:
            content_bottom = nz[0].max()
            y = GROUND_Y + dy - content_bottom
            x = (TARGET_SIZE[0] - new_w) // 2 + dx
        else:
            x = (TARGET_SIZE[0] - new_w) // 2 + dx
            y = (TARGET_SIZE[1] - new_h) // 2 + dy
        canvas.paste(scaled, (x, y), scaled)
        return canvas

    # 1. spawn.png
    spawn_im = fit_single(raw_ditto, sx=1.0, sy=1.0)
    spawn_im.save(os.path.join(SPRITES_DIR, "spawn.png"))

    # 2. grabbed.png & grabbed1.png (stretched jelly water balloon)
    grabbed_im = fit_single(raw_ditto, sx=0.88, sy=1.18, dy=-14, anchor_ground=False)
    grabbed_im.save(os.path.join(SPRITES_DIR, "grabbed.png"))

    grabbed1_im = fit_single(raw_ditto, sx=0.85, sy=1.22, ang=2, dy=-16, anchor_ground=False)
    grabbed1_im.save(os.path.join(SPRITES_DIR, "grabbed1.png"))

    # 3. shaken.png (trembling / stunned)
    shaken_im = fit_single(raw_shaken, sx=1.0, sy=1.0)
    shaken_im.save(os.path.join(SPRITES_DIR, "shaken.png"))

    # 4. falling.png (downward stretched jelly)
    falling_im = fit_single(raw_ditto, sx=0.86, sy=1.20, dy=-18, anchor_ground=False)
    falling_im.save(os.path.join(SPRITES_DIR, "falling.png"))

    # 5. fallingend.png (pancake melted impact)
    fallingend_im = fit_single(raw_fallingend, sx=1.02, sy=0.98, dy=2)
    fallingend_im.save(os.path.join(SPRITES_DIR, "fallingend.png"))

    # 6. jumpleft.png & jumpright.png (upward leaning jump)
    jumpleft_im = fit_single(raw_ditto, sx=0.92, sy=1.12, ang=5, dx=-6, dy=-14, flip=False, anchor_ground=False)
    jumpright_im = fit_single(raw_ditto, sx=0.92, sy=1.12, ang=-5, dx=6, dy=-14, flip=True, anchor_ground=False)
    jumpleft_im.save(os.path.join(SPRITES_DIR, "jumpleft.png"))
    jumpright_im.save(os.path.join(SPRITES_DIR, "jumpright.png"))

    # 7. icons/icon.png (256x256)
    icon_canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    bbox = raw_ditto.getbbox()
    cropped = raw_ditto.crop(bbox)
    cropped.thumbnail((220, 220), Image.Resampling.LANCZOS)
    ox = (256 - cropped.width) // 2
    oy = (256 - cropped.height) // 2
    icon_canvas.paste(cropped, (ox, oy), cropped)
    icon_canvas.save(os.path.join(ICONS_DIR, "icon.png"))
    print("  [+] Base sprites & icon built successfully.")

def build_walk_loops():
    """Build 10-frame fluid waddle walk loops for walkleft and walkright."""
    print("Building Walk Loops (walkleft & walkright)...")
    squish_path = os.path.join(SCRATCH_DIR, "11_871611907.png")
    with Image.open(squish_path) as im:
        frames = [f.convert("RGBA") for f in ImageSequence.Iterator(im)]
        im_ditto = frames[2]

    walk_cycle = [
        # (dx, dy, sx, sy, ang)
        (0, 0, 1.0, 1.0, 0),
        (-4, -4, 0.95, 1.05, 3),
        (-8, -8, 0.93, 1.08, 5),
        (-6, -2, 1.05, 0.95, 3),
        (-3, 0, 1.08, 0.92, 0),
        (0, 0, 1.0, 1.0, 0),
        (3, -4, 0.95, 1.05, -3),
        (6, -8, 0.93, 1.08, -5),
        (4, -2, 1.05, 0.95, -3),
        (2, 0, 1.08, 0.92, 0),
    ]

    out_left = os.path.join(ANIM_DIR, "walkleft")
    out_right = os.path.join(ANIM_DIR, "walkright")
    os.makedirs(out_left, exist_ok=True)
    os.makedirs(out_right, exist_ok=True)

    def render_walk_frame(img, dx, dy, sx, sy, ang, flip):
        w, h = img.size
        new_w = max(1, int(w * sx))
        new_h = max(1, int(h * sy))
        scaled = img.resize((new_w, new_h), Image.Resampling.LANCZOS)
        if ang != 0:
            scaled = scaled.rotate(ang, resample=Image.Resampling.BICUBIC, expand=True)
            new_w, new_h = scaled.size
        if flip:
            scaled = ImageOps.mirror(scaled)
        canvas = Image.new("RGBA", TARGET_SIZE, (0, 0, 0, 0))
        arr = np.array(scaled)
        nz = np.where(arr[:, :, 3] > 10)
        if len(nz[0]) > 0:
            content_bottom = nz[0].max()
            y = GROUND_Y + dy - content_bottom
            x = (TARGET_SIZE[0] - new_w) // 2 + dx
        else:
            x = (TARGET_SIZE[0] - new_w) // 2 + dx
            y = (TARGET_SIZE[1] - new_h) // 2 + dy
        canvas.paste(scaled, (x, y), scaled)
        return canvas

    for idx, (dx, dy, sx, sy, ang) in enumerate(walk_cycle, 1):
        fl = render_walk_frame(im_ditto, dx=dx, dy=dy, sx=sx, sy=sy, ang=ang, flip=False)
        fl.save(os.path.join(out_left, f"{idx}.png"))
        
        fr = render_walk_frame(im_ditto, dx=-dx, dy=dy, sx=sx, sy=sy, ang=-ang, flip=True)
        fr.save(os.path.join(out_right, f"{idx}.png"))
    print("  [+] Walk loops built successfully.")

def main():
    print("=== Building Assets for Ditto (百變怪) ===")
    ensure_stickers()
    build_base_sprites()
    build_walk_loops()
    print("Exporting 24 Animated Stickers...")
    for idx, (sid, name, desc) in enumerate(STICKER_MAP, 1):
        export_animation(idx, sid, name)
    print("=== Asset Generation Completed! ===")

if __name__ == "__main__":
    main()

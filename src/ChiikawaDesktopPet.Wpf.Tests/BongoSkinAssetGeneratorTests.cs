using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class BongoSkinAssetGeneratorTests
{
    private const int Width = 320;
    private const int Height = 240;

    [Fact]
    public void GenerateAllBongoSkins_AssetsCreatedSuccessfully()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "LICENSE.md")))
        {
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        string bongoRoot = Path.Combine(current, "assets", "bongo");
        Directory.CreateDirectory(bongoRoot);

        // Chiikawa (吉伊卡哇) - Cream white fur, round ears, pastel pink accents
        GenerateSkin(
            repoRoot: current,
            root: bongoRoot,
            key: "chiikawa",
            displayName: "Chiikawa (吉伊卡哇)",
            desc: "吉伊卡哇認真打字、小爪握持滑鼠努力工作的可愛模樣！",
            furColor: Color.FromRgb(254, 252, 248),
            accentColor: Color.FromRgb(255, 174, 201),
            mousepadColor: Color.FromRgb(255, 235, 242),
            mousepadBorderColor: Color.FromRgb(255, 182, 193),
            charType: CharacterKind.Chiikawa);

        // Hachiware (小八貓) - Cream fur, blue "八" bangs, cat ears, sky blue accents
        GenerateSkin(
            repoRoot: current,
            root: bongoRoot,
            key: "hachiware",
            displayName: "Hachiware (小八貓)",
            desc: "八字瀏海小八貓，左手滑鼠右手鍵盤，打字充滿好奇心與活力！",
            furColor: Color.FromRgb(254, 252, 248),
            accentColor: Color.FromRgb(91, 146, 189),
            mousepadColor: Color.FromRgb(232, 242, 255),
            mousepadBorderColor: Color.FromRgb(154, 194, 238),
            charType: CharacterKind.Hachiware);

        // Usagi (兔兔烏薩奇) - Light pastel yellow fur, long ears, warm yellow/gold accents
        GenerateSkin(
            repoRoot: current,
            root: bongoRoot,
            key: "usagi",
            displayName: "Usagi (兔兔烏薩奇)",
            desc: "兔兔烏薩奇狂暴敲鍵盤與高速滑鼠點擊，超頻速度破表高喊烏哈（YAHA）！",
            furColor: Color.FromRgb(255, 246, 197),
            accentColor: Color.FromRgb(255, 209, 102),
            mousepadColor: Color.FromRgb(255, 249, 224),
            mousepadBorderColor: Color.FromRgb(244, 212, 134),
            charType: CharacterKind.Usagi);

        // ChesthairMonkey (胸毛公寓 猴子朋友) - Tan/brown fur, warm peach/banana accents
        GenerateSkin(
            repoRoot: current,
            root: bongoRoot,
            key: "chesthair_monkey",
            displayName: "胸毛公寓 猴子朋友",
            desc: "胸毛公寓猴子朋友認真敲打鍵盤，超頻速度爆發魔性尖叫！",
            furColor: Color.FromRgb(173, 126, 81),
            accentColor: Color.FromRgb(249, 230, 208),
            mousepadColor: Color.FromRgb(255, 235, 150),
            mousepadBorderColor: Color.FromRgb(220, 190, 110),
            charType: CharacterKind.ChesthairMonkey);

        Assert.True(File.Exists(Path.Combine(bongoRoot, "chiikawa", "body.png")));
        Assert.True(File.Exists(Path.Combine(bongoRoot, "hachiware", "body.png")));
        Assert.True(File.Exists(Path.Combine(bongoRoot, "usagi", "body.png")));
        Assert.True(File.Exists(Path.Combine(bongoRoot, "chesthair_monkey", "body.png")));
    }

    private enum CharacterKind { Chiikawa, Hachiware, Usagi, ChesthairMonkey }

    private void GenerateSkin(
        string repoRoot,
        string root,
        string key,
        string displayName,
        string desc,
        Color furColor,
        Color accentColor,
        Color mousepadColor,
        Color mousepadBorderColor,
        CharacterKind charType)
    {
        string dir = Path.Combine(root, key);
        Directory.CreateDirectory(dir);

        // Load authentic pet sprites from assets/optimized/
        string spritesDir = Path.Combine(repoRoot, "assets", "optimized", key, "sprites");
        string normalPath = Path.Combine(spritesDir, "spawn.png");
        string focusedPath = charType switch
        {
            CharacterKind.ChesthairMonkey => Path.Combine(repoRoot, "assets", "optimized", key, "animations", "keyboard", "1.png"),
            _ => Path.Combine(spritesDir, "spawn2.png")
        };
        string overdrivePath = charType switch
        {
            CharacterKind.ChesthairMonkey => Path.Combine(repoRoot, "assets", "optimized", key, "animations", "scream", "1.png"),
            CharacterKind.Usagi => Path.Combine(spritesDir, "shaken.png"),
            _ => Path.Combine(spritesDir, "sad.png")
        };

        var normalBmp = LoadCroppedSprite(normalPath);
        var focusedBmp = LoadCroppedSprite(focusedPath);
        var overdriveBmp = LoadCroppedSprite(overdrivePath);

        var inkBrush = new SolidColorBrush(Color.FromRgb(56, 36, 27)); // #38241B authentic manga ink
        var inkPen = new Pen(inkBrush, 3.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var furBrush = new SolidColorBrush(furColor);

        // Rectangles for positioning character behind desk (desk starts at Y=126)
        // With desk at Y=126, the standing hands (Y ~ 128..135) and feet are 100% hidden behind the desk!
        Rect petRectNormal;
        Rect petRectFocused;
        Rect petRectOverdrive;

        switch (charType)
        {
            case CharacterKind.Chiikawa:
                petRectNormal = new Rect(91, 16, 138, 152);
                petRectFocused = new Rect(91, 16, 138, 152);
                petRectOverdrive = new Rect(91, 18, 138, 150);
                break;
            case CharacterKind.Hachiware:
                petRectNormal = new Rect(91, 14, 138, 154);
                petRectFocused = new Rect(91, 14, 138, 154);
                petRectOverdrive = new Rect(91, 16, 138, 152);
                break;
            case CharacterKind.ChesthairMonkey:
                petRectNormal = new Rect(85, 4, 150, 148);
                petRectFocused = new Rect(85, 4, 150, 148);
                petRectOverdrive = new Rect(85, 4, 150, 148);
                break;
            case CharacterKind.Usagi:
            default:
                petRectNormal = new Rect(98, 2, 124, 172);
                petRectFocused = new Rect(98, 2, 124, 172);
                petRectOverdrive = new Rect(98, 6, 124, 168);
                break;
        }

        // 1. Body layer: Desk + Mousepad (LEFT) + Keyboard (RIGHT) - NO character on body.png
        SavePng(Path.Combine(dir, "body.png"), dc =>
        {
            // Draw Desk (warm wooden/cream tone with hand-drawn manga contours)
            var deskBrush = new SolidColorBrush(Color.FromRgb(246, 240, 232));
            var deskPen = new Pen(inkBrush, 3.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawRoundedRectangle(deskBrush, deskPen, new Rect(14, 126, 292, 108), 14, 14);

            // Subtle desk lip shadow for depth
            var lipBrush = new SolidColorBrush(Color.FromArgb(40, 180, 150, 130));
            dc.DrawRoundedRectangle(lipBrush, null, new Rect(18, 226, 284, 6), 3, 3);

            // BongoCat Layout: MOUSEPAD ON THE LEFT, KEYBOARD ON THE RIGHT!
            // 1a. Mousepad on the LEFT: Rect(26, 134, 114, 90)
            var padBrush = new SolidColorBrush(mousepadColor);
            var padPen = new Pen(new SolidColorBrush(mousepadBorderColor), 2.5);
            dc.DrawRoundedRectangle(padBrush, padPen, new Rect(26, 134, 114, 90), 10, 10);

            // Cute mousepad corner emblem (mini star / emblem)
            var starBrush = new SolidColorBrush(accentColor);
            dc.DrawEllipse(starBrush, null, new Point(40, 148), 3.5, 3.5);
            dc.DrawEllipse(starBrush, null, new Point(47, 146), 2.0, 2.0);

            // 1b. Mechanical Keyboard on the RIGHT: Rect(148, 136, 144, 86)
            var kbBaseBrush = new SolidColorBrush(Color.FromRgb(50, 52, 64));
            var kbPlateBrush = new SolidColorBrush(Color.FromRgb(38, 40, 50));
            var kbPen = new Pen(inkBrush, 2.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawRoundedRectangle(kbBaseBrush, kbPen, new Rect(148, 136, 144, 86), 7, 7);
            dc.DrawRoundedRectangle(kbPlateBrush, null, new Rect(152, 140, 136, 78), 5, 5);

            // 5-Row 75% Mechanical Keyboard (Rotated 180°):
            // Row 0 (top): Spacebar row (closest to character's chest)
            // Row 1: Shift / Z-M row
            // Row 2: Home row (Enter on left, Caps on right)
            // Row 3: QWERTY / Q-P row
            // Row 4 (bottom): Number row & Esc (Esc at bottom-right in 180° layout)
            var keycapBrush = new SolidColorBrush(Color.FromRgb(250, 247, 242));
            var spacebarBrush = new SolidColorBrush(accentColor);
            var keyPen = new Pen(new SolidColorBrush(Color.FromRgb(210, 205, 200)), 1.0);

            // Row 0 (Top / Spacebar Row, Y = 143)
            double r0Y = 143;
            // Left keys in Row 0 (Ctrl, Win, Alt)
            dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(156, r0Y, 12, 12), 2, 2);
            dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(170, r0Y, 12, 12), 2, 2);
            dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(184, r0Y, 12, 12), 2, 2);
            // Center Spacebar (accent color)
            dc.DrawRoundedRectangle(spacebarBrush, null, new Rect(198, r0Y, 44, 12), 3, 3);
            // Right keys in Row 0 (Alt, Fn, Arrows)
            dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(244, r0Y, 12, 12), 2, 2);
            dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(258, r0Y, 12, 12), 2, 2);
            dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(272, r0Y, 12, 12), 2, 2);

            // Row 1 (Shift / Z-M Row, Y = 157)
            double r1Y = 157;
            for (int c = 0; c < 9; c++)
            {
                double kx = 156 + c * 14.5;
                dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(kx, r1Y, 13, 12), 2, 2);
            }

            // Row 2 (Home Row, Y = 171 - Enter on left side in 180°)
            double r2Y = 171;
            // Enter key on the left side
            dc.DrawRoundedRectangle(spacebarBrush, null, new Rect(156, r2Y, 18, 12), 2.5, 2.5);
            for (int c = 0; c < 7; c++)
            {
                double kx = 176 + c * 13.5;
                dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(kx, r2Y, 12, 12), 2, 2);
            }
            // Caps key on the right side
            dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(272, r2Y, 14, 12), 2, 2);

            // Row 3 (QWERTY / Q-P Row, Y = 185)
            double r3Y = 185;
            for (int c = 0; c < 9; c++)
            {
                double kx = 156 + c * 14.5;
                dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(kx, r3Y, 13, 12), 2, 2);
            }

            // Row 4 (Bottom / Number Row & Esc, Y = 199 - Esc on right, Backspace on left in 180°)
            double r4Y = 199;
            // Backspace key on the left side
            dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(156, r4Y, 18, 12), 2, 2);
            for (int c = 0; c < 7; c++)
            {
                double kx = 176 + c * 13.5;
                dc.DrawRoundedRectangle(keycapBrush, keyPen, new Rect(kx, r4Y, 12, 12), 2, 2);
            }
            // Esc key on the right side (accent color)
            dc.DrawRoundedRectangle(spacebarBrush, null, new Rect(272, r4Y, 14, 12), 2.5, 2.5);
        });

        // 2. Character Expressions: Rendered using the authentic official sprites!
        // 2a. Face Normal (Authentic official character from spawn.png)
        SavePng(Path.Combine(dir, "face_normal.png"), dc =>
        {
            dc.DrawImage(normalBmp, petRectNormal);
        });

        // 2b. Face Focused (Authentic official focused character)
        SavePng(Path.Combine(dir, "face_focused.png"), dc =>
        {
            dc.DrawImage(focusedBmp, petRectFocused);
        });

        // 2c. Face Overdrive (Authentic crying / determined character)
        SavePng(Path.Combine(dir, "face_overdrive.png"), dc =>
        {
            dc.DrawImage(overdriveBmp, petRectOverdrive);
        });

        // 3. Left Paw (Mouse Hand on the LEFT - Arm extends from body down to mini-mouse on mousepad)
        // 3a. Left Paw Up: Arm extending from body, resting on mini-mouse
        SavePng(Path.Combine(dir, "left_up.png"), dc =>
        {
            DrawLeftArmAndMouse(dc, furBrush, inkBrush, inkPen, accentColor, MouseClickType.None);
        });

        // 3b. Left Paw Down (Left Click): Arm clicking mini-mouse left button (mirrored to screen-right in 180° layout)
        SavePng(Path.Combine(dir, "left_down.png"), dc =>
        {
            DrawLeftArmAndMouse(dc, furBrush, inkBrush, inkPen, accentColor, MouseClickType.LeftButton);
        });

        // 3c. Left Paw Down (Right Click): Arm clicking mini-mouse right button (mirrored to screen-left in 180° layout)
        SavePng(Path.Combine(dir, "left_down_right.png"), dc =>
        {
            DrawLeftArmAndMouse(dc, furBrush, inkBrush, inkPen, accentColor, MouseClickType.RightButton);
        });

        // 4. Right Paw (Keyboard Hand on the RIGHT - Arm extends from body down to keyboard)
        // 4a. Right Paw Up: Arm extending from body, paw hovering above keyboard keys
        SavePng(Path.Combine(dir, "right_up.png"), dc =>
        {
            DrawRightArmAndPaw(dc, furBrush, inkBrush, inkPen, isDown: false);
        });

        // 4b. Right Paw Down: Arm extending from body, paw tapping down on keyboard keys
        SavePng(Path.Combine(dir, "right_down.png"), dc =>
        {
            DrawRightArmAndPaw(dc, furBrush, inkBrush, inkPen, isDown: true);
        });

        // 5. Overdrive Effect (Anime speed lines, sweat drops, speed sparkles behind character)
        SavePng(Path.Combine(dir, "effect_overdrive.png"), dc =>
        {
            var speedPen = new Pen(new SolidColorBrush(Color.FromArgb(170, 255, 200, 80)), 2.6)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            var sweatBrush = new SolidColorBrush(Color.FromArgb(220, 120, 200, 255));
            var sweatStroke = new Pen(inkBrush, 1.5);

            // Sweat droplets popping off head
            dc.DrawEllipse(sweatBrush, sweatStroke, new Point(78, 54), 5, 8);
            dc.DrawEllipse(sweatBrush, sweatStroke, new Point(242, 54), 5, 8);
            dc.DrawEllipse(sweatBrush, sweatStroke, new Point(96, 30), 4, 6);
            dc.DrawEllipse(sweatBrush, sweatStroke, new Point(224, 30), 4, 6);

            // Energy action lines
            dc.DrawLine(speedPen, new Point(36, 90), new Point(66, 94));
            dc.DrawLine(speedPen, new Point(40, 108), new Point(70, 108));
            dc.DrawLine(speedPen, new Point(284, 90), new Point(254, 94));
            dc.DrawLine(speedPen, new Point(280, 108), new Point(250, 108));

            // Star bursts
            var starBrush = new SolidColorBrush(Color.FromRgb(255, 220, 50));
            dc.DrawEllipse(starBrush, null, new Point(60, 72), 3.5, 3.5);
            dc.DrawEllipse(starBrush, null, new Point(260, 72), 3.5, 3.5);
        });

        // 6. manifest.json
        string json = $$"""
        {
          "key": "{{key}}",
          "displayName": "{{displayName}}",
          "author": "DAOjun0690",
          "description": "{{desc}}",
          "bodyFile": "body.png",
          "leftUpFile": "left_up.png",
          "leftDownFile": "left_down.png",
          "leftDownRightFile": "left_down_right.png",
          "rightUpFile": "right_up.png",
          "rightDownFile": "right_down.png",
          "faceNormalFile": "face_normal.png",
          "faceFocusedFile": "face_focused.png",
          "faceOverdriveFile": "face_overdrive.png",
          "overdriveEffectFile": "effect_overdrive.png"
        }
        """;
        File.WriteAllText(Path.Combine(dir, "manifest.json"), json);

        // 7. Render composite preview images for easy visual verification
        SavePng(Path.Combine(dir, "preview_normal.png"), dc =>
        {
            var face = BitmapFrame.Create(new Uri(Path.Combine(dir, "face_normal.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var body = BitmapFrame.Create(new Uri(Path.Combine(dir, "body.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var left = BitmapFrame.Create(new Uri(Path.Combine(dir, "left_up.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var right = BitmapFrame.Create(new Uri(Path.Combine(dir, "right_up.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            dc.DrawImage(face, new Rect(0, 0, Width, Height));
            dc.DrawImage(body, new Rect(0, 0, Width, Height));
            dc.DrawImage(left, new Rect(0, 0, Width, Height));
            dc.DrawImage(right, new Rect(0, 0, Width, Height));
        });

        SavePng(Path.Combine(dir, "preview_overdrive.png"), dc =>
        {
            var effect = BitmapFrame.Create(new Uri(Path.Combine(dir, "effect_overdrive.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var face = BitmapFrame.Create(new Uri(Path.Combine(dir, "face_overdrive.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var body = BitmapFrame.Create(new Uri(Path.Combine(dir, "body.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var left = BitmapFrame.Create(new Uri(Path.Combine(dir, "left_down.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var right = BitmapFrame.Create(new Uri(Path.Combine(dir, "right_down.png")), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            dc.DrawImage(effect, new Rect(0, 0, Width, Height));
            dc.DrawImage(face, new Rect(0, 0, Width, Height));
            dc.DrawImage(body, new Rect(0, 0, Width, Height));
            dc.DrawImage(left, new Rect(0, 0, Width, Height));
            dc.DrawImage(right, new Rect(0, 0, Width, Height));
        });
    }

    private enum MouseClickType
    {
        None,
        LeftButton,
        RightButton
    }

    private static void DrawLeftArmAndMouse(
        DrawingContext dc,
        Brush furBrush,
        Brush inkBrush,
        Pen inkPen,
        Color accentColor,
        MouseClickType clickType)
    {
        double mx = 66;
        double my = 156;
        double mw = 34;
        double mh = 48;

        var mouseBrush = new SolidColorBrush(Color.FromRgb(252, 252, 255));
        var mousePen = new Pen(inkBrush, 2.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        // 1. Mouse Body (Rotated 180°: buttons and scroll wheel at the BOTTOM, palm rest at the TOP)
        dc.DrawRoundedRectangle(mouseBrush, mousePen, new Rect(mx, my, mw, mh), 12, 12);

        // Middle button separation line (bottom)
        dc.DrawLine(new Pen(inkBrush, 1.8), new Point(mx + mw / 2.0, my + mh - 18), new Point(mx + mw / 2.0, my + mh));

        // Cute mini scroll wheel (bottom)
        var wheelBrush = new SolidColorBrush(accentColor);
        dc.DrawRoundedRectangle(wheelBrush, null, new Rect(mx + mw / 2.0 - 2, my + mh - 14, 4, 7), 1.5, 1.5);

        // 180° Inverted / Mirrored Mouse Buttons:
        // - Left Click (User clicks left button) -> in 180° layout, the mouse's LEFT button is on the RIGHT side of the mouse body (mx + mw/2 to mx + mw)
        // - Right Click (User clicks right button) -> in 180° layout, the mouse's RIGHT button is on the LEFT side of the mouse body (mx to mx + mw/2)
        if (clickType == MouseClickType.LeftButton)
        {
            var clickHighlight = new SolidColorBrush(Color.FromArgb(90, 255, 200, 100));
            dc.DrawRoundedRectangle(clickHighlight, null, new Rect(mx + mw / 2.0 + 1, my + mh - 16, mw / 2.0 - 3, 14), 4, 4);
        }
        else if (clickType == MouseClickType.RightButton)
        {
            var clickHighlight = new SolidColorBrush(Color.FromArgb(90, 255, 200, 100));
            dc.DrawRoundedRectangle(clickHighlight, null, new Rect(mx + 2, my + mh - 16, mw / 2.0 - 3, 14), 4, 4);
        }

        // 2. Left Arm connecting smoothly from character's body down to mouse
        bool isClicked = clickType != MouseClickType.None;
        double pawY = isClicked ? 172 : 170;

        // Arm Fill (closed shape, no stroke across shoulder)
        var fillGeo = new StreamGeometry();
        using (var ctx = fillGeo.Open())
        {
            ctx.BeginFigure(new Point(92, 126), true, true);
            ctx.BezierTo(new Point(82, 138), new Point(70, 154), new Point(69, pawY), true, false);
            ctx.BezierTo(new Point(69, pawY + 10), new Point(97, pawY + 10), new Point(97, pawY), true, false);
            ctx.BezierTo(new Point(98, 154), new Point(108, 138), new Point(116, 126), true, false);
        }
        fillGeo.Freeze();
        dc.DrawGeometry(furBrush, null, fillGeo);

        // Arm Stroke (open path - meets desk border at Y=126)
        var strokeGeo = new StreamGeometry();
        using (var ctx = strokeGeo.Open())
        {
            ctx.BeginFigure(new Point(92, 126), false, false);
            ctx.BezierTo(new Point(82, 138), new Point(70, 154), new Point(69, pawY), true, false);
            ctx.BezierTo(new Point(69, pawY + 10), new Point(97, pawY + 10), new Point(97, pawY), true, false);
            ctx.BezierTo(new Point(98, 154), new Point(108, 138), new Point(116, 126), true, false);
        }
        strokeGeo.Freeze();
        dc.DrawGeometry(null, inkPen, strokeGeo);

        // Paw thumb contour line
        var thumbPen = new Pen(inkBrush, 2.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(thumbPen, new Point(81, pawY + 1), new Point(85, pawY + 5));

        if (clickType == MouseClickType.LeftButton)
        {
            // Cute "click!" spark lines at the mouse's LEFT button (bottom-right in 180° layout)
            var sparkPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 180, 50)), 2.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(sparkPen, new Point(mx + mw - 3, my + mh + 3), new Point(mx + mw - 7, my + mh));
            dc.DrawLine(sparkPen, new Point(mx + mw + 3, my + mh - 3), new Point(mx + mw - 2, my + mh - 1));
            dc.DrawLine(sparkPen, new Point(mx + mw - 10, my + mh + 5), new Point(mx + mw - 10, my + mh));
        }
        else if (clickType == MouseClickType.RightButton)
        {
            // Cute "click!" spark lines at the mouse's RIGHT button (bottom-left in 180° layout)
            var sparkPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 180, 50)), 2.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(sparkPen, new Point(mx + 3, my + mh + 3), new Point(mx + 7, my + mh));
            dc.DrawLine(sparkPen, new Point(mx - 3, my + mh - 3), new Point(mx + 2, my + mh - 1));
            dc.DrawLine(sparkPen, new Point(mx + 10, my + mh + 5), new Point(mx + 10, my + mh));
        }
    }

    private static void DrawRightArmAndPaw(
        DrawingContext dc,
        Brush furBrush,
        Brush inkBrush,
        Pen inkPen,
        bool isDown)
    {
        double pawY = isDown ? 172 : 158;

        // Arm Fill (closed shape, no stroke across shoulder)
        var fillGeo = new StreamGeometry();
        using (var ctx = fillGeo.Open())
        {
            ctx.BeginFigure(new Point(204, 126), true, true);
            ctx.BezierTo(new Point(204, 138), new Point(203, pawY - 4), new Point(203, pawY), true, false);
            ctx.BezierTo(new Point(203, pawY + 10), new Point(229, pawY + 10), new Point(229, pawY), true, false);
            ctx.BezierTo(new Point(230, pawY - 4), new Point(230, 138), new Point(228, 126), true, false);
        }
        fillGeo.Freeze();
        dc.DrawGeometry(furBrush, null, fillGeo);

        // Arm Stroke (open path - meets desk border at Y=126)
        var strokeGeo = new StreamGeometry();
        using (var ctx = strokeGeo.Open())
        {
            ctx.BeginFigure(new Point(204, 126), false, false);
            ctx.BezierTo(new Point(204, 138), new Point(203, pawY - 4), new Point(203, pawY), true, false);
            ctx.BezierTo(new Point(203, pawY + 10), new Point(229, pawY + 10), new Point(229, pawY), true, false);
            ctx.BezierTo(new Point(230, pawY - 4), new Point(230, 138), new Point(228, 126), true, false);
        }
        strokeGeo.Freeze();
        dc.DrawGeometry(null, inkPen, strokeGeo);

        // Paw thumb contour line
        var thumbPen = new Pen(inkBrush, 2.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(thumbPen, new Point(213, pawY + 1), new Point(209, pawY + 5));

        if (isDown)
        {
            // Cute tap impact lines around paw
            var tapPen = new Pen(inkBrush, 2.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(tapPen, new Point(194, pawY + 6), new Point(199, pawY + 10));
            dc.DrawLine(tapPen, new Point(238, pawY + 6), new Point(233, pawY + 10));
            dc.DrawLine(tapPen, new Point(216, pawY + 18), new Point(216, pawY + 24));
        }
    }

    private static BitmapSource LoadCroppedSprite(string path)
    {
        var srcFrame = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var opaqueRect = GetOpaqueBounds(srcFrame);
        return new CroppedBitmap(srcFrame, opaqueRect);
    }

    private static void SavePng(string path, Action<DrawingContext> drawAction)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            drawAction(dc);
        }

        var rtb = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static Int32Rect GetOpaqueBounds(BitmapSource bmp)
    {
        var formatBmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
        int width = formatBmp.PixelWidth;
        int height = formatBmp.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[height * stride];
        formatBmp.CopyPixels(pixels, stride, 0);

        int minX = width, minY = height, maxX = 0, maxY = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte alpha = pixels[y * stride + x * 4 + 3];
                if (alpha > 15)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (minX > maxX || minY > maxY) return new Int32Rect(0, 0, width, height);
        return new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}

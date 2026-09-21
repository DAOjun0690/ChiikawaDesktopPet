// src/ChiikawaDesktopPet.Core/BongoKeyboardLayout.cs
using System;
using System.Collections.Generic;

namespace ChiikawaDesktopPet.Core;

/// <summary>
/// 75% 機械鍵盤鍵位映射系統（依角色視角 180° 倒置佈局）。
/// 輸出相對於右手默認中心（X ~ 218, Y ~ 168）的微調偏移量 (DeltaX, DeltaY)。
/// </summary>
public static class BongoKeyboardLayout
{
    // (DeltaX, DeltaY) offset relative to default paw resting center
    // Because keyboard is rotated 180°:
    // - Spacebar is at the TOP (DeltaY < 0)
    // - Number row is at the BOTTOM (DeltaY > 0)
    // - Left keys (Esc, Tab, Q, A, Z) are on the RIGHT (DeltaX > 0)
    // - Right keys (Backspace, Enter, P, L, M) are on the LEFT (DeltaX < 0)
    private static readonly Dictionary<int, (double DeltaX, double DeltaY)> KeyOffsets = new()
    {
        // === Row 0: Spacebar row (TOP, DeltaY ~ -20) ===
        { 0x20, (0.0, -20.0) },   // VK_SPACE (Spacebar center)
        { 0x12, (28.0, -20.0) },  // VK_MENU (Generic Alt)
        { 0xA4, (28.0, -20.0) },  // VK_LMENU (Left Alt)
        { 0x5B, (40.0, -20.0) },  // VK_LWIN (Left Win)
        { 0x11, (50.0, -20.0) },  // VK_CONTROL (Generic Ctrl -> Top-Right)
        { 0xA2, (50.0, -20.0) },  // VK_LCONTROL (Left Ctrl -> Top-Right)
        { 0xA5, (-18.0, -20.0) }, // VK_RMENU (Right Alt)
        { 0x5C, (-30.0, -20.0) }, // VK_RWIN (Right Win)
        { 0xA3, (-44.0, -20.0) }, // VK_RCONTROL (Right Ctrl)

        // Arrow keys (on the left side in 180° layout)
        { 0x26, (-42.0, -10.0) }, // VK_UP
        { 0x28, (-42.0, -20.0) }, // VK_DOWN
        { 0x25, (-30.0, -20.0) }, // VK_LEFT
        { 0x27, (-54.0, -20.0) }, // VK_RIGHT

        // === Row 1: Shift / Bottom alpha row (DeltaY ~ -10) ===
        { 0x10, (50.0, -10.0) },  // VK_SHIFT
        { 0xA0, (50.0, -10.0) },  // VK_LSHIFT
        { 0x5A, (42.0, -10.0) },  // 'Z'
        { 0x58, (32.0, -10.0) },  // 'X'
        { 0x43, (22.0, -10.0) },  // 'C'
        { 0x56, (12.0, -10.0) },  // 'V'
        { 0x42, (2.0, -10.0) },   // 'B'
        { 0x4E, (-8.0, -10.0) },  // 'N'
        { 0x4D, (-18.0, -10.0) }, // 'M'
        { 0xBC, (-28.0, -10.0) }, // VK_OEM_COMMA ','
        { 0xBE, (-38.0, -10.0) }, // VK_OEM_PERIOD '.'
        { 0xBF, (-48.0, -10.0) }, // VK_OEM_2 '/'
        { 0xA1, (-56.0, -10.0) }, // VK_RSHIFT

        // === Row 2: Home row (CENTER, DeltaY ~ 0) ===
        { 0x14, (54.0, 0.0) },    // VK_CAPITAL (Caps Lock)
        { 0x41, (42.0, 0.0) },    // 'A'
        { 0x53, (32.0, 0.0) },    // 'S'
        { 0x44, (22.0, 0.0) },    // 'D'
        { 0x46, (12.0, 0.0) },    // 'F'
        { 0x47, (2.0, 0.0) },     // 'G'
        { 0x48, (-8.0, 0.0) },    // 'H'
        { 0x4A, (-18.0, 0.0) },   // 'J'
        { 0x4B, (-28.0, 0.0) },   // 'K'
        { 0x4C, (-38.0, 0.0) },   // 'L'
        { 0xBA, (-46.0, 0.0) },   // VK_OEM_1 ';'
        { 0xDE, (-52.0, 0.0) },   // VK_OEM_7 '''
        { 0x0D, (-56.0, 0.0) },   // VK_RETURN (Enter)

        // === Row 3: QWERTY row (DeltaY ~ +10) ===
        { 0x09, (52.0, 10.0) },   // VK_TAB
        { 0x51, (42.0, 10.0) },   // 'Q'
        { 0x57, (32.0, 10.0) },   // 'W'
        { 0x45, (22.0, 10.0) },   // 'E'
        { 0x52, (12.0, 10.0) },   // 'R'
        { 0x54, (2.0, 10.0) },    // 'T'
        { 0x59, (-8.0, 10.0) },   // 'Y'
        { 0x55, (-18.0, 10.0) },  // 'U'
        { 0x49, (-28.0, 10.0) },  // 'I'
        { 0x4F, (-38.0, 10.0) },  // 'O'
        { 0x50, (-46.0, 10.0) },  // 'P'
        { 0xDB, (-52.0, 10.0) },  // VK_OEM_4 '['
        { 0xDD, (-56.0, 10.0) },  // VK_OEM_6 ']'

        // === Row 4: Numbers & Esc row (BOTTOM, DeltaY ~ +20) ===
        { 0x1B, (54.0, 20.0) },   // VK_ESCAPE (Esc at bottom-right in 180° layout)
        { 0xC0, (46.0, 20.0) },   // VK_OEM_3 '`'
        { 0x31, (38.0, 20.0) },   // '1'
        { 0x32, (30.0, 20.0) },   // '2'
        { 0x33, (22.0, 20.0) },   // '3'
        { 0x34, (14.0, 20.0) },   // '4'
        { 0x35, (6.0, 20.0) },    // '5'
        { 0x36, (-2.0, 20.0) },   // '6'
        { 0x37, (-10.0, 20.0) },  // '7'
        { 0x38, (-18.0, 20.0) },  // '8'
        { 0x39, (-26.0, 20.0) },  // '9'
        { 0x30, (-34.0, 20.0) },  // '0'
        { 0xBD, (-42.0, 20.0) },  // VK_OEM_MINUS '-'
        { 0xBB, (-48.0, 20.0) },  // VK_OEM_PLUS '='
        { 0x08, (-54.0, 20.0) },  // VK_BACK (Backspace at bottom-left in 180°)

        // Function keys (F1-F12 mapped to bottom row)
        { 0x70, (48.0, 20.0) },   // F1
        { 0x71, (40.0, 20.0) },   // F2
        { 0x72, (32.0, 20.0) },   // F3
        { 0x73, (24.0, 20.0) },   // F4
        { 0x74, (16.0, 20.0) },   // F5
        { 0x75, (8.0, 20.0) },    // F6
        { 0x76, (0.0, 20.0) },    // F7
        { 0x77, (-8.0, 20.0) },   // F8
        { 0x78, (-16.0, 20.0) },  // F9
        { 0x79, (-24.0, 20.0) },  // F10
        { 0x7A, (-32.0, 20.0) },  // F11
        { 0x7B, (-40.0, 20.0) },  // F12
    };

    /// <summary>
    /// 取得指定 VK 鍵碼在 75% 鍵盤上的目標位移 (DeltaX, DeltaY)。
    /// 若未在映射表內，預設敲擊鍵盤正中心 (0, 0)。
    /// </summary>
    // ponytail: use standard library GetValueOrDefault for single-line lookup with fallback
    public static (double DeltaX, double DeltaY) GetTargetOffset(int vkCode) =>
        KeyOffsets.GetValueOrDefault(vkCode, (0.0, 0.0));

    /// <summary>
    /// 判定按鍵是否屬於鍵盤左半區（供雙手純鍵盤模式分配左手或右手敲擊）。
    /// </summary>
    public static bool IsLeftHandKey(int vkCode)
    {
        // Keys traditionally on the physical left half of a standard keyboard:
        // Esc, `, 1, 2, 3, 4, 5, Tab, Q, W, E, R, T, Caps, A, S, D, F, G, LShift, Z, X, C, V, B, LCtrl, LAlt
        return vkCode switch
        {
            0x1B or 0xC0 or (>= 0x31 and <= 0x35) or (>= 0x70 and <= 0x75) => true,
            0x09 or 0x51 or 0x57 or 0x45 or 0x52 or 0x54 => true, // Tab, Q, W, E, R, T
            0x14 or 0x41 or 0x53 or 0x44 or 0x46 or 0x47 => true, // Caps, A, S, D, F, G
            0x10 or 0xA0 or 0x5A or 0x58 or 0x43 or 0x56 or 0x42 => true, // Shift, LShift, Z, X, C, V, B
            0x11 or 0xA2 or 0x12 or 0xA4 or 0x5B => true,         // LCtrl, LAlt, LWin
            _ => false
        };
    }
}

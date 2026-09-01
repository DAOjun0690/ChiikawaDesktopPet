// src/ChiikawaDesktopPet.Wpf/App.xaml.cs
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using ChiikawaDesktopPet.Core;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace ChiikawaDesktopPet.Wpf;

public partial class App : Application
{
    private static readonly FrozenDictionary<string, string> AnimationDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["walkleft"] = "向左走",
        ["walkright"] = "向右走",
        ["jumpleft"] = "向左跳",
        ["jumpright"] = "向右跳",
        ["bounce"] = "原地彈跳",
        ["dance"] = "狂歡跳舞",
        ["eat"] = "吃拉麵",
        ["cheer"] = "拍手歡呼",
        ["drama"] = "崩潰搥地",
        ["sleep"] = "躺平睡覺",
        ["appeal"] = "展現魅力",
        ["stomp"] = "跺腳生氣",
        ["tapdance"] = "踢踏舞",
        ["danceswirl"] = "旋轉舞",
        ["mock"] = "嘲諷搖擺",
        ["bushi"] = "不是不是喔",
        ["heart"] = "發送愛心",
        ["kiss"] = "飛吻放閃",
        ["run"] = "快步狂奔",
        ["cry"] = "痛哭流涕",
        ["party"] = "派對狂歡",
        ["chainsaw"] = "鏈鋸狂飆",
        ["spin"] = "旋轉狂舞",
        ["bark"] = "汪汪叫",
        ["roar"] = "張大嘴怒吼",
        ["thunder"] = "小雞觸電",
        ["squeeze"] = "胖到溢出來",
        ["worship"] = "膜拜香蕉",
        ["keyboard"] = "狂敲鍵盤",
        ["chair"] = "辦公椅狂飆",
        ["smash"] = "鐵鎚砸手機",
        ["error"] = "筆電報錯",
        ["toilet"] = "馬桶滑手機",
        ["swing"] = "藤蔓擺盪",
        ["flat"] = "趴平融化",
        ["scream"] = "驚嚇尖叫",
        ["fine"] = "火海喝茶",
        ["melt"] = "融化成史萊姆",
        ["rich"] = "撒錢暴富",
        ["muscle"] = "秀二頭肌",
        ["laugh"] = "仰天狂笑",
        ["pompom"] = "彩球應援",
        ["sparkle"] = "水汪汪大眼",
        ["yay"] = "好耶舉手",
        ["wave"] = "揮手掰掰",
        ["hug"] = "雙貓互蹭",
        ["sit"] = "乖乖坐好",
        ["dash"] = "急速橫移",
        ["dashleft"] = "向左滑行",
        ["butt"] = "開心扭屁股",
        ["isolated"] = "角落畫圈自閉",
        ["shy"] = "害羞雙手摀臉",
        ["hulahoop"] = "瘋狂搖呼拉圈",
        ["towel"] = "雙手搓毛巾",
        ["legcircle"] = "躺平雙腿畫圈",
        ["sillydance"] = "魔性魔幻舞步",
        ["lookup"] = "抬頭看上面",
        ["music"] = "戴耳機聽音樂",
        ["iine"] = "雙手比讚",
        ["kusao"] = "魔性大笑(草)",
        ["bro"] = "BRO兄弟深情",
        ["smoke"] = "抽菸一服中",
        ["explosion"] = "身後大爆炸",
        ["money"] = "咬鈔票搖擺",
        ["beer"] = "來喝一杯",
        ["night"] = "晚安星空",
        ["saikou"] = "太棒了最高",
        ["shirankedo"] = "雖然我也不清楚啦"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private sealed class CharacterInstanceData(
        string instanceId,
        string characterKey,
        int index,
        string displayName,
        CharacterWindow window,
        MenuItem playSubmenu,
        MenuItem kickItem,
        MenuItem stopResumeItem,
        MenuItem jumpItem)
    {
        public string InstanceId { get; } = instanceId;
        public string CharacterKey { get; } = characterKey;
        public int Index { get; } = index;
        public string DisplayName { get; } = displayName;
        public CharacterWindow Window { get; } = window;
        public MenuItem PlaySubmenu { get; } = playSubmenu;
        public MenuItem KickItem { get; } = kickItem;
        public MenuItem StopResumeItem { get; } = stopResumeItem;
        public MenuItem JumpItem { get; } = jumpItem;
    }

    public static bool EnableWindowsNotifications { get; set; } = false;
    public static bool IsAllHidden { get; private set; } = false;

    private const int HOTKEY_ID = 0xB055;
    private HwndSource? _hotkeyHwndSource;
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayContextMenu;
    private MenuItem? _unsealMenuItem;
    private ToolStripSeparator? _unsealSeparator;
    private MenuItem? _aliveMenu;
    private MenuItem? _playAnimationMenu;
    private MenuItem? _stopResumeMenu;
    private MenuItem? _jumpMenu;
    private readonly Dictionary<string, int> _characterCounters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CharacterInstanceData> _instances = new(StringComparer.OrdinalIgnoreCase);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayContextMenu = new ContextMenuStrip();

        _unsealMenuItem = new MenuItem("與你訂下約定的我命令你，封印解除!");
        _unsealMenuItem.Font = new System.Drawing.Font(_unsealMenuItem.Font, System.Drawing.FontStyle.Bold);
        _unsealMenuItem.Click += (_, _) => UnhideAllCharacters();
        _unsealSeparator = new ToolStripSeparator();

        var spawnMenu = new MenuItem("生成角色");
        var spawnAllItem = new MenuItem("生成所有角色");
        spawnAllItem.Click += (_, _) => SpawnAllCharacters();
        spawnMenu.DropDownItems.Add(spawnAllItem);
        spawnMenu.DropDownItems.Add(new ToolStripSeparator());

        foreach (var (key, displayName, _) in CharacterRegistry.All)
        {
            var item = new MenuItem(displayName);
            string k = key;
            item.Click += (_, _) => SpawnCharacter(k);
            spawnMenu.DropDownItems.Add(item);
        }
        _trayContextMenu.Items.Add(spawnMenu);

        _aliveMenu = new MenuItem("現在存活的角色") { Enabled = false };
        _trayContextMenu.Items.Add(_aliveMenu);

        _playAnimationMenu = new MenuItem("播放動畫") { Enabled = false };
        _trayContextMenu.Items.Add(_playAnimationMenu);

        var sayHiItem = new MenuItem("打個招呼！");
        sayHiItem.Click += (_, _) => SayHi();
        _trayContextMenu.Items.Add(sayHiItem);

        _stopResumeMenu = new MenuItem("停止/恢復隨機動畫...") { Enabled = false };
        _trayContextMenu.Items.Add(_stopResumeMenu);

        _jumpMenu = new MenuItem("停止/恢復隨機跳躍...") { Enabled = false };
        _trayContextMenu.Items.Add(_jumpMenu);

        var confineToMonitorItem = new MenuItem("限制角色只能在單一螢幕內移動")
        {
            CheckOnClick = true,
            Checked = CharacterWindow.ConfineToCurrentMonitor
        };
        confineToMonitorItem.Click += (_, _) =>
            CharacterWindow.ConfineToCurrentMonitor = confineToMonitorItem.Checked;
        _trayContextMenu.Items.Add(confineToMonitorItem);

        var notificationItem = new MenuItem("啟用 Windows 系統通知")
        {
            CheckOnClick = true,
            Checked = EnableWindowsNotifications
        };
        notificationItem.Click += (_, _) =>
            EnableWindowsNotifications = notificationItem.Checked;
        _trayContextMenu.Items.Add(notificationItem);

        _trayContextMenu.Items.Add(new ToolStripSeparator());

        var exportItem = new MenuItem("匯出角色配置...");
        exportItem.Click += (_, _) => ExportProfile();
        _trayContextMenu.Items.Add(exportItem);

        var importItem = new MenuItem("匯入角色配置...");
        importItem.Click += (_, _) => ImportProfile();
        _trayContextMenu.Items.Add(importItem);

        _trayContextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new MenuItem("結束程式");
        exitItem.Click += (_, _) => Shutdown();
        _trayContextMenu.Items.Add(exitItem);

        string icoPath = Path.Combine(AppContext.BaseDirectory, "assets", "app.ico");
        string pngPath = Path.Combine(AppContext.BaseDirectory, "assets", "app_icon.png");
        System.Drawing.Icon trayIcon;

        if (File.Exists(icoPath))
        {
            trayIcon = new System.Drawing.Icon(icoPath);
        }
        else if (File.Exists(pngPath))
        {
            using var iconBitmap = new System.Drawing.Bitmap(pngPath);
            trayIcon = System.Drawing.Icon.FromHandle(iconBitmap.GetHicon());
        }
        else
        {
            string fallbackPath = Path.Combine(AppContext.BaseDirectory, "assets", "hachiware", "icons", "icon.png");
            if (File.Exists(fallbackPath))
            {
                using var fallbackBmp = new System.Drawing.Bitmap(fallbackPath);
                trayIcon = System.Drawing.Icon.FromHandle(fallbackBmp.GetHicon());
            }
            else
            {
                trayIcon = System.Drawing.SystemIcons.Application;
            }
        }

        _trayIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Visible = true,
            ContextMenuStrip = _trayContextMenu,
            Text = "ChiikawaDesktopPet"
        };
        _trayIcon.DoubleClick += (_, _) =>
        {
            if (IsAllHidden)
            {
                UnhideAllCharacters();
            }
        };

        try
        {
            var parameters = new HwndSourceParameters("ChiikawaPet_HotkeySink")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0
            };
            _hotkeyHwndSource = new HwndSource(parameters);
            _hotkeyHwndSource.AddHook(HotkeyWndProc);
            NativeMethods.RegisterHotKey(_hotkeyHwndSource.Handle, HOTKEY_ID, NativeMethods.MOD_WIN | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, (uint)Keys.H);
        }
        catch
        {
            // Gracefully ignore if hotkey registration fails
        }

        // Automatically spawn a random regular character on startup (excluding lai)
        string initialCharacter = CharacterRegistry.AutoSpawnCandidates[Random.Shared.Next(CharacterRegistry.AutoSpawnCandidates.Length)];
        SpawnCharacter(initialCharacter);
    }

    private IntPtr HotkeyWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HideAllCharacters();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void SpawnAllCharacters()
    {
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        int minX = 50;
        int maxX = Math.Max(minX, (int)screenWidth - 200);

        foreach (var name in CharacterRegistry.AutoSpawnCandidates)
        {
            double randomX = Random.Shared.Next(minX, maxX);
            SpawnCharacter(name, randomX);
        }
    }

    private CharacterWindow SpawnCharacter(string name, double? initialX = null, CharacterProfileItem? profile = null)
    {
        if (IsAllHidden)
        {
            UnhideAllCharacters();
        }

        string key = name.ToLowerInvariant();
        int nextIndex = _characterCounters.TryGetValue(key, out int count) ? count + 1 : 1;
        _characterCounters[key] = nextIndex;

        string instanceId = $"{key}_{nextIndex}";
        string displayName = GetCharacterInstanceDisplayName(key, nextIndex);

        var window = new CharacterWindow(key, nextIndex, displayName);
        window.Spawn(initialX);

        if (profile != null)
        {
            window.ApplyProfile(profile);
        }

        var playSubmenu = new MenuItem(displayName);
        foreach (var animName in window.AllAnimationNames())
        {
            var item = new MenuItem(GetAnimationDisplayName(animName));
            string nameCopy = animName;
            item.Click += (_, _) => window.PlayAnimationByName(nameCopy);
            playSubmenu.DropDownItems.Add(item);
        }

        if (key is "chiikawa" or "momonga")
        {
            playSubmenu.DropDownItems.Add(new ToolStripSeparator());
            var coopItem = new MenuItem("【雙人互動】飛撲蹭臉 (Chiikawa & Momonga)");
            coopItem.Click += (_, _) =>
            {
                bool success = InteractionCoordinator.Instance.TriggerManualInteraction(window);
                if (!success)
                {
                    if (EnableWindowsNotifications)
                    {
                        _trayIcon?.ShowBalloonTip(1500, "雙人互動提示", "所需角色不足（需要 Chiikawa 與 Momonga 同時在場）", ToolTipIcon.Info);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            "所需角色不足（需要 Chiikawa 與 Momonga 同時在場）",
                            "雙人互動提示",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                }
            };
            playSubmenu.DropDownItems.Add(coopItem);
        }

        _playAnimationMenu!.DropDownItems.Add(playSubmenu);
        _playAnimationMenu.Enabled = true;

        var stopResumeItem = new MenuItem(window.RandomAnimationsEnabled ? $"{displayName}（點擊以停用）" : $"{displayName}（點擊以啟用）");
        stopResumeItem.Click += (_, _) => window.ToggleRandomAnimations();
        window.RandomAnimationsEnabledChanged += enabled =>
        {
            stopResumeItem.Text = enabled ? $"{displayName}（點擊以停用）" : $"{displayName}（點擊以啟用）";
        };
        _stopResumeMenu!.DropDownItems.Add(stopResumeItem);
        _stopResumeMenu.Enabled = true;

        var jumpItem = new MenuItem(window.JumpEnabled ? $"{displayName}（點擊以停用跳躍）" : $"{displayName}（點擊以啟用跳躍）");
        jumpItem.Click += (_, _) => window.ToggleJump();
        window.JumpEnabledChanged += enabled =>
        {
            jumpItem.Text = enabled ? $"{displayName}（點擊以停用跳躍）" : $"{displayName}（點擊以啟用跳躍）";
        };
        _jumpMenu!.DropDownItems.Add(jumpItem);
        _jumpMenu.Enabled = true;

        var kickItem = new MenuItem(displayName);
        kickItem.Click += (_, _) => KickCharacter(instanceId);
        _aliveMenu!.DropDownItems.Add(kickItem);
        _aliveMenu.Enabled = true;

        var instanceData = new CharacterInstanceData(
            instanceId,
            key,
            nextIndex,
            displayName,
            window,
            playSubmenu,
            kickItem,
            stopResumeItem,
            jumpItem);

        _instances[instanceId] = instanceData;

        window.KickRequested += () => KickCharacter(instanceId);
        window.SayHiRequested += () => SayHi(instanceId);

        return window;
    }

    private void KickCharacter(string instanceId)
    {
        if (_instances.Remove(instanceId, out var data))
        {
            data.Window.Shutdown();
            _playAnimationMenu!.DropDownItems.Remove(data.PlaySubmenu);
            _aliveMenu!.DropDownItems.Remove(data.KickItem);
            _stopResumeMenu!.DropDownItems.Remove(data.StopResumeItem);
            _jumpMenu!.DropDownItems.Remove(data.JumpItem);
        }

        if (_instances.Count == 0)
        {
            _aliveMenu!.Enabled = false;
            _playAnimationMenu!.Enabled = false;
            _stopResumeMenu!.Enabled = false;
            _jumpMenu!.Enabled = false;
            if (IsAllHidden)
            {
                UnhideAllCharacters();
            }
        }
    }

    private void KickAllCharacters()
    {
        var allIds = new List<string>(_instances.Keys);
        foreach (var id in allIds)
        {
            KickCharacter(id);
        }
        _characterCounters.Clear();
    }

    public void HideAllCharacters()
    {
        if (IsAllHidden) return;
        IsAllHidden = true;

        foreach (var instance in _instances.Values)
        {
            instance.Window.HidePet();
        }

        if (_trayContextMenu != null && _unsealMenuItem != null && _unsealSeparator != null)
        {
            if (!_trayContextMenu.Items.Contains(_unsealMenuItem))
            {
                _trayContextMenu.Items.Insert(0, _unsealMenuItem);
                _trayContextMenu.Items.Insert(1, _unsealSeparator);
            }
        }
    }

    public void UnhideAllCharacters()
    {
        if (!IsAllHidden) return;
        IsAllHidden = false;

        if (_trayContextMenu != null && _unsealMenuItem != null && _unsealSeparator != null)
        {
            _trayContextMenu.Items.Remove(_unsealMenuItem);
            _trayContextMenu.Items.Remove(_unsealSeparator);
        }

        foreach (var instance in _instances.Values)
        {
            instance.Window.ShowPet();
        }
    }

    public static void HideAllCharactersStatic() => (Current as App)?.HideAllCharacters();
    public static void UnhideAllCharactersStatic() => (Current as App)?.UnhideAllCharacters();

    private void ExportProfile()
    {
        if (_instances.Count == 0)
        {
            if (EnableWindowsNotifications)
            {
                _trayIcon?.ShowBalloonTip(1500, "匯出提示", "目前沒有任何角色可以匯出！", ToolTipIcon.Info);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "目前沒有任何角色可以匯出！",
                    "匯出提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            return;
        }

        using var saveDialog = new System.Windows.Forms.SaveFileDialog
        {
            Title = "匯出角色配置",
            Filter = "JSON 檔案 (*.json)|*.json|所有檔案 (*.*)|*.*",
            FileName = "chiikawapet_profile.json",
            DefaultExt = "json",
            AddExtension = true
        };

        if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        try
        {
            var profile = new PetProfile
            {
                Version = 1,
                Characters = new List<CharacterProfileItem>()
            };

            foreach (var instance in _instances.Values)
            {
                profile.Characters.Add(instance.Window.ToProfileItem());
            }

            ProfileManager.SaveToFile(saveDialog.FileName, profile);

            string msg = $"成功匯出 {profile.Characters.Count} 個角色配置！";
            if (EnableWindowsNotifications)
            {
                _trayIcon?.ShowBalloonTip(1500, "匯出成功", msg, ToolTipIcon.Info);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    msg,
                    "匯出成功",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"匯出失敗：{ex.Message}",
                "錯誤",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void ImportProfile()
    {
        using var openDialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "匯入角色配置",
            Filter = "JSON 檔案 (*.json)|*.json|所有檔案 (*.*)|*.*",
            FileName = "chiikawapet_profile.json",
            DefaultExt = "json"
        };

        if (openDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        PetProfile? profile;
        try
        {
            profile = ProfileManager.LoadFromFile(openDialog.FileName);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"讀取檔案失敗：{ex.Message}",
                "錯誤",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return;
        }

        if (profile == null || profile.Characters == null || profile.Characters.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "設定檔格式不正確或未包含任何角色資料！",
                "匯入失敗",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (IsAllHidden)
        {
            UnhideAllCharacters();
        }

        // Clear existing characters (replace mode)
        KickAllCharacters();

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        int minX = 50;
        int maxX = Math.Max(minX, (int)screenWidth - 200);

        foreach (var charItem in profile.Characters)
        {
            if (string.IsNullOrWhiteSpace(charItem.CharacterName)) continue;
            double randomX = Random.Shared.Next(minX, maxX);
            SpawnCharacter(charItem.CharacterName, randomX, charItem);
        }

        string msg = $"成功匯入 {profile.Characters.Count} 個角色！";
        if (EnableWindowsNotifications)
        {
            _trayIcon?.ShowBalloonTip(1500, "匯入成功", msg, ToolTipIcon.Info);
        }
        else
        {
            System.Windows.MessageBox.Show(
                msg,
                "匯入成功",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    private void SayHi(string? specificInstanceId = null)
    {
        if (_instances.Count == 0)
        {
            if (EnableWindowsNotifications)
            {
                _trayIcon!.ShowBalloonTip(500, "等等！", "你還沒有生成任何角色！", ToolTipIcon.Info);
            }
            return;
        }

        CharacterInstanceData chosen;
        if (specificInstanceId != null && _instances.TryGetValue(specificInstanceId, out var targetInstance))
        {
            chosen = targetInstance;
        }
        else
        {
            var list = new List<CharacterInstanceData>(_instances.Values);
            chosen = list[Random.Shared.Next(list.Count)];
        }

        chosen.Window.ShowSpeechBubble();
        if (EnableWindowsNotifications && chosen.Window.HasCustomText)
        {
            string dialogueText = chosen.Window.CurrentDialogueText;
            _trayIcon!.ShowBalloonTip(500, $"{chosen.DisplayName} 說：", dialogueText, ToolTipIcon.Info);
        }
    }

    public static string GetCharacterDisplayName(string characterName) =>
        CharacterRegistry.GetDisplayName(characterName);

    public static string GetCharacterInstanceDisplayName(string characterName, int index) =>
        $"{GetCharacterDisplayName(characterName)} {index}";

    public static string GetAnimationDisplayName(string animName) =>
        AnimationDisplayNames.TryGetValue(animName, out var name) ? name : animName;

    protected override void OnExit(ExitEventArgs e)
    {
        if (_hotkeyHwndSource != null)
        {
            try
            {
                NativeMethods.UnregisterHotKey(_hotkeyHwndSource.Handle, HOTKEY_ID);
                _hotkeyHwndSource.RemoveHook(HotkeyWndProc);
                _hotkeyHwndSource.Dispose();
            }
            catch
            {
                // ignore
            }
            _hotkeyHwndSource = null;
        }

        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

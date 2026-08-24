// src/YahaPet.Wpf/App.xaml.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace YahaPet.Wpf;

public partial class App : Application
{
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _playAnimationMenu;
    private ToolStripMenuItem? _kickMenu;
    private ToolStripMenuItem? _stopResumeMenu;
    private ToolStripMenuItem? _muteAllItem;
    private readonly Dictionary<string, CharacterWindow> _characters = new(StringComparer.OrdinalIgnoreCase);
    private bool _muteAll;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ponytail: the Python original shows an invisible, contentless full-screen
        // background window (`yahawindow`) that nothing ever draws on and no user story
        // observably depends on. ShutdownMode=OnExplicitShutdown already keeps this app
        // alive with only a tray icon, so that vestigial window is not ported.

        var contextMenu = new ContextMenuStrip();

        var spawnMenu = new MenuItem("生成角色");
        var spawnHachiware = new MenuItem("Hachiware");
        spawnHachiware.Click += (_, _) => SpawnCharacter("hachiware");
        spawnMenu.DropDownItems.Add(spawnHachiware);
        var spawnChiikawa = new MenuItem("Chiikawa");
        spawnChiikawa.Click += (_, _) => SpawnCharacter("chiikawa");
        spawnMenu.DropDownItems.Add(spawnChiikawa);
        var spawnUsagi = new MenuItem("Usagi");
        spawnUsagi.Click += (_, _) => SpawnCharacter("usagi");
        spawnMenu.DropDownItems.Add(spawnUsagi);
        var spawnMomonga = new MenuItem("Momonga");
        spawnMomonga.Click += (_, _) => SpawnCharacter("momonga");
        spawnMenu.DropDownItems.Add(spawnMomonga);
        contextMenu.Items.Add(spawnMenu);

        _playAnimationMenu = new MenuItem("播放動畫") { Enabled = false };
        contextMenu.Items.Add(_playAnimationMenu);

        var sayHiItem = new MenuItem("打個招呼！");
        sayHiItem.Click += (_, _) => SayHi();
        contextMenu.Items.Add(sayHiItem);

        _kickMenu = new MenuItem("踢出角色") { Enabled = false };
        contextMenu.Items.Add(_kickMenu);

        _muteAllItem = new MenuItem("全部靜音") { Enabled = false };
        _muteAllItem.Click += (_, _) => ToggleMuteAll();
        contextMenu.Items.Add(_muteAllItem);

        _stopResumeMenu = new MenuItem("停止/恢復隨機動畫...") { Enabled = false };
        contextMenu.Items.Add(_stopResumeMenu);

        var confineToMonitorItem = new MenuItem("限制角色只能在單一螢幕內移動")
        {
            CheckOnClick = true,
            Checked = CharacterWindow.ConfineToCurrentMonitor
        };
        confineToMonitorItem.Click += (_, _) =>
            CharacterWindow.ConfineToCurrentMonitor = confineToMonitorItem.Checked;
        contextMenu.Items.Add(confineToMonitorItem);

        var exitItem = new MenuItem("結束程式");
        exitItem.Click += (_, _) => Shutdown();
        contextMenu.Items.Add(exitItem);

        // ponytail: use the character's own artwork instead of the exe's associated icon —
        // Icon.ExtractAssociatedIcon also breaks under single-file publishing, since
        // Assembly.Location returns "" for an embedded assembly.
        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "hachiware", "icons", "icon.png");
        using var iconBitmap = new System.Drawing.Bitmap(iconPath);
        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.FromHandle(iconBitmap.GetHicon()),
            Visible = true,
            ContextMenuStrip = contextMenu,
            Text = "Yaha-Pet"
        };
    }

    private void SpawnCharacter(string name)
    {
        string key = name.ToLowerInvariant();
        if (_characters.ContainsKey(key))
        {
            System.Windows.MessageBox.Show("角色已經生成過了！", "失敗");
            return;
        }

        var window = new CharacterWindow(key);
        _characters[key] = window;
        window.Spawn();

        var playSubmenu = new ToolStripMenuItem(key);
        foreach (var animName in window.AllAnimationNames())
        {
            var item = new ToolStripMenuItem(animName);
            item.Click += (_, _) => window.PlayAnimationByName(animName);
            playSubmenu.DropDownItems.Add(item);
        }
        _playAnimationMenu!.DropDownItems.Add(playSubmenu);
        _playAnimationMenu.Enabled = true;

        var stopResumeItem = new ToolStripMenuItem($"{key}（點擊以停用）");
        stopResumeItem.Click += (_, _) =>
        {
            window.ToggleRandomAnimations();
            stopResumeItem.Text = window.RandomAnimationsEnabled ? $"{key}（點擊以停用）" : $"{key}（點擊以啟用）";
        };
        _stopResumeMenu!.DropDownItems.Add(stopResumeItem);
        _stopResumeMenu.Enabled = true;

        var kickItem = new ToolStripMenuItem(key);
        kickItem.Click += (_, _) => KickCharacter(key, playSubmenu, kickItem, stopResumeItem);
        _kickMenu!.DropDownItems.Add(kickItem);
        _kickMenu.Enabled = true;

        _muteAllItem!.Enabled = true;
    }

    private void KickCharacter(string key, ToolStripMenuItem playSubmenu, ToolStripMenuItem kickItem, ToolStripMenuItem stopResumeItem)
    {
        if (_characters.TryGetValue(key, out var window))
        {
            window.Shutdown();
            _characters.Remove(key);
        }
        _playAnimationMenu!.DropDownItems.Remove(playSubmenu);
        _kickMenu!.DropDownItems.Remove(kickItem);
        _stopResumeMenu!.DropDownItems.Remove(stopResumeItem);

        if (_characters.Count == 0)
        {
            _playAnimationMenu.Enabled = false;
            _kickMenu.Enabled = false;
            _muteAllItem!.Enabled = false;
            _stopResumeMenu.Enabled = false;
        }
    }

    private void SayHi()
    {
        if (_characters.Count == 0)
        {
            _trayIcon!.ShowBalloonTip(500, "等等！", "你還沒有生成任何角色！", ToolTipIcon.Info);
            return;
        }
        var names = new List<string>(_characters.Keys);
        string chosen = names[new Random().Next(names.Count)];
        SoundPlayerFactory.PlayIfExists(System.IO.Path.Combine(AppContext.BaseDirectory, "assets", chosen, "sounds", "hi.wav"));
        _trayIcon!.ShowBalloonTip(500, $"{chosen} 說：", "嗨！", ToolTipIcon.Info);
    }

    private void ToggleMuteAll()
    {
        _muteAll = !_muteAll;
        SoundPlayerFactory.MuteAll = _muteAll;
        _muteAllItem!.Text = _muteAll ? "取消全部靜音" : "全部靜音";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

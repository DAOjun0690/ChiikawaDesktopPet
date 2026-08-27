// src/ChiikawaDesktopPet.Wpf/CharacterWindow.Menu.cs
using System;
using System.Windows.Controls;

namespace ChiikawaDesktopPet.Wpf;

public partial class CharacterWindow
{
    private void ShowContextMenu()
    {
        var contextMenu = new ContextMenu();

        string displayName = InstanceDisplayName;
        var titleItem = new MenuItem
        {
            Header = $"【{displayName}】",
            IsEnabled = false,
            FontWeight = System.Windows.FontWeights.Bold
        };
        contextMenu.Items.Add(titleItem);
        contextMenu.Items.Add(new Separator());

        var playMenu = new MenuItem { Header = "播放動畫" };
        foreach (var animName in AllAnimationNames())
        {
            var item = new MenuItem { Header = App.GetAnimationDisplayName(animName) };
            string nameCopy = animName;
            item.Click += (_, _) => PlayAnimationByName(nameCopy);
            playMenu.Items.Add(item);
        }

        if (CharacterName is "chiikawa" or "momonga")
        {
            playMenu.Items.Add(new Separator());
            var coopItem = new MenuItem { Header = "【雙人互動】飛撲蹭臉 (Chiikawa & Momonga)" };
            coopItem.Click += (_, _) =>
            {
                bool success = InteractionCoordinator.Instance.TriggerManualInteraction(this);
                if (!success)
                {
                    System.Windows.MessageBox.Show(
                        "所需角色不足（需要 Chiikawa 與 Momonga 同時在場）",
                        "雙人互動提示",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            };
            playMenu.Items.Add(coopItem);
        }

        contextMenu.Items.Add(playMenu);

        var defaultActionMenu = new MenuItem { Header = "預設動作" };
        var defaultIdleItem = new MenuItem
        {
            Header = "【預設待機】",
            IsCheckable = true,
            IsChecked = string.IsNullOrEmpty(_defaultAnimation)
        };
        defaultIdleItem.Click += (_, _) => SetDefaultAnimation(null);
        defaultActionMenu.Items.Add(defaultIdleItem);
        defaultActionMenu.Items.Add(new Separator());

        foreach (var animName in InPlaceAnimationNames())
        {
            var item = new MenuItem
            {
                Header = App.GetAnimationDisplayName(animName),
                IsCheckable = true,
                IsChecked = string.Equals(_defaultAnimation, animName, StringComparison.OrdinalIgnoreCase)
            };
            string nameCopy = animName;
            item.Click += (_, _) => SetDefaultAnimation(nameCopy);
            defaultActionMenu.Items.Add(item);
        }
        contextMenu.Items.Add(defaultActionMenu);

        var randomAnimItem = new MenuItem
        {
            Header = "隨機動作",
            IsCheckable = true,
            IsChecked = _randomAnimationsEnabled
        };
        randomAnimItem.Click += (_, _) => SetRandomAnimationsEnabled(randomAnimItem.IsChecked);
        contextMenu.Items.Add(randomAnimItem);

        var randomJumpItem = new MenuItem
        {
            Header = "允許隨機跳躍",
            IsCheckable = true,
            IsChecked = _jumpEnabled
        };
        randomJumpItem.Click += (_, _) => SetJumpEnabled(randomJumpItem.IsChecked);
        contextMenu.Items.Add(randomJumpItem);

        var alwaysShowBubbleItem = new MenuItem
        {
            Header = "永久顯示對話框",
            IsCheckable = true,
            IsChecked = _alwaysShowBubble
        };
        alwaysShowBubbleItem.Click += (_, _) => SetAlwaysShowBubble(alwaysShowBubbleItem.IsChecked);
        contextMenu.Items.Add(alwaysShowBubbleItem);

        var setQuoteItem = new MenuItem { Header = "設定對話文字..." };
        setQuoteItem.Click += (_, _) =>
        {
            var dialog = new TextInputDialog(CharacterName, _customText, DialogueAlignment, DialogueFontSize);
            if (dialog.ShowDialog() == true)
            {
                SetCustomText(dialog.ResultText, dialog.ResultAlignment, dialog.ResultFontSize);
            }
        };
        contextMenu.Items.Add(setQuoteItem);

        var scaleMenu = new MenuItem { Header = "調整角色比例" };
        var presetScales = new (string Label, double Ratio)[]
        {
            ("50%", 0.50),
            ("75%", 0.75),
            ("100%（預設）", 1.00),
            ("125%", 1.25),
            ("150%", 1.50),
            ("175%", 1.75),
            ("200%", 2.00)
        };

        foreach (var (label, ratio) in presetScales)
        {
            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = Math.Abs(ScaleRatio - ratio) < 0.01
            };
            double r = ratio;
            item.Click += (_, _) => SetScaleRatio(r);
            scaleMenu.Items.Add(item);
        }

        scaleMenu.Items.Add(new Separator());

        var customScaleItem = new MenuItem { Header = "自訂比例..." };
        customScaleItem.Click += (_, _) =>
        {
            var dialog = new ScaleInputDialog(CharacterName, ScaleRatio);
            if (dialog.ShowDialog() == true)
            {
                SetScaleRatio(dialog.ResultScaleRatio);
            }
        };
        scaleMenu.Items.Add(customScaleItem);
        contextMenu.Items.Add(scaleMenu);

        contextMenu.Items.Add(new Separator());

        var sayHiItem = new MenuItem { Header = "打個招呼！" };
        sayHiItem.Click += (_, _) => SayHiRequested?.Invoke();
        contextMenu.Items.Add(sayHiItem);

        var kickItem = new MenuItem { Header = "踢出角色" };
        kickItem.Click += (_, _) => KickRequested?.Invoke();
        contextMenu.Items.Add(kickItem);

        contextMenu.Closed += (_, _) =>
        {
            if (!_isShuttingDown && !_isAnimating && !_isDragging)
            {
                EnterIdleState();
            }
        };

        ContextMenu = contextMenu;
        contextMenu.IsOpen = true;
    }
}


// src/YahaPet.Wpf/CharacterWindow.Menu.cs
using System.Windows.Controls;

namespace YahaPet.Wpf;

public partial class CharacterWindow
{
    private void ShowContextMenu()
    {
        var contextMenu = new ContextMenu();

        string displayName = App.GetCharacterDisplayName(CharacterName);
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
        contextMenu.Items.Add(playMenu);

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
                if (_randomAnimationsEnabled)
                {
                    StartIdleTimer();
                }
            }
        };

        ContextMenu = contextMenu;
        contextMenu.IsOpen = true;
    }
}


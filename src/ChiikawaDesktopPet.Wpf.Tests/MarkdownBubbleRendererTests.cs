// src/ChiikawaDesktopPet.Wpf.Tests/MarkdownBubbleRendererTests.cs
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ChiikawaDesktopPet.Core;
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class MarkdownBubbleRendererTests
{
    private static readonly object StaLock = new();

    private static void RunInSta(Action action)
    {
        lock (StaLock)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
            {
                throw new Exception("STA thread failed", exception);
            }
        }
    }

    [Fact]
    public void ContainsImages_IdentifiesMarkdownImages()
    {
        Assert.False(MarkdownBubbleRenderer.ContainsImages(null));
        Assert.False(MarkdownBubbleRenderer.ContainsImages(""));
        Assert.False(MarkdownBubbleRenderer.ContainsImages("一般文字訊息"));
        Assert.False(MarkdownBubbleRenderer.ContainsImages("[不是圖片](https://example.com)"));

        Assert.True(MarkdownBubbleRenderer.ContainsImages("![吉伊](https://example.com/chiikawa.png)"));
        Assert.True(MarkdownBubbleRenderer.ContainsImages("看這個：![貼圖](D:\\sticker.gif) 好可愛！"));
    }

    [Fact]
    public void ShouldExtendDisplayDuration_Tests()
    {
        Assert.False(MarkdownBubbleRenderer.ShouldExtendDisplayDuration(null));
        Assert.False(MarkdownBubbleRenderer.ShouldExtendDisplayDuration("短短的台詞"));

        // Long text (> 30 characters)
        string longText = new string('A', 35);
        Assert.True(MarkdownBubbleRenderer.ShouldExtendDisplayDuration(longText));

        // Image present
        Assert.True(MarkdownBubbleRenderer.ShouldExtendDisplayDuration("![圖](https://x.com/a.png)"));
    }

    [Fact]
    public void Render_PlainText_ProducesRun()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            MarkdownBubbleRenderer.Render(textBlock, "哈囉世界！", 14.0, TextAlignment.Left);

            Assert.Equal(TextAlignment.Left, textBlock.TextAlignment);
            Assert.Equal(14.0, textBlock.FontSize);
            Assert.NotEmpty(textBlock.Inlines);

            var run = textBlock.Inlines.OfType<Run>().FirstOrDefault();
            Assert.NotNull(run);
            Assert.Equal("哈囉世界！", run.Text);
        });
    }

    [Fact]
    public void Render_BoldAndItalicAndStrikethrough_ProducesFormattedSpans()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            MarkdownBubbleRenderer.Render(textBlock, "**粗體** 與 *斜體* 以及 ~~刪除線~~");

            var spans = textBlock.Inlines.OfType<Span>().ToList();
            Assert.NotEmpty(spans);

            // Check bold
            var boldSpan = spans.FirstOrDefault(s => s.FontWeight == FontWeights.Bold);
            Assert.NotNull(boldSpan);

            // Check italic
            var italicSpan = spans.FirstOrDefault(s => s.FontStyle == FontStyles.Italic);
            Assert.NotNull(italicSpan);

            // Check strikethrough
            var strikeSpan = spans.FirstOrDefault(s => s.TextDecorations == TextDecorations.Strikethrough);
            Assert.NotNull(strikeSpan);
        });
    }

    [Fact]
    public void Render_Hyperlink_OnlyAllowsHttpAndHttps()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            MarkdownBubbleRenderer.Render(textBlock, "[安全連結](https://example.com) 與 [不安全連結](cmd://run)");

            var hyperlinks = textBlock.Inlines.OfType<Hyperlink>().ToList();
            Assert.Equal(2, hyperlinks.Count);

            var safeLink = hyperlinks.First();
            Assert.NotNull(safeLink.NavigateUri);
            Assert.Equal("https", safeLink.NavigateUri.Scheme);

            var unsafeLink = hyperlinks.Last();
            Assert.Null(unsafeLink.NavigateUri); // Disallowed schemes should have null NavigateUri
        });
    }

    [Fact]
    public void Render_Image_ProducesDialogueImageControl()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            MarkdownBubbleRenderer.Render(textBlock, "看圖：![小可愛](https://example.com/chiikawa.png)", 13.0, TextAlignment.Center, 280.0, 220.0);

            var uiContainers = textBlock.Inlines.OfType<InlineUIContainer>().ToList();
            Assert.Single(uiContainers);

            var imgControl = uiContainers[0].Child as DialogueImageControl;
            Assert.NotNull(imgControl);
            Assert.Equal("https://example.com/chiikawa.png", imgControl.SourceUrl);
            Assert.Equal("小可愛", imgControl.AltText);
            Assert.Equal(280.0, imgControl.MaxWidth);
            Assert.Equal(220.0, imgControl.MaxHeight);
        });
    }

    [Fact]
    public void Render_LocalImageWithSpaces_ProducesDialogueImageControl()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            string md = @"![1651125986105](C:\Users\JEFF WANG\Pictures\1651125986105.jpg)";
            MarkdownBubbleRenderer.Render(textBlock, md, 13.0, TextAlignment.Center, 280.0, 220.0);

            var uiContainers = textBlock.Inlines.OfType<InlineUIContainer>().ToList();
            Assert.Single(uiContainers);
            var imgControl = uiContainers[0].Child as DialogueImageControl;
            Assert.NotNull(imgControl);
            Assert.Equal(@"C:\Users\JEFF WANG\Pictures\1651125986105.jpg", imgControl.SourceUrl);
        });
    }

    [Fact]
    public void DialogueImageControl_LoadsRealLocalImageFile()
    {
        string realPath = @"C:\Users\JEFF WANG\Pictures\1651125986105.jpg";
        if (!File.Exists(realPath)) return;

        RunInSta(() =>
        {
            var ctrl = new DialogueImageControl(realPath, "1651125986105");
            bool loaded = false;
            ctrl.ImageLoaded += () => loaded = true;

            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            ctrl.ImageLoaded += () =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);

            Assert.True(loaded);
        });
    }

    [Fact]
    public void Render_HeadingAndList_ProducesStructure()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            string md = "### 大標題\n- 項目1\n- 項目2";
            MarkdownBubbleRenderer.Render(textBlock, md);

            Assert.NotEmpty(textBlock.Inlines);
            // Has bold elements for headings/lists
            bool hasBold = textBlock.Inlines.Any(i => i.FontWeight == FontWeights.Bold || (i is Span s && s.FontWeight == FontWeights.Bold));
            Assert.True(hasBold);
        });
    }

    [Fact]
    public void CharacterProfileItem_DialogueImageDimensions_DefaultsAndPersistence()
    {
        var item = new CharacterProfileItem
        {
            CharacterName = "usagi",
            DialogueText = "烏拉！",
            DialogueImageMaxWidth = 300.0,
            DialogueImageMaxHeight = 240.0
        };

        Assert.Equal(300.0, item.DialogueImageMaxWidth);
        Assert.Equal(240.0, item.DialogueImageMaxHeight);

        var profile = new PetProfile
        {
            Characters = [item]
        };

        string json = System.Text.Json.JsonSerializer.Serialize(profile);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<PetProfile>(json);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized.Characters);
        Assert.Equal(300.0, deserialized.Characters[0].DialogueImageMaxWidth);
        Assert.Equal(240.0, deserialized.Characters[0].DialogueImageMaxHeight);
    }

    [Fact]
    public void TextInputDialog_InitializesAndSetsProperties()
    {
        RunInSta(() =>
        {
            var dialog = new TextInputDialog("hachiware", "なんとかなれ！", TextAlignment.Right, 16.0, 280.0, 180.0);
            Assert.Equal("なんとかなれ！", dialog.InputTextBox.Text);
            Assert.Equal(16.0, dialog.FontSizeSlider.Value);
            Assert.Equal(280.0, dialog.ImageWidthSlider.Value);
            Assert.Equal(180.0, dialog.ImageHeightSlider.Value);
            Assert.True(dialog.AlignRightRadio.IsChecked);
            Assert.Equal(2000, dialog.InputTextBox.MaxLength);
            Assert.True(dialog.ShowInTaskbar);

            dialog.Show();
            dialog.WindowState = WindowState.Minimized;
            Assert.Equal(WindowState.Normal, dialog.WindowState);

            dialog.Close();
        });
    }

    [Fact]
    public void NativeMethods_DisableMinimizeButton_HandlesZeroAndValidHwnd()
    {
        // Zero handle should be safely ignored
        NativeMethods.DisableMinimizeButton(IntPtr.Zero);

        RunInSta(() =>
        {
            var dialog = new TextInputDialog("usagi", "烏拉！");
            dialog.Show();
            var hwnd = new System.Windows.Interop.WindowInteropHelper(dialog).Handle;
            Assert.NotEqual(IntPtr.Zero, hwnd);
            NativeMethods.DisableMinimizeButton(hwnd);
            dialog.Close();
        });
    }

    [Fact]
    public void Render_StandardTaskList_ProducesCheckboxesAndSuppressesBullets()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            string md = "- [ ] 待辦事項\n- [x] 已完成事項\n- [X] 大寫完成事項";
            MarkdownBubbleRenderer.Render(textBlock, md);

            // Verify no bullet "• " is present
            var runs = textBlock.Inlines.OfType<Run>().ToList();
            Assert.DoesNotContain(runs, r => r.Text.Contains("• "));

            // Verify checkboxes
            var containers = textBlock.Inlines.OfType<InlineUIContainer>().ToList();
            Assert.Equal(3, containers.Count);

            // First checkbox: unchecked
            var border1 = Assert.IsType<Border>(containers[0].Child);
            Assert.Null(border1.Child);

            // Second checkbox: checked
            var border2 = Assert.IsType<Border>(containers[1].Child);
            Assert.NotNull(border2.Child);
            Assert.IsType<System.Windows.Shapes.Path>(border2.Child);

            // Third checkbox: checked [X]
            var border3 = Assert.IsType<Border>(containers[2].Child);
            Assert.NotNull(border3.Child);
            Assert.IsType<System.Windows.Shapes.Path>(border3.Child);
        });
    }

    [Fact]
    public void Render_StandaloneLineCheckboxes_ProducesCheckboxes()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            string md = "[ ] 買牛奶\n[x] 散步";
            MarkdownBubbleRenderer.Render(textBlock, md);

            var containers = textBlock.Inlines.OfType<InlineUIContainer>().ToList();
            Assert.Equal(2, containers.Count);

            var border1 = Assert.IsType<Border>(containers[0].Child);
            Assert.Null(border1.Child);

            var border2 = Assert.IsType<Border>(containers[1].Child);
            Assert.NotNull(border2.Child);
            Assert.IsType<System.Windows.Shapes.Path>(border2.Child);
        });
    }

    [Fact]
    public void Render_InlineCheckboxInSentence_ProducesCheckboxes()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            string md = "任務狀態：[x] 成功 [ ] 失敗";
            MarkdownBubbleRenderer.Render(textBlock, md);

            var containers = textBlock.Inlines.OfType<InlineUIContainer>().ToList();
            Assert.Equal(2, containers.Count);

            var border1 = Assert.IsType<Border>(containers[0].Child);
            Assert.NotNull(border1.Child);

            var border2 = Assert.IsType<Border>(containers[1].Child);
            Assert.Null(border2.Child);
        });
    }

    [Fact]
    public void TextInputDialog_TaskListButton_AppendsPrefixToSelection()
    {
        RunInSta(() =>
        {
            var dialog = new TextInputDialog("chiikawa", "項目一\n項目二");
            dialog.InputTextBox.SelectAll();

            // Simulate clicking OnTaskListClicked
            var taskListButton = dialog.TaskListButton;
            Assert.NotNull(taskListButton);

            // Fire click event
            taskListButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

            Assert.Contains("- [ ] 項目一", dialog.InputTextBox.Text);
            Assert.Contains("- [ ] 項目二", dialog.InputTextBox.Text);

            dialog.Close();
        });
    }

    [Fact]
    public void Diagnose_BubbleLayoutAndClipping()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("hachiware");
            window.SetCustomText("記每日工時\n9/8\n1. 修改批次匯入\nmodel\n2. copilot 測驗\n3. code review檢核表\n\n![pic](https://example.com/test.png)");
            window.Show();
            window.UpdateLayout();

            // Simulate the image being loaded:
            // Find DialogueImageControl inside BubbleText
            var inlineContainer = window.BubbleText.Inlines.OfType<System.Windows.Documents.InlineUIContainer>().FirstOrDefault();
            if (inlineContainer?.Child is DialogueImageControl imgControl)
            {
                // Trigger image load with a 260x200 dummy bitmap
                var bmp = System.Windows.Media.Imaging.BitmapSource.Create(260, 180, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null, new byte[260 * 180 * 4], 260 * 4);
                // Access private method or invoke OnBubbleImageLoaded
                var img = (System.Windows.Controls.Image)((System.Windows.Controls.Grid)imgControl.Child).Children[1];
                img.Source = bmp;
                img.Visibility = Visibility.Visible;
                ((System.Windows.Controls.Grid)imgControl.Child).Children[0].Visibility = Visibility.Collapsed;
            }

            // Without manually invoking UpdateWindowSizeAndLayout, SizeChanged should handle it!
            window.UpdateLayout();

            var bubbleContainer = window.BubbleContainer;
            var bubbleBorder = window.BubbleBorder;
            var rootGrid = window.RootGrid;

            var ptRoot = rootGrid.TranslatePoint(new System.Windows.Point(0, 0), window);
            var ptContainer = bubbleContainer.TranslatePoint(new System.Windows.Point(0, 0), window);
            var ptBorder = bubbleBorder.TranslatePoint(new System.Windows.Point(0, 0), window);

            string diag = $"WinH={window.Height}, WinActualH={window.ActualHeight}, RootH={rootGrid.ActualHeight}, RootY={ptRoot.Y}, BubbleContH={bubbleContainer.ActualHeight}, BubbleContY={ptContainer.Y}, BubbleBorderH={bubbleBorder.ActualHeight}, BubbleBorderY={ptBorder.Y}, SpriteH={window.SpriteImage.ActualHeight}";

            window.Close();

            // ptBorder.Y must be >= 10 to ensure the top border line, rounded corners, and shadow are never clipped!
            Assert.True(ptBorder.Y >= 10, $"Border top clipped or too close to window top edge! Y = {ptBorder.Y}. Diag: {diag}");
        });
    }

    [Fact]
    public void DialogueImageControl_AlreadyLoaded_InvokesCallbackImmediately()
    {
        RunInSta(() =>
        {
            var imgControl = new DialogueImageControl("https://example.com/test.png", "pic");
            // Simulate that the image loaded before subscriber attached
            var method = typeof(DialogueImageControl).GetMethod("DisplayImageControl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(imgControl, null);

            Assert.True(imgControl.IsImageLoaded);

            bool callbackFired = false;
            imgControl.ImageLoaded += () => { callbackFired = true; };

            Assert.True(callbackFired, "ImageLoaded subscriber attached after load must be fired immediately to avoid lost layout update.");
        });
    }

    [Fact]
    public void ToggleCheckboxAt_CorrectlyTogglesIndices()
    {
        string input = "- [ ] 項目一\n- [x] 項目二\n[link](https://example.com)\n![pic](https://example.com/pic.png)\n- [X] 項目三\n進度: [ ] 未完";

        // Toggle index 0: "- [ ]" -> "- [x]"
        string step1 = MarkdownBubbleRenderer.ToggleCheckboxAt(input, 0);
        Assert.StartsWith("- [x] 項目一", step1);

        // Toggle index 1: "- [x]" -> "- [ ]"
        string step2 = MarkdownBubbleRenderer.ToggleCheckboxAt(input, 1);
        Assert.Contains("- [ ] 項目二", step2);

        // Toggle index 2 (which is "- [X] 項目三", since link and image are ignored): "- [X]" -> "- [ ]"
        string step3 = MarkdownBubbleRenderer.ToggleCheckboxAt(input, 2);
        Assert.Contains("- [ ] 項目三", step3);

        // Toggle index 3 (inline "[ ]"): "[ ]" -> "[x]"
        string step4 = MarkdownBubbleRenderer.ToggleCheckboxAt(input, 3);
        Assert.Contains("進度: [x] 未完", step4);

        // Out of bounds: unchanged
        string outOfBounds = MarkdownBubbleRenderer.ToggleCheckboxAt(input, 99);
        Assert.Equal(input, outOfBounds);
    }

    [Fact]
    public void Render_CheckedTaskItem_AppliesStrikethroughAndMutedColor()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            string md = "- [ ] 未完成項目\n- [x] 已完成項目";

            MarkdownBubbleRenderer.Render(textBlock, md);

            // Find all Spans in textBlock
            var spans = textBlock.Inlines.OfType<Span>().ToList();
            
            // There should be a span for the checked item that has Strikethrough
            var strikethroughSpan = spans.FirstOrDefault(s => s.TextDecorations != null && s.TextDecorations.Count > 0);
            Assert.NotNull(strikethroughSpan);
            Assert.Equal(TextDecorations.Strikethrough, strikethroughSpan.TextDecorations);

            // Verify foreground is muted gray (140, 140, 140)
            var brush = strikethroughSpan.Foreground as SolidColorBrush;
            Assert.NotNull(brush);
            Assert.Equal(140, brush.Color.R);
            Assert.Equal(140, brush.Color.G);
            Assert.Equal(140, brush.Color.B);
        });
    }

    [Fact]
    public void Render_InteractiveCheckbox_ClickInvokesCallbackAndHandlesEvent()
    {
        RunInSta(() =>
        {
            var textBlock = new TextBlock();
            string md = "- [ ] 任務一\n- [ ] 任務二";

            int clickedIndex = -1;
            MarkdownBubbleRenderer.Render(textBlock, md, onCheckboxToggled: idx => clickedIndex = idx);

            // Find InlineUIContainers
            var uiContainers = new List<InlineUIContainer>();
            foreach (var inline in textBlock.Inlines)
            {
                if (inline is InlineUIContainer uic) uiContainers.Add(uic);
                else if (inline is Span s) uiContainers.AddRange(s.Inlines.OfType<InlineUIContainer>());
            }

            Assert.True(uiContainers.Count >= 2);

            // Trigger click on second checkbox (index 1)
            var border1 = uiContainers[1].Child as Border;
            Assert.NotNull(border1);
            Assert.True(border1.IsHitTestVisible);
            Assert.Equal(System.Windows.Input.Cursors.Hand, border1.Cursor);

            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent
            };
            border1.RaiseEvent(args);

            Assert.Equal(1, clickedIndex);
            Assert.True(args.Handled, "Event must be handled to prevent window drag/random action");
        });
    }

    [Fact]
    public void TextInputDialog_PreviewCheckboxClick_UpdatesInputTextBox()
    {
        RunInSta(() =>
        {
            var dialog = new TextInputDialog("hachiware", "- [ ] 待辦事項一\n- [ ] 待辦事項二");
            dialog.Show();

            // Find checkboxes in preview
            var uiContainers = new List<InlineUIContainer>();
            foreach (var inline in dialog.PreviewBubbleText.Inlines)
            {
                if (inline is InlineUIContainer uic) uiContainers.Add(uic);
                else if (inline is Span s) uiContainers.AddRange(s.Inlines.OfType<InlineUIContainer>());
            }

            Assert.True(uiContainers.Count >= 2);

            // Click the first checkbox in the preview pane
            var border0 = (Border)uiContainers[0].Child;
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent
            };
            border0.RaiseEvent(args);

            // InputTextBox should now have "- [x] 待辦事項一"
            Assert.StartsWith("- [x] 待辦事項一", dialog.InputTextBox.Text);

            dialog.Close();
        });
    }

    [Fact]
    public void CharacterWindow_BubbleCheckboxClick_UpdatesCustomText()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("hachiware");
            window.SetCustomText("- [ ] 買牛奶\n- [ ] 寫程式");
            window.Show();

            // Find checkboxes in BubbleText
            var uiContainers = new List<InlineUIContainer>();
            foreach (var inline in window.BubbleText.Inlines)
            {
                if (inline is InlineUIContainer uic) uiContainers.Add(uic);
                else if (inline is Span s) uiContainers.AddRange(s.Inlines.OfType<InlineUIContainer>());
            }

            Assert.True(uiContainers.Count >= 2);

            // Click the second checkbox (寫程式)
            var border1 = (Border)uiContainers[1].Child;
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent
            };
            border1.RaiseEvent(args);

            // CurrentDialogueText should be updated
            Assert.Contains("- [x] 寫程式", window.CurrentDialogueText);

            window.Close();
        });
    }
}

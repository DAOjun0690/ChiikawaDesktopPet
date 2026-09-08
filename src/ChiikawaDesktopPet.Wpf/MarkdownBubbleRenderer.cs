// src/ChiikawaDesktopPet.Wpf/MarkdownBubbleRenderer.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ChiikawaDesktopPet.Wpf;

public static class MarkdownBubbleRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UseTaskLists()
        .Build();

    private static readonly Regex ImagePatternRegex = new(@"!\[.*?\]\(.*?\)", RegexOptions.Compiled);

    private static readonly Regex UnbracketedLinkWithSpacesRegex = new(
        @"(?<prefix>!?\[(?<alt>[^\]]*)\]\()(?<url>[^<>\r\n)]+)(?<suffix>\))",
        RegexOptions.Compiled);

    private static readonly Regex StandaloneCheckboxRegex = new(
        @"(?m)^([ \t]*)\[([ xX])\](?:\s+|$)",
        RegexOptions.Compiled);

    private static readonly Regex GeneralCheckboxRegex = new(
        @"(?<![-*+]\s*)(?<!\d+\.\s*)(?<!\!)(?<!\[)\[([ xX])\](?!\()",
        RegexOptions.Compiled);

    private static readonly Regex CheckboxPatternRegex = new(
        @"(?<!\!)(?<!\[)\[([ xX])\](?!\()",
        RegexOptions.Compiled);

    public static string ToggleCheckboxAt(string? markdownText, int targetIndex)
    {
        if (string.IsNullOrEmpty(markdownText) || targetIndex < 0)
        {
            return markdownText ?? string.Empty;
        }

        var matches = CheckboxPatternRegex.Matches(markdownText);
        if (targetIndex >= matches.Count)
        {
            return markdownText;
        }

        var match = matches[targetIndex];
        int charIndex = match.Groups[1].Index;
        char currentChar = markdownText[charIndex];
        char newChar = (currentChar == 'x' || currentChar == 'X') ? ' ' : 'x';

        var chars = markdownText.ToCharArray();
        chars[charIndex] = newChar;
        return new string(chars);
    }

    public static string NormalizeMarkdown(string? markdownText)
    {
        if (string.IsNullOrWhiteSpace(markdownText)) return markdownText ?? string.Empty;

        string normalized = UnbracketedLinkWithSpacesRegex.Replace(markdownText, match =>
        {
            string inner = match.Groups["url"].Value.Trim();
            if (!inner.Contains(' ')) return match.Value;

            int quoteIndex = inner.IndexOfAny(new[] { '"', '\'' });
            if (quoteIndex > 0)
            {
                string pathPart = inner[..quoteIndex].Trim();
                string titlePart = inner[quoteIndex..].Trim();
                return $"{match.Groups["prefix"].Value}<{pathPart}> {titlePart}{match.Groups["suffix"].Value}";
            }
            else if (quoteIndex < 0)
            {
                return $"{match.Groups["prefix"].Value}<{inner}>{match.Groups["suffix"].Value}";
            }

            return match.Value;
        });

        // Normalize standalone line-starting [ ] or [x] to standard list - [ ] / - [x]
        normalized = StandaloneCheckboxRegex.Replace(normalized, "$1- [$2] ");

        // Normalize any remaining inline checkboxes to special private-use tokens
        return GeneralCheckboxRegex.Replace(normalized, m =>
            m.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase) ? "\uE001" : "\uE000");
    }

    public static bool ContainsImages(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return ImagePatternRegex.IsMatch(text);
    }

    public static bool ShouldExtendDisplayDuration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (ContainsImages(text)) return true;
        return text.Trim().Length > 30;
    }

    public static void Render(
        TextBlock target,
        string? markdownText,
        double baseFontSize = 13.0,
        TextAlignment alignment = TextAlignment.Center,
        double maxImageWidth = 260.0,
        double maxImageHeight = 200.0,
        Action? onImageLoaded = null,
        Action<int>? onCheckboxToggled = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Inlines.Clear();
        target.TextAlignment = alignment;
        target.FontSize = baseFontSize;

        if (string.IsNullOrWhiteSpace(markdownText))
        {
            return;
        }

        string normalized = NormalizeMarkdown(markdownText);
        MarkdownDocument doc;
        try
        {
            doc = Markdown.Parse(normalized, Pipeline);
        }
        catch
        {
            // Fallback to plain text on unexpected parser error
            target.Inlines.Add(new Run(markdownText));
            return;
        }

        int checkboxCounter = 0;
        bool isFirstBlock = true;
        foreach (var block in doc)
        {
            if (!isFirstBlock)
            {
                target.Inlines.Add(new LineBreak());
            }

            RenderBlock(block, target.Inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
            isFirstBlock = false;
        }
    }

    private static void RenderBlock(
        Markdig.Syntax.Block block,
        InlineCollection inlines,
        double baseFontSize,
        double maxImageWidth,
        double maxImageHeight,
        Action? onImageLoaded,
        ref int checkboxCounter,
        Action<int>? onCheckboxToggled)
    {
        switch (block)
        {
            case HeadingBlock heading:
                double headingFactor = heading.Level switch
                {
                    1 => 1.35,
                    2 => 1.25,
                    3 => 1.15,
                    _ => 1.05
                };
                var headingSpan = new Span { FontWeight = FontWeights.Bold, FontSize = baseFontSize * headingFactor };
                if (heading.Inline != null)
                {
                    RenderInlines(heading.Inline, headingSpan.Inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
                }
                inlines.Add(headingSpan);
                break;

            case ParagraphBlock paragraph:
                if (paragraph.Inline != null)
                {
                    RenderInlines(paragraph.Inline, inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
                }
                break;

            case QuoteBlock quote:
                var quoteSpan = new Span
                {
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90))
                };
                quoteSpan.Inlines.Add(new Run("▎ "));
                bool firstChild = true;
                foreach (var child in quote)
                {
                    if (!firstChild) quoteSpan.Inlines.Add(new LineBreak());
                    RenderBlock(child, quoteSpan.Inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
                    firstChild = false;
                }
                inlines.Add(quoteSpan);
                break;

            case ListBlock list:
                int itemIndex = 1;
                bool isOrdered = list.IsOrdered;
                bool firstItem = true;
                foreach (var item in list)
                {
                    if (!firstItem) inlines.Add(new LineBreak());
                    firstItem = false;

                    bool isTaskChecked = false;
                    bool isTaskList = item is ListItemBlock li && TryGetTaskList(li, out isTaskChecked);
                    if (!isTaskList)
                    {
                        string prefix = isOrdered ? $"{itemIndex++}. " : "• ";
                        inlines.Add(new Run(prefix) { FontWeight = FontWeights.Bold });
                    }
                    else if (isOrdered)
                    {
                        string prefix = $"{itemIndex++}. ";
                        inlines.Add(new Run(prefix) { FontWeight = FontWeights.Bold });
                    }

                    if (item is ListItemBlock listItem)
                    {
                        RenderListItem(listItem, inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, isTaskList, isTaskChecked, ref checkboxCounter, onCheckboxToggled);
                    }
                }
                break;

            case FencedCodeBlock codeBlock:
                RenderCodeBlock(codeBlock.Lines.ToString(), inlines);
                break;

            case CodeBlock plainCodeBlock:
                RenderCodeBlock(plainCodeBlock.Lines.ToString(), inlines);
                break;

            default:
                if (block is LeafBlock leaf && leaf.Inline != null)
                {
                    RenderInlines(leaf.Inline, inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
                }
                break;
        }
    }

    private static void RenderListItem(
        ListItemBlock listItem,
        InlineCollection inlines,
        double baseFontSize,
        double maxImageWidth,
        double maxImageHeight,
        Action? onImageLoaded,
        bool isTaskList,
        bool isTaskChecked,
        ref int checkboxCounter,
        Action<int>? onCheckboxToggled)
    {
        bool firstSub = true;
        foreach (var subBlock in listItem)
        {
            if (!firstSub) inlines.Add(new LineBreak());
            firstSub = false;

            if (isTaskList && subBlock is ParagraphBlock para && para.Inline != null)
            {
                var firstInline = para.Inline.FirstChild;
                if (firstInline is TaskList taskList)
                {
                    int currentCheckboxIndex = checkboxCounter++;
                    inlines.Add(CreateCheckboxInline(taskList.Checked, baseFontSize, currentCheckboxIndex, onCheckboxToggled));

                    Span taskSpan = new Span();
                    if (isTaskChecked)
                    {
                        taskSpan.TextDecorations = TextDecorations.Strikethrough;
                        taskSpan.Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140));
                    }

                    for (var sibling = firstInline.NextSibling; sibling != null; sibling = sibling.NextSibling)
                    {
                        RenderInline(sibling, taskSpan.Inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
                    }

                    inlines.Add(taskSpan);
                    continue;
                }
            }

            RenderBlock(subBlock, inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
        }
    }

    private static bool TryGetTaskList(ListItemBlock listItem, out bool isChecked)
    {
        isChecked = false;
        foreach (var sub in listItem)
        {
            if (sub is ParagraphBlock para && para.Inline != null)
            {
                foreach (var inline in para.Inline)
                {
                    if (inline is TaskList taskList)
                    {
                        isChecked = taskList.Checked;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static void RenderCodeBlock(string codeText, InlineCollection inlines)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 2, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var tb = new TextBlock
        {
            Text = codeText.TrimEnd(),
            FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New, monospace"),
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap
        };
        border.Child = tb;
        inlines.Add(new InlineUIContainer(border));
    }

    private static void RenderInlines(
        ContainerInline container,
        InlineCollection targetInlines,
        double baseFontSize,
        double maxImageWidth,
        double maxImageHeight,
        Action? onImageLoaded,
        ref int checkboxCounter,
        Action<int>? onCheckboxToggled)
    {
        foreach (var inline in container)
        {
            RenderInline(inline, targetInlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
        }
    }

    private static void RenderInline(
        Markdig.Syntax.Inlines.Inline inline,
        InlineCollection targetInlines,
        double baseFontSize,
        double maxImageWidth,
        double maxImageHeight,
        Action? onImageLoaded,
        ref int checkboxCounter,
        Action<int>? onCheckboxToggled)
    {
        switch (inline)
        {
            case TaskList taskList:
                int currentTaskIdx = checkboxCounter++;
                targetInlines.Add(CreateCheckboxInline(taskList.Checked, baseFontSize, currentTaskIdx, onCheckboxToggled));
                break;

            case LiteralInline literal:
                string text = literal.Content.ToString();
                if (text.Contains('\uE000') || text.Contains('\uE001'))
                {
                    int lastPos = 0;
                    for (int i = 0; i < text.Length; i++)
                    {
                        char c = text[i];
                        if (c == '\uE000' || c == '\uE001')
                        {
                            if (i > lastPos)
                            {
                                targetInlines.Add(new Run(text.Substring(lastPos, i - lastPos)));
                            }
                            int inlineCheckIdx = checkboxCounter++;
                            targetInlines.Add(CreateCheckboxInline(c == '\uE001', baseFontSize, inlineCheckIdx, onCheckboxToggled));
                            lastPos = i + 1;
                        }
                    }
                    if (lastPos < text.Length)
                    {
                        targetInlines.Add(new Run(text.Substring(lastPos)));
                    }
                }
                else
                {
                    targetInlines.Add(new Run(text));
                }
                break;

            case LineBreakInline:
                targetInlines.Add(new LineBreak());
                break;

            case EmphasisInline emphasis:
                Span span = new Span();
                if (emphasis.DelimiterChar == '~')
                {
                    span.TextDecorations = TextDecorations.Strikethrough;
                }
                else
                {
                    if (emphasis.DelimiterCount >= 2)
                    {
                        span.FontWeight = FontWeights.Bold;
                    }
                    if (emphasis.DelimiterCount % 2 != 0)
                    {
                        span.FontStyle = FontStyles.Italic;
                    }
                }
                RenderInlines(emphasis, span.Inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
                targetInlines.Add(span);
                break;

            case CodeInline code:
                var codeRun = new Run(code.Content)
                {
                    FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New, monospace"),
                    Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0))
                };
                targetInlines.Add(codeRun);
                break;

            case LinkInline link when link.IsImage:
                string alt = link.FirstChild is LiteralInline lit ? lit.Content.ToString() : (link.Title ?? "");
                string url = link.Url ?? string.Empty;
                var imgControl = new DialogueImageControl(url, alt, maxImageWidth, maxImageHeight, onImageLoaded);
                if (onImageLoaded != null)
                {
                    imgControl.ImageLoaded += onImageLoaded;
                }
                var container = new InlineUIContainer(imgControl)
                {
                    BaselineAlignment = BaselineAlignment.Center
                };
                targetInlines.Add(container);
                break;

            case LinkInline link:
                var hyperlink = new Hyperlink
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
                    TextDecorations = TextDecorations.Underline,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    hyperlink.NavigateUri = uri;
                    hyperlink.RequestNavigate += (sender, args) =>
                    {
                        try
                        {
                            if (args.Uri.Scheme == Uri.UriSchemeHttp || args.Uri.Scheme == Uri.UriSchemeHttps)
                            {
                                Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[MarkdownBubbleRenderer] Hyperlink open failed: {ex.Message}");
                        }
                        args.Handled = true;
                    };
                }

                RenderInlines(link, hyperlink.Inlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
                targetInlines.Add(hyperlink);
                break;

            case ContainerInline nested:
                RenderInlines(nested, targetInlines, baseFontSize, maxImageWidth, maxImageHeight, onImageLoaded, ref checkboxCounter, onCheckboxToggled);
                break;

            default:
                // Fallback: literal string representation
                targetInlines.Add(new Run(inline.ToString()));
                break;
        }
    }

    private static InlineUIContainer CreateCheckboxInline(
        bool isChecked,
        double baseFontSize,
        int checkboxIndex,
        Action<int>? onCheckboxToggled)
    {
        double size = Math.Round(Math.Clamp(baseFontSize * 1.05, 13.0, 22.0));
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(3.5),
            BorderThickness = new Thickness(1.5),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = onCheckboxToggled != null,
            Cursor = onCheckboxToggled != null ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
            Focusable = false,
            Background = isChecked ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) : Brushes.White,
            BorderBrush = isChecked ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) : new SolidColorBrush(Color.FromRgb(150, 150, 150))
        };

        if (isChecked)
        {
            var checkmark = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 2,6 L 5,9 L 10,2"),
                Stroke = Brushes.White,
                StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            border.Child = checkmark;
        }

        if (onCheckboxToggled != null)
        {
            var defaultBorderBrush = border.BorderBrush;
            var defaultBackground = border.Background;
            var hoverBorderBrush = isChecked
                ? new SolidColorBrush(Color.FromRgb(30, 95, 35))
                : new SolidColorBrush(Color.FromRgb(70, 70, 70));
            var hoverBackground = isChecked
                ? new SolidColorBrush(Color.FromRgb(38, 110, 42))
                : new SolidColorBrush(Color.FromRgb(240, 240, 240));

            border.MouseEnter += (s, e) =>
            {
                border.BorderBrush = hoverBorderBrush;
                border.Background = hoverBackground;
            };
            border.MouseLeave += (s, e) =>
            {
                border.BorderBrush = defaultBorderBrush;
                border.Background = defaultBackground;
            };
            border.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onCheckboxToggled(checkboxIndex);
            };
        }

        return new InlineUIContainer(border)
        {
            BaselineAlignment = BaselineAlignment.Center
        };
    }
}

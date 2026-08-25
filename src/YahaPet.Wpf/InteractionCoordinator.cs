// src/YahaPet.Wpf/InteractionCoordinator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using YahaPet.Core;

namespace YahaPet.Wpf;

public sealed class InteractionCoordinator
{
    public static InteractionCoordinator Instance { get; } = new();

    public const int CMD_QUERY = 1;
    public const int CMD_ENTER_INTERACTION = 2;
    public const int CMD_EXIT_INTERACTION = 3;
    public const int CMD_MOVE_TO = 4;

    public uint MessageId { get; }

    private readonly List<CharacterWindow> _localPets = [];
    private readonly DispatcherTimer _scanTimer = new();
    private DateTime _lastTriggerTime = DateTime.MinValue;
    private bool _isInteractionPlaying;

    public InteractionCoordinator()
    {
        MessageId = NativeMethods.RegisterWindowMessage("YahaPet_InterProcessCoordination");

        _scanTimer.Interval = TimeSpan.FromMilliseconds(400);
        _scanTimer.Tick += OnScanTimerTick;
        _scanTimer.Start();
    }

    public static int GetCharacterTypeId(string characterName) => characterName.ToLowerInvariant() switch
    {
        "chiikawa" => 1,
        "momonga" => 2,
        "hachiware" => 3,
        "usagi" => 4,
        "jokebear" => 5,
        "loverabbit" => 6,
        "lai" => 7,
        "poro" => 8,
        "pochita" => 9,
        _ => 0
    };

    public static string? GetCharacterNameFromTypeId(int typeId) => typeId switch
    {
        1 => "chiikawa",
        2 => "momonga",
        3 => "hachiware",
        4 => "usagi",
        5 => "jokebear",
        6 => "loverabbit",
        7 => "lai",
        8 => "poro",
        9 => "pochita",
        _ => null
    };

    public void RegisterPet(CharacterWindow pet)
    {
        if (!_localPets.Contains(pet))
        {
            _localPets.Add(pet);
        }
    }

    public void UnregisterPet(CharacterWindow pet)
    {
        _localPets.Remove(pet);
    }

    public sealed class PetCandidate
    {
        public IntPtr Hwnd { get; set; }
        public string CharacterName { get; set; } = "";
        public bool IsLocal { get; set; }
        public CharacterWindow? LocalWindow { get; set; }
        public PetPoint Position { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsReady { get; set; }
    }

    public List<PetCandidate> DiscoverAllActivePets()
    {
        var result = new List<PetCandidate>();
        var localHwnds = new HashSet<IntPtr>();

        // 1. Add all local pets
        foreach (var pet in _localPets)
        {
            if (!pet.IsLoaded) continue;
            var hwnd = new WindowInteropHelper(pet).Handle;
            if (hwnd != IntPtr.Zero)
            {
                localHwnds.Add(hwnd);
            }

            result.Add(new PetCandidate
            {
                Hwnd = hwnd,
                CharacterName = pet.CharacterName,
                IsLocal = true,
                LocalWindow = pet,
                Position = new PetPoint((int)pet.Left, (int)pet.Top),
                Width = (int)pet.Width,
                Height = (int)pet.Height,
                IsReady = pet.IsReadyForInteraction
            });
        }

        // 2. Discover remote pets in other processes via EnumWindows
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (localHwnds.Contains(hWnd)) return true;
            if (!NativeMethods.IsWindow(hWnd) || !NativeMethods.IsWindowVisible(hWnd)) return true;

            IntPtr typeProp = NativeMethods.GetProp(hWnd, "YahaPet_PetType");
            int typeId = (int)typeProp;
            if (typeId <= 0) return true;

            string? charName = GetCharacterNameFromTypeId(typeId);
            if (string.IsNullOrEmpty(charName)) return true;

            if (NativeMethods.TryGetWindowBounds(hWnd, out var rect))
            {
                double dipScale = SystemParameters.PrimaryScreenWidth / System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width;
                int leftDip = (int)(rect.Left * dipScale);
                int topDip = (int)(rect.Top * dipScale);
                int widthDip = (int)(rect.Width * dipScale);
                int heightDip = (int)(rect.Height * dipScale);

                IntPtr readyProp = NativeMethods.GetProp(hWnd, "YahaPet_IsReady");
                bool isReady = readyProp == (IntPtr)1;

                result.Add(new PetCandidate
                {
                    Hwnd = hWnd,
                    CharacterName = charName,
                    IsLocal = false,
                    LocalWindow = null,
                    Position = new PetPoint(leftDip, topDip),
                    Width = widthDip,
                    Height = heightDip,
                    IsReady = isReady
                });
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    public bool HasBothChiikawaAndMomonga(out PetCandidate? chiikawa, out PetCandidate? momonga)
    {
        var allPets = DiscoverAllActivePets();
        chiikawa = allPets.FirstOrDefault(p => p.CharacterName.Equals("chiikawa", StringComparison.OrdinalIgnoreCase));
        momonga = allPets.FirstOrDefault(p => p.CharacterName.Equals("momonga", StringComparison.OrdinalIgnoreCase));
        return chiikawa != null && momonga != null;
    }

    private void OnScanTimerTick(object? sender, EventArgs e)
    {
        if (_isInteractionPlaying) return;
        if (InteractionPlanner.IsCooldownActive(_lastTriggerTime, DateTime.UtcNow, InteractionPlanner.DefaultCooldown)) return;

        if (!HasBothChiikawaAndMomonga(out var chiikawa, out var momonga)) return;
        if (chiikawa == null || momonga == null) return;

        // Ensure at least one is local so this process acts as the initiator
        if (!chiikawa.IsLocal && !momonga.IsLocal) return;

        // If both are local, only the lower ID initiates; if one is local, local initiates
        if (chiikawa.IsLocal && momonga.IsLocal)
        {
            // Both local: this process is in full control
        }

        if (!chiikawa.IsReady || !momonga.IsReady) return;

        // Check distance
        bool inRange = InteractionPlanner.IsInRange(chiikawa.Position, momonga.Position, InteractionPlanner.DefaultDistanceThreshold) ||
                       InteractionPlanner.IsOverlapping(chiikawa.Position, chiikawa.Width, chiikawa.Height,
                                                        momonga.Position, momonga.Width, momonga.Height);

        if (inRange)
        {
            if (InteractionPlanner.ShouldTrigger(SystemRandomSource.Shared, InteractionPlanner.DefaultTriggerProbabilityPercent))
            {
                ExecuteInteraction(chiikawa, momonga);
            }
        }
    }

    public bool TriggerManualInteraction(CharacterWindow? sourceWindow = null)
    {
        if (_isInteractionPlaying) return true;

        if (!HasBothChiikawaAndMomonga(out var chiikawa, out var momonga) || chiikawa == null || momonga == null)
        {
            return false;
        }

        // Move them towards midpoint first if separated
        ExecuteInteractionWithConvergence(chiikawa, momonga);
        return true;
    }

    private void ExecuteInteractionWithConvergence(PetCandidate chiikawa, PetCandidate momonga)
    {
        _isInteractionPlaying = true;
        _lastTriggerTime = DateTime.UtcNow;

        int midX = (chiikawa.Position.X + momonga.Position.X) / 2;
        int midY = (chiikawa.Position.Y + momonga.Position.Y) / 2;

        int targetChiikawaX = midX - 60;
        int targetMomongaX = midX + 60;

        int pendingArrivals = 0;
        Action checkBothArrived = () =>
        {
            pendingArrivals--;
            if (pendingArrivals <= 0)
            {
                chiikawa.Position = new PetPoint(targetChiikawaX, midY);
                momonga.Position = new PetPoint(targetMomongaX, midY);
                ExecuteInteraction(chiikawa, momonga);
            }
        };

        if (chiikawa.IsLocal && chiikawa.LocalWindow != null)
        {
            pendingArrivals++;
            chiikawa.LocalWindow.SmoothMoveTo(new PetPoint(targetChiikawaX, midY), checkBothArrived);
        }
        else
        {
            SendRemoteMoveTo(chiikawa.Hwnd, targetChiikawaX, midY);
        }

        if (momonga.IsLocal && momonga.LocalWindow != null)
        {
            pendingArrivals++;
            momonga.LocalWindow.SmoothMoveTo(new PetPoint(targetMomongaX, midY), checkBothArrived);
        }
        else
        {
            SendRemoteMoveTo(momonga.Hwnd, targetMomongaX, midY);
        }

        if (pendingArrivals == 0)
        {
            ExecuteInteraction(chiikawa, momonga);
        }
    }

    private void ExecuteInteraction(PetCandidate chiikawa, PetCandidate momonga)
    {
        _isInteractionPlaying = true;
        _lastTriggerTime = DateTime.UtcNow;

        // Command both pets to enter interaction state (hide & pause)
        if (chiikawa.IsLocal && chiikawa.LocalWindow != null)
        {
            chiikawa.LocalWindow.EnterInteractionState();
        }
        else
        {
            SendRemoteEnterInteraction(chiikawa.Hwnd);
        }

        if (momonga.IsLocal && momonga.LocalWindow != null)
        {
            momonga.LocalWindow.EnterInteractionState();
        }
        else
        {
            SendRemoteEnterInteraction(momonga.Hwnd);
        }

        // Calculate screen bounds
        double dipScale = SystemParameters.PrimaryScreenWidth / System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width;
        var screenPoint = new System.Drawing.Point((int)(chiikawa.Position.X / dipScale), (int)(chiikawa.Position.Y / dipScale));
        var workingArea = System.Windows.Forms.Screen.FromPoint(screenPoint).WorkingArea;
        var bounds = new PetBounds(
            (int)(workingArea.Left * dipScale),
            (int)(workingArea.Top * dipScale),
            (int)(workingArea.Right * dipScale),
            (int)(workingArea.Bottom * dipScale));

        int windowW = 472; // 440 + 32 margin
        int windowH = 280; // 248 + 32 margin

        var windowPos = InteractionPlanner.CalculateInteractionWindowPosition(
            chiikawa.Position, chiikawa.Width, chiikawa.Height,
            momonga.Position, momonga.Width, momonga.Height,
            windowW, windowH, bounds);

        var interactionWindow = new InteractionWindow(fps: 15);
        interactionWindow.Completed += () =>
        {
            int petWA = chiikawa.Width > 0 ? chiikawa.Width : (int)(SystemParameters.PrimaryScreenWidth / 10);
            int petHA = chiikawa.Height > 0 ? chiikawa.Height : (int)(SystemParameters.PrimaryScreenHeight / 10);
            int petWB = momonga.Width > 0 ? momonga.Width : (int)(SystemParameters.PrimaryScreenWidth / 10);
            int petHB = momonga.Height > 0 ? momonga.Height : (int)(SystemParameters.PrimaryScreenHeight / 10);

            // Calculate reappear positions
            var (reappearChiikawa, reappearMomonga) = InteractionPlanner.CalculateReappearPositions(
                new PetPoint(windowPos.X + windowW / 2, windowPos.Y + windowH / 2),
                spacing: 140,
                bounds,
                petWA, petHA,
                petWB, petHB);

            if (chiikawa.IsLocal && chiikawa.LocalWindow != null)
            {
                chiikawa.LocalWindow.ExitInteractionState(reappearChiikawa);
            }
            else
            {
                SendRemoteExitInteraction(chiikawa.Hwnd, reappearChiikawa.X, reappearChiikawa.Y);
            }

            if (momonga.IsLocal && momonga.LocalWindow != null)
            {
                momonga.LocalWindow.ExitInteractionState(reappearMomonga);
            }
            else
            {
                SendRemoteExitInteraction(momonga.Hwnd, reappearMomonga.X, reappearMomonga.Y);
            }

            _isInteractionPlaying = false;
            _lastTriggerTime = DateTime.UtcNow;
        };

        interactionWindow.Play(windowPos.X, windowPos.Y);
    }

    private void SendRemoteEnterInteraction(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.PostMessage(hwnd, MessageId, (IntPtr)CMD_ENTER_INTERACTION, IntPtr.Zero);
    }

    private void SendRemoteExitInteraction(IntPtr hwnd, int targetX, int targetY)
    {
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.SetProp(hwnd, "YahaPet_TargetX", (IntPtr)targetX);
        NativeMethods.SetProp(hwnd, "YahaPet_TargetY", (IntPtr)targetY);
        NativeMethods.PostMessage(hwnd, MessageId, (IntPtr)CMD_EXIT_INTERACTION, IntPtr.Zero);
    }

    private void SendRemoteMoveTo(IntPtr hwnd, int targetX, int targetY)
    {
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.SetProp(hwnd, "YahaPet_TargetX", (IntPtr)targetX);
        NativeMethods.SetProp(hwnd, "YahaPet_TargetY", (IntPtr)targetY);
        NativeMethods.PostMessage(hwnd, MessageId, (IntPtr)CMD_MOVE_TO, IntPtr.Zero);
    }
}

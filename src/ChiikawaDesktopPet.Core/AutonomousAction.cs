namespace ChiikawaDesktopPet.Core;

public enum AutonomousActionKind { Jump, Walk, PlayAnimation, Talk, NoOp }

public sealed record AutonomousAction(AutonomousActionKind Kind, string? AnimationName = null);


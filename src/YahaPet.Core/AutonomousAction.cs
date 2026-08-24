namespace YahaPet.Core;

public enum AutonomousActionKind { Jump, Walk, PlayAnimation, NoOp }

public sealed record AutonomousAction(AutonomousActionKind Kind, string? AnimationName = null);

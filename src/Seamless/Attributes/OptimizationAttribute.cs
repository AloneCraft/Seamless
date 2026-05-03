using System;

namespace Seamless;

public enum OptimizationMode {
    Disabled = 0,
    None = Disabled,
    LoopDelegate = 1,
    LoopStrategy = 2,
}

[global::System.AttributeUsage(global::System.AttributeTargets.Assembly|global::System.AttributeTargets.Class|global::System.AttributeTargets.Method,AllowMultiple = false,Inherited = false)]
public sealed class OptimizationAttribute:global::System.Attribute {
    public OptimizationAttribute(OptimizationMode mode) {
        this.Mode=mode;
    }

    public OptimizationMode Mode { get; }
}

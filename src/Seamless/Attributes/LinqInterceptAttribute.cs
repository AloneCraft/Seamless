using System;
using System.Collections.Generic;
using System.Text;

namespace Seamless;

[global::System.AttributeUsage(global::System.AttributeTargets.Class|global::System.AttributeTargets.Method,AllowMultiple = false,Inherited = false)]
public sealed class LinqInterceptAttribute:global::System.Attribute {
    public enum LinqInterceptMode {
        ContainerMethod,
        Invocation,
    }
    public LinqInterceptAttribute(LinqInterceptMode mode) {
        Mode=mode;
    }

    public LinqInterceptMode Mode { get; }
}

namespace Shared.Linq.Enumerable;

[AttributeUsage(AttributeTargets.Method,AllowMultiple=false)]
public sealed class NoopLinqInterceptAttribute(bool optimize):Attribute {
    public bool Optimize { get; } = optimize;
}

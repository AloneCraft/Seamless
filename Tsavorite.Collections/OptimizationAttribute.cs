namespace Tsavorite.Common {

    [AttributeUsage(AttributeTargets.Assembly|AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=false,Inherited=false)]
    public sealed class OptimizationAttribute(OptimizationMode Mode):Attribute {
        public OptimizationMode Mode { get; } = Mode;
    }
}

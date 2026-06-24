#nullable enable
namespace Tsavorite.SourceGenerator {
    [global::System.Flags]
    internal enum LinqInstrumentation {
        None = 0,
        Counter = 1,
    }
    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly|global::System.AttributeTargets.Class|global::System.AttributeTargets.Method,AllowMultiple=false)]
    internal sealed class LinqInterceptAttribute : global::System.Attribute {
        public LinqInterceptAttribute(bool optimizeLinq) {
            this.OptimizeLinq=optimizeLinq;
            this.Intercept=optimizeLinq;
        }
        public LinqInterceptAttribute(bool optimizeLinq,LinqInstrumentation instrumentation) {
            this.OptimizeLinq=optimizeLinq;
            this.Intercept=optimizeLinq;
            this.Instrumentation=instrumentation;
        }
        public bool OptimizeLinq { get; }
        public bool Intercept { get; }
        public LinqInstrumentation Instrumentation { get; set; }
    }
}
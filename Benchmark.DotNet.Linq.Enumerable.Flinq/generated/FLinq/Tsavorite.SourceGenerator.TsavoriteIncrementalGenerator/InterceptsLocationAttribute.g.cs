#nullable enable
namespace System.Runtime.CompilerServices {
    [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]
    internal sealed class InterceptsLocationAttribute : global::System.Attribute {
        public InterceptsLocationAttribute(int version,string data) {
            this.Version=version;
            this.Data=data;
        }
        public int Version { get; }
        public string Data { get; }
    }
}
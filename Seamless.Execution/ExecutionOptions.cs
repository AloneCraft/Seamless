namespace Seamless.Execution;
public sealed record ExecutionOptions{
    public long MemoryLimitBytes{get;init;}=256L*1024*1024;
    public long TemporaryStorageLimitBytes{get;init;}=16L*1024*1024*1024;
    public string TemporaryDirectory{get;init;}=Path.Combine(Path.GetTempPath(),"Seamless","Execution");
    public CancellationToken CancellationToken{get;init;}
}
public enum ResourceLimitKind{Memory,TemporaryStorage,SingleElement}
public sealed class ResourceLimitException(ResourceLimitKind kind,long requestedBytes,long limitBytes):InvalidOperationException($"Resource limit exceeded: {kind}; requested={requestedBytes}; limit={limitBytes}."){
    public ResourceLimitKind Kind{get;}=kind;
    public long RequestedBytes{get;}=requestedBytes;
    public long LimitBytes{get;}=limitBytes;
}
public sealed class CodecUnavailableException(Type type):NotSupportedException($"No deterministic codec is registered for {type}. Register a codec, or provide an explicit equality comparer for keys."){
    public Type ValueType{get;}=type;
}

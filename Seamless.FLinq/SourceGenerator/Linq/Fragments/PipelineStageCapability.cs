namespace Seamless.FLinq;

[Flags]
internal enum PipelineStageCapability {
    None = 0,
    Streaming = 1,
    CountPreserving = 1<<1,
    Filtering = 1<<2,
    Projection = 1<<3,
    Stateful = 1<<4,
    Buffering = 1<<5,
    Binary = 1<<6,
    Order = 1<<7,
    Grouping = 1<<8,
}

internal static class PipelineStageCapabilityTable {
    public static PipelineStageCapability Get(PipelineStageKind kind) => OperatorRegistry.GetPipelineStageCapability(kind);

    public static bool SupportsProviderNativeIterate(PipelineStageKind kind) => OperatorRegistry.SupportsProviderNativeIterate(kind);
}
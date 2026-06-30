namespace Seamless.FLinq.Linq.Fragments;

[Flags]
public enum PipelineStageCapability {
    None = 0,
    Streaming = 1,
    CountPreserving = 1 << 1,
    Filtering = 1 << 2,
    Projection = 1 << 3,
    Stateful = 1 << 4,
    Buffering = 1 << 5,
    Binary = 1 << 6,
    Order = 1 << 7,
    Grouping = 1 << 8,
}

public static class PipelineStageCapabilityTable {
    public static PipelineStageCapability Get(PipelineStageKind kind) => kind switch {
        PipelineStageKind.Where => PipelineStageCapability.Streaming|PipelineStageCapability.Filtering,
        PipelineStageKind.Select => PipelineStageCapability.Streaming|PipelineStageCapability.CountPreserving|PipelineStageCapability.Projection,
        PipelineStageKind.SelectMany => PipelineStageCapability.Streaming|PipelineStageCapability.Projection|PipelineStageCapability.Stateful,
        PipelineStageKind.Distinct or PipelineStageKind.DistinctBy => PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,
        PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect
            or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy =>
            PipelineStageCapability.Streaming|PipelineStageCapability.Stateful|PipelineStageCapability.Binary,
        PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend =>
            PipelineStageCapability.Streaming|PipelineStageCapability.Binary,
        PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending
            or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending =>
            PipelineStageCapability.Buffering|PipelineStageCapability.Order,
        PipelineStageKind.GroupBy => PipelineStageCapability.Buffering|PipelineStageCapability.Grouping,
        PipelineStageKind.Reverse => PipelineStageCapability.Buffering|PipelineStageCapability.Stateful,
        PipelineStageKind.DefaultIfEmpty => PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,
        PipelineStageKind.AsEnumerable or PipelineStageKind.Cast => PipelineStageCapability.Streaming|PipelineStageCapability.CountPreserving|PipelineStageCapability.Projection,
        PipelineStageKind.OfType => PipelineStageCapability.Streaming|PipelineStageCapability.Filtering|PipelineStageCapability.Projection,
        PipelineStageKind.Take or PipelineStageKind.Skip
            or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile
            or PipelineStageKind.TakeRange =>
            PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,
        PipelineStageKind.SkipLast or PipelineStageKind.TakeLast => PipelineStageCapability.Buffering|PipelineStageCapability.Stateful,
        _ => PipelineStageCapability.None,
    };

    public static bool SupportsProviderNativeIterate(PipelineStageKind kind) =>
        kind is PipelineStageKind.Where or PipelineStageKind.Select or PipelineStageKind.AsEnumerable;
}

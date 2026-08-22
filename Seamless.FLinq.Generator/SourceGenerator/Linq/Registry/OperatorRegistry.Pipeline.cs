namespace Seamless.FLinq.Generator;

internal readonly struct OperatorPipelineDefinition {
    internal OperatorPipelineDefinition(PipelineStageKind stage,PipelineStageCapability capabilities,bool supportsProviderNative) {
        this.Stage=stage;
        this.Capabilities=capabilities;
        this.SupportsProviderNative=supportsProviderNative;
    }

    internal PipelineStageKind Stage { get; }
    internal PipelineStageCapability Capabilities { get; }
    internal bool SupportsProviderNative { get; }
}

internal static class OperatorPipelineManifest {
    private static readonly ImmutableDictionary<string,OperatorPipelineDefinition> definitions =
        new Dictionary<string,OperatorPipelineDefinition>(StringComparer.Ordinal) {
            ["Where"] = new(PipelineStageKind.Where,PipelineStageCapability.Streaming|PipelineStageCapability.Filtering,true),
            ["Select"] = new(PipelineStageKind.Select,PipelineStageCapability.Streaming|PipelineStageCapability.CountPreserving|PipelineStageCapability.Projection,true),
            ["SelectMany"] = new(PipelineStageKind.SelectMany,PipelineStageCapability.Streaming|PipelineStageCapability.Projection|PipelineStageCapability.Stateful,false),
            ["Distinct"] = new(PipelineStageKind.Distinct,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,false),
            ["DistinctBy"] = new(PipelineStageKind.DistinctBy,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,false),
            ["Union"] = new(PipelineStageKind.Union,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful|PipelineStageCapability.Binary,false),
            ["Except"] = new(PipelineStageKind.Except,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful|PipelineStageCapability.Binary,false),
            ["Intersect"] = new(PipelineStageKind.Intersect,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful|PipelineStageCapability.Binary,false),
            ["UnionBy"] = new(PipelineStageKind.UnionBy,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful|PipelineStageCapability.Binary,false),
            ["ExceptBy"] = new(PipelineStageKind.ExceptBy,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful|PipelineStageCapability.Binary,false),
            ["IntersectBy"] = new(PipelineStageKind.IntersectBy,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful|PipelineStageCapability.Binary,false),
            ["Concat"] = new(PipelineStageKind.Concat,PipelineStageCapability.Streaming|PipelineStageCapability.Binary,false),
            ["Append"] = new(PipelineStageKind.Append,PipelineStageCapability.Streaming|PipelineStageCapability.Binary,false),
            ["Prepend"] = new(PipelineStageKind.Prepend,PipelineStageCapability.Streaming|PipelineStageCapability.Binary,false),
            ["OrderBy"] = new(PipelineStageKind.OrderBy,PipelineStageCapability.Buffering|PipelineStageCapability.Order,false),
            ["OrderByDescending"] = new(PipelineStageKind.OrderByDescending,PipelineStageCapability.Buffering|PipelineStageCapability.Order,false),
            ["ThenBy"] = new(PipelineStageKind.ThenBy,PipelineStageCapability.Buffering|PipelineStageCapability.Order,false),
            ["ThenByDescending"] = new(PipelineStageKind.ThenByDescending,PipelineStageCapability.Buffering|PipelineStageCapability.Order,false),
            ["GroupBy"] = new(PipelineStageKind.GroupBy,PipelineStageCapability.Buffering|PipelineStageCapability.Grouping,false),
            ["Reverse"] = new(PipelineStageKind.Reverse,PipelineStageCapability.Buffering|PipelineStageCapability.Stateful,false),
            ["DefaultIfEmpty"] = new(PipelineStageKind.DefaultIfEmpty,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,false),
            ["AsEnumerable"] = new(PipelineStageKind.AsEnumerable,PipelineStageCapability.Streaming|PipelineStageCapability.CountPreserving|PipelineStageCapability.Projection,true),
            ["Cast"] = new(PipelineStageKind.Cast,PipelineStageCapability.Streaming|PipelineStageCapability.CountPreserving|PipelineStageCapability.Projection,true),
            ["OfType"] = new(PipelineStageKind.OfType,PipelineStageCapability.Streaming|PipelineStageCapability.Filtering|PipelineStageCapability.Projection,true),
            ["Take"] = new(PipelineStageKind.Take,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,false),
            ["Skip"] = new(PipelineStageKind.Skip,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,false),
            ["TakeWhile"] = new(PipelineStageKind.TakeWhile,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,false),
            ["SkipWhile"] = new(PipelineStageKind.SkipWhile,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,false),
            ["TakeRange"] = new(PipelineStageKind.TakeRange,PipelineStageCapability.Streaming|PipelineStageCapability.Stateful,false),
            ["SkipLast"] = new(PipelineStageKind.SkipLast,PipelineStageCapability.Buffering|PipelineStageCapability.Stateful,false),
            ["TakeLast"] = new(PipelineStageKind.TakeLast,PipelineStageCapability.Buffering|PipelineStageCapability.Stateful,false),
        }.ToImmutableDictionary(StringComparer.Ordinal);

    internal static ImmutableArray<KeyValuePair<string,OperatorPipelineDefinition>> Entries => definitions.ToImmutableArray();

    internal static bool TryGet(string methodName,out OperatorPipelineDefinition definition) => definitions.TryGetValue(methodName,out definition);
}

internal static partial class OperatorRegistry {
    private static readonly Lazy<ImmutableDictionary<PipelineStageKind,PipelineStageCapability>> pipelineCapabilities =
        new(CreatePipelineCapabilities);
    private static readonly Lazy<ImmutableHashSet<PipelineStageKind>> providerNativePipelineStages =
        new(CreateProviderNativePipelineStages);

    private static ImmutableDictionary<PipelineStageKind,PipelineStageCapability> CreatePipelineCapabilities() =>
        descriptors.Where(static descriptor => descriptor.PipelineStage is not null)
            .GroupBy(static descriptor => descriptor.PipelineStage!.Value)
            .ToImmutableDictionary(static group => group.Key,static group => group.Select(static descriptor => descriptor.PipelineCapabilities).First());

    private static ImmutableHashSet<PipelineStageKind> CreateProviderNativePipelineStages() =>
        descriptors.Where(static descriptor => descriptor.SupportsProviderNativePipeline&&descriptor.PipelineStage is not null)
            .Select(static descriptor => descriptor.PipelineStage!.Value)
            .ToImmutableHashSet();

    internal static PipelineStageCapability GetPipelineStageCapability(PipelineStageKind kind) =>
        pipelineCapabilities.Value.TryGetValue(kind,out var capability)
            ? capability
            : PipelineStageCapability.None;

    internal static bool SupportsProviderNativeIterate(PipelineStageKind kind) =>
        providerNativePipelineStages.Value.Contains(kind);
}
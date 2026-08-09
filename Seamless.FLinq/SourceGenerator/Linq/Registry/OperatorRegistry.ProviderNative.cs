namespace Seamless.FLinq;

internal enum ProviderNativePredicateBinding {
    None,
    Filter,
    Select,
    Terminal,
}

internal readonly struct ProviderNativeTerminalProfile {
    public ProviderNativeTerminalProfile(
        OperatorKey key,
        ProviderIterateTerminalKind terminalKind,
        PipelineTerminalKind? pipelineTerminalKind,
        ProviderNativePredicateBinding predicateBinding,
        bool usesTerminalFunctions = false,
        bool usesTrailingComparer = false,
        bool usesNativeLookup = false,
        int containsValueIndex = -1,
        int containsComparerIndex = -1) {
        this.Key=key;
        this.TerminalKind=terminalKind;
        this.PipelineTerminalKind=pipelineTerminalKind;
        this.PredicateBinding=predicateBinding;
        this.UsesTerminalFunctions=usesTerminalFunctions;
        this.UsesTrailingComparer=usesTrailingComparer;
        this.UsesNativeLookup=usesNativeLookup;
        this.ContainsValueIndex=containsValueIndex;
        this.ContainsComparerIndex=containsComparerIndex;
    }

    public OperatorKey Key { get; }
    public ProviderIterateTerminalKind TerminalKind { get; }
    public PipelineTerminalKind? PipelineTerminalKind { get; }
    public ProviderNativePredicateBinding PredicateBinding { get; }
    public bool UsesTerminalFunctions { get; }
    public bool UsesTrailingComparer { get; }
    public bool UsesNativeLookup { get; }
    public int ContainsValueIndex { get; }
    public int ContainsComparerIndex { get; }
}

internal static partial class OperatorRegistry {
    private static ImmutableDictionary<OperatorKey,ProviderNativeTerminalProfile> CreateProviderNativeProfiles() {
        return new ProviderNativeTerminalProfile[] {
            Profile("All","predicate",ProviderIterateTerminalKind.All,null,ProviderNativePredicateBinding.Terminal),
            Profile("Any","predicate",ProviderIterateTerminalKind.Any,null,ProviderNativePredicateBinding.Filter),
            Profile("Any","",ProviderIterateTerminalKind.Any,null,ProviderNativePredicateBinding.None),
            Profile("Contains","comparer",ProviderIterateTerminalKind.Contains,null,ProviderNativePredicateBinding.None,usesNativeLookup:true,containsValueIndex:2,containsComparerIndex:3),
            Profile("Contains","",ProviderIterateTerminalKind.Contains,null,ProviderNativePredicateBinding.None,usesNativeLookup:true,containsValueIndex:2),
            Profile("Count","predicate",ProviderIterateTerminalKind.Count,null,ProviderNativePredicateBinding.Filter),
            Profile("Count","",ProviderIterateTerminalKind.Count,null,ProviderNativePredicateBinding.None),
            Profile("First","predicate",ProviderIterateTerminalKind.First,null,ProviderNativePredicateBinding.Filter),
            Profile("First","",ProviderIterateTerminalKind.First,null,ProviderNativePredicateBinding.None),
            Profile("FirstOrDefault","predicate",ProviderIterateTerminalKind.FirstOrDefault,null,ProviderNativePredicateBinding.Filter),
            Profile("FirstOrDefault","",ProviderIterateTerminalKind.FirstOrDefault,null,ProviderNativePredicateBinding.None),
            Profile("Sum","selector",ProviderIterateTerminalKind.Sum,PipelineTerminalKind.Sum,ProviderNativePredicateBinding.Select),
            Profile("Sum","",ProviderIterateTerminalKind.Sum,PipelineTerminalKind.Sum,ProviderNativePredicateBinding.None),
            Profile("ToArray","",ProviderIterateTerminalKind.ToArray,PipelineTerminalKind.ToArray,ProviderNativePredicateBinding.None),
            Profile("ToDictionary","keySelector_elementSelector_comparer",ProviderIterateTerminalKind.ToDictionary,PipelineTerminalKind.ToDictionary,ProviderNativePredicateBinding.None,usesTerminalFunctions:true,usesTrailingComparer:true),
            Profile("ToDictionary","keySelector_comparer",ProviderIterateTerminalKind.ToDictionary,PipelineTerminalKind.ToDictionary,ProviderNativePredicateBinding.None,usesTerminalFunctions:true,usesTrailingComparer:true),
            Profile("ToDictionary","keySelector_elementSelector",ProviderIterateTerminalKind.ToDictionary,PipelineTerminalKind.ToDictionary,ProviderNativePredicateBinding.None,usesTerminalFunctions:true),
            Profile("ToDictionary","keySelector",ProviderIterateTerminalKind.ToDictionary,PipelineTerminalKind.ToDictionary,ProviderNativePredicateBinding.None,usesTerminalFunctions:true),
            Profile("ToList","",ProviderIterateTerminalKind.ToList,PipelineTerminalKind.ToList,ProviderNativePredicateBinding.None),
        }.ToImmutableDictionary(static profile => profile.Key);
    }

    private static ProviderNativeTerminalProfile Profile(
        string methodName,
        string variantName,
        ProviderIterateTerminalKind terminalKind,
        PipelineTerminalKind? pipelineTerminalKind,
        ProviderNativePredicateBinding predicateBinding,
        bool usesTerminalFunctions = false,
        bool usesTrailingComparer = false,
        bool usesNativeLookup = false,
        int containsValueIndex = -1,
        int containsComparerIndex = -1) {
        return new ProviderNativeTerminalProfile(
            new OperatorKey(methodName,variantName),
            terminalKind,
            pipelineTerminalKind,
            predicateBinding,
            usesTerminalFunctions,
            usesTrailingComparer,
            usesNativeLookup,
            containsValueIndex,
            containsComparerIndex);
    }
}

public sealed partial class FLinqIncrementalGenerator {
    private static bool TryCreateBackendNativeIterateFragment(OperatorKey key,FragmentArguments arguments,out Fragment fragment) {
        if(arguments.Count==0) {
            fragment=null!;
            return false;
        }
        return TryCreateBackendNativeIterateFragment(
            key,
            arguments.Number,
            arguments.GetEnumerable(0),
            arguments.GetFirstFunction(),
            new object?[] { arguments.Number }.Concat(arguments.Values).ToArray(),
            out fragment);
    }
}
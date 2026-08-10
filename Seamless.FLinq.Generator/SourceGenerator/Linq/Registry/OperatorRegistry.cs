namespace Seamless.FLinq;

internal enum OperatorCategory {
    Sequence,
    Scalar,
    Terminal,
    ProviderNative,
    Root,
    CoverageOnly,
}

internal enum OperatorCoverageKind {
    Fragment,
    Pipeline,
    ProviderNative,
    Root,
    Excluded,
}

[Flags]
internal enum OperatorCapability {
    None=0,
    ProviderNativeIterate=1,
}

internal enum FragmentArgumentKind {
    Source,
    Enumerable,
    Function,
    Expression,
    ResultType,
    ResultElementType,
}

internal readonly struct OperatorKey:IEquatable<OperatorKey> {
    public OperatorKey(string methodName,string variantName) {
        this.MethodName=methodName;
        this.VariantName=variantName;
    }

    public string MethodName { get; }
    public string VariantName { get; }

    public bool Equals(OperatorKey other) => string.Equals(this.MethodName,other.MethodName,StringComparison.Ordinal)
        &&string.Equals(this.VariantName,other.VariantName,StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is OperatorKey other&&this.Equals(other);

    public override int GetHashCode() {
        unchecked {
            return (StringComparer.Ordinal.GetHashCode(this.MethodName)*397)^StringComparer.Ordinal.GetHashCode(this.VariantName);
        }
    }

    public override string ToString() => this.VariantName.Length==0 ? this.MethodName : this.MethodName+"_"+this.VariantName;
}

internal readonly struct FragmentParameter {
    private FragmentParameter(FragmentArgumentKind kind,string? name) {
        this.Kind=kind;
        this.Name=name;
    }

    public FragmentArgumentKind Kind { get; }
    public string? Name { get; }

    public static FragmentParameter Source { get; } = new(FragmentArgumentKind.Source,null);
    public static FragmentParameter Enumerable { get; } = new(FragmentArgumentKind.Enumerable,null);
    public static FragmentParameter Function { get; } = new(FragmentArgumentKind.Function,null);
    public static FragmentParameter ResultType { get; } = new(FragmentArgumentKind.ResultType,"resultType");
    public static FragmentParameter ResultElementType { get; } = new(FragmentArgumentKind.ResultElementType,"resultElementType");
    public static FragmentParameter Expression(string name) => new(FragmentArgumentKind.Expression,name);
}

/// <summary>
/// 生成時にBindが返す共通境界です。値をobjectのリストで保持せず、各VariantのArgs型が実データを所有します。
/// </summary>
internal abstract class FragmentArguments {
}

/// <summary>
/// MethodBodyRewriterなどの既に構築済みFragmentをRegistryへ渡すための限定された直接呼び出し値です。
/// 通常のSemanticModelバインドでは使用しません。
/// </summary>
internal sealed class DirectFragmentArguments {
    private readonly IReadOnlyList<object?> values;

    internal DirectFragmentArguments(int number,IReadOnlyList<object?> values) {
        this.Number=number;
        this.values=values;
    }

    internal int Number { get; }
    internal int Count => this.values.Count;

    internal bool TryGetEnumerable(int index,out Enumerable value) =>
        TryGet(index,out value);

    internal bool TryGetFunction(int index,out FuncFragment value) =>
        TryGet(index,out value);

    internal bool TryGetExpression(int index,out string value) =>
        TryGet(index,out value);

    private bool TryGet<T>(int index,out T value) {
        if(index>=0&&index<this.values.Count&&this.values[index] is T typed) {
            value=typed;
            return true;
        }
        value=default!;
        return false;
    }
}

internal interface IOperatorDescriptor {
    OperatorKey Key { get; }
    OperatorCategory Category { get; }
    OperatorCoverageKind Coverage { get; }
    OperatorCapability Capabilities { get; }
    int Priority { get; }
    Type FragmentType { get; }
    Type ArgumentsType { get; }
    IReadOnlyList<FragmentParameter> Parameters { get; }
    PipelineStageKind? PipelineStage { get; }
    PipelineStageCapability PipelineCapabilities { get; }
    bool SupportsProviderNativePipeline { get; }

    bool IsApplicable(IMethodSymbol method);
    bool TryCreate(FLinqIncrementalGenerator.FragmentFactoryContext context,out Fragment fragment);
    bool TryCreateDirect(DirectFragmentArguments arguments,out Fragment fragment);
}

internal interface IRootOperatorDescriptor:IOperatorDescriptor {
    bool TryCreateRoot(FLinqIncrementalGenerator.RootFactoryContext context,out Enumerable fragment);
}

internal sealed class OperatorDescriptor<TFragment,TArguments>:IOperatorDescriptor
    where TFragment:Fragment
    where TArguments:FragmentArguments {
    private readonly Func<FLinqIncrementalGenerator.FragmentFactoryContext,TArguments?> bind;
    private readonly Func<DirectFragmentArguments,TArguments?> directBind;
    private readonly Func<TArguments,TFragment> factory;
    private readonly Func<IMethodSymbol,bool>? isApplicable;

    public OperatorDescriptor(
        OperatorKey key,
        OperatorCategory category,
        OperatorCoverageKind coverage,
        OperatorCapability capabilities,
        int priority,
        IReadOnlyList<FragmentParameter> parameters,
        Func<FLinqIncrementalGenerator.FragmentFactoryContext,TArguments?> bind,
        Func<DirectFragmentArguments,TArguments?> directBind,
        Func<TArguments,TFragment> factory,
        Func<IMethodSymbol,bool>? isApplicable) {
        this.Key=key;
        this.Category=category;
        this.Coverage=coverage;
        this.Capabilities=capabilities;
        this.Priority=priority;
        this.Parameters=parameters;
        this.bind=bind;
        this.directBind=directBind;
        this.factory=factory;
        this.isApplicable=isApplicable;
    }

    public OperatorKey Key { get; }
    public OperatorCategory Category { get; }
    public OperatorCoverageKind Coverage { get; }
    public OperatorCapability Capabilities { get; }
    public int Priority { get; }
    public Type FragmentType => typeof(TFragment);
    public Type ArgumentsType => typeof(TArguments);
    public IReadOnlyList<FragmentParameter> Parameters { get; }
    public PipelineStageKind? PipelineStage => OperatorPipelineManifest.TryGet(this.Key.MethodName,out var definition) ? definition.Stage : null;
    public PipelineStageCapability PipelineCapabilities => OperatorPipelineManifest.TryGet(this.Key.MethodName,out var definition) ? definition.Capabilities : PipelineStageCapability.None;
    public bool SupportsProviderNativePipeline => OperatorPipelineManifest.TryGet(this.Key.MethodName,out var definition)&&definition.SupportsProviderNative;

    public bool IsApplicable(IMethodSymbol method) => this.isApplicable?.Invoke(method)??true;

    public bool TryCreate(FLinqIncrementalGenerator.FragmentFactoryContext context,out Fragment fragment) {
        fragment=null!;
        var arguments=this.bind(context);
        if(arguments is null) {
            return false;
        }
        if((this.Capabilities&OperatorCapability.ProviderNativeIterate)!=0
            &&arguments is IProviderNativeBoundArguments providerArguments
            &&context.TryCreateProviderNative(this.Key,providerArguments,out fragment)) {
            return true;
        }
        fragment=this.factory(arguments);
        return true;
    }

    public bool TryCreateDirect(DirectFragmentArguments arguments,out Fragment fragment) {
        fragment=null!;
        var typedArguments=this.directBind(arguments);
        if(typedArguments is null) {
            return false;
        }
        fragment=this.factory(typedArguments);
        return true;
    }
}

internal sealed class RootOperatorDescriptor<TFragment,TArguments>:IRootOperatorDescriptor
    where TFragment:Enumerable
    where TArguments:FragmentArguments {
    private readonly Func<FLinqIncrementalGenerator.RootFactoryContext,TArguments?> bind;
    private readonly Func<DirectFragmentArguments,TArguments?> directBind;
    private readonly Func<TArguments,TFragment> factory;
    private readonly Func<IMethodSymbol,bool>? isApplicable;

    public RootOperatorDescriptor(
        OperatorKey key,
        int priority,
        IReadOnlyList<FragmentParameter> parameters,
        Func<FLinqIncrementalGenerator.RootFactoryContext,TArguments?> bind,
        Func<DirectFragmentArguments,TArguments?> directBind,
        Func<TArguments,TFragment> factory,
        Func<IMethodSymbol,bool>? isApplicable) {
        this.Key=key;
        this.Priority=priority;
        this.Parameters=parameters;
        this.bind=bind;
        this.directBind=directBind;
        this.factory=factory;
        this.isApplicable=isApplicable;
    }

    public OperatorKey Key { get; }
    public OperatorCategory Category => OperatorCategory.Root;
    public OperatorCoverageKind Coverage => OperatorCoverageKind.Root;
    public OperatorCapability Capabilities => OperatorCapability.None;
    public int Priority { get; }
    public Type FragmentType => typeof(TFragment);
    public Type ArgumentsType => typeof(TArguments);
    public IReadOnlyList<FragmentParameter> Parameters { get; }
    public PipelineStageKind? PipelineStage => null;
    public PipelineStageCapability PipelineCapabilities => PipelineStageCapability.None;
    public bool SupportsProviderNativePipeline => false;

    public bool IsApplicable(IMethodSymbol method) => this.isApplicable?.Invoke(method)??true;

    public bool TryCreate(FLinqIncrementalGenerator.FragmentFactoryContext context,out Fragment fragment) {
        fragment=null!;
        return false;
    }

    public bool TryCreateDirect(DirectFragmentArguments arguments,out Fragment fragment) {
        fragment=null!;
        return false;
    }

    public bool TryCreateRoot(FLinqIncrementalGenerator.RootFactoryContext context,out Enumerable fragment) {
        fragment=null!;
        var arguments=this.bind(context);
        if(arguments is null) {
            return false;
        }
        fragment=this.factory(arguments);
        return true;
    }
}

internal sealed class AsEnumerableRootOperatorDescriptor:IRootOperatorDescriptor {
    public OperatorKey Key => new("AsEnumerable","root");
    public OperatorCategory Category => OperatorCategory.Root;
    public OperatorCoverageKind Coverage => OperatorCoverageKind.Root;
    public OperatorCapability Capabilities => OperatorCapability.None;
    public int Priority => 999;
    public Type FragmentType => typeof(Enumerable);
    public Type ArgumentsType => typeof(FragmentArguments);
    public IReadOnlyList<FragmentParameter> Parameters { get; } = Array.Empty<FragmentParameter>();
    public PipelineStageKind? PipelineStage => null;
    public PipelineStageCapability PipelineCapabilities => PipelineStageCapability.None;
    public bool SupportsProviderNativePipeline => false;

    public bool IsApplicable(IMethodSymbol method) => method.Parameters.Length==1&&method.TypeArguments.Length==1;

    public bool TryCreate(FLinqIncrementalGenerator.FragmentFactoryContext context,out Fragment fragment) {
        fragment=null!;
        return false;
    }

    public bool TryCreateDirect(DirectFragmentArguments arguments,out Fragment fragment) {
        fragment=null!;
        return false;
    }

    public bool TryCreateRoot(FLinqIncrementalGenerator.RootFactoryContext context,out Enumerable fragment) =>
        context.TryBuildAsEnumerableSource(out fragment);
}

internal static partial class OperatorRegistry {
    private static readonly ImmutableHashSet<string> EnumerableCoverageNames=CreateEnumerableCoverageNames();
    private static readonly ImmutableArray<IOperatorDescriptor> descriptors=CreateDescriptors();
    private static readonly ImmutableDictionary<string,ImmutableArray<IOperatorDescriptor>> descriptorsByMethod=descriptors
        .GroupBy(static descriptor => descriptor.Key.MethodName,StringComparer.Ordinal)
        .ToImmutableDictionary(
            static group => group.Key,
            static group => group.OrderByDescending(static descriptor => descriptor.Priority)
                .ThenBy(static descriptor => descriptor.Key.VariantName,StringComparer.Ordinal)
                .ToImmutableArray(),
            StringComparer.Ordinal);
    private static readonly ImmutableDictionary<OperatorKey,IOperatorDescriptor> descriptorsByKey=descriptors
        .GroupBy(static descriptor => descriptor.Key)
        .Where(static group => group.Count()==1)
        .ToImmutableDictionary(static group => group.Key,static group => group.Single());
    private static readonly ImmutableDictionary<string,ImmutableArray<IRootOperatorDescriptor>> rootDescriptorsByMethod=descriptors
        .OfType<IRootOperatorDescriptor>()
        .GroupBy(static descriptor => descriptor.Key.MethodName,StringComparer.Ordinal)
        .ToImmutableDictionary(
            static group => group.Key,
            static group => group.OrderByDescending(static descriptor => descriptor.Priority).ToImmutableArray(),
            StringComparer.Ordinal);
    private static readonly Lazy<ImmutableDictionary<OperatorKey,ProviderNativeTerminalProfile>> providerNativeProfiles=
        new(CreateProviderNativeProfiles);
    private static readonly ImmutableArray<string> validationErrors=Validate(descriptors,EnumerableCoverageNames);
    private static readonly ImmutableArray<OperatorCoverageManifestEntry> coverageManifest=OperatorCoverageManifest.Create(EnumerableCoverageNames);

    internal static ImmutableArray<IOperatorDescriptor> Descriptors => descriptors;
    internal static ImmutableArray<string> ValidationErrors => validationErrors;
    internal static ImmutableArray<OperatorCoverageManifestEntry> CoverageManifest => coverageManifest;

    internal static bool TryGetCandidates(string methodName,out ImmutableArray<IOperatorDescriptor> candidates) => descriptorsByMethod.TryGetValue(methodName,out candidates);

    internal static bool TryCreateDirect(string methodName,string variantName,DirectFragmentArguments arguments,out Fragment fragment) {
        fragment=null!;
        return descriptorsByKey.TryGetValue(new OperatorKey(methodName,variantName),out var descriptor)
            &&descriptor.TryCreateDirect(arguments,out fragment);
    }

    internal static bool IsKnownEnumerableMethodName(string methodName) => EnumerableCoverageNames.Contains(methodName);

    internal static bool IsRootMethodName(string methodName) => rootDescriptorsByMethod.ContainsKey(methodName);

    internal static bool TryCreateRoot(FLinqIncrementalGenerator.RootFactoryContext context,out Enumerable fragment) {
        fragment=null!;
        if(!rootDescriptorsByMethod.TryGetValue(context.Method.Name,out var candidates)) {
            return false;
        }
        foreach(var candidate in candidates) {
            if(candidate.IsApplicable(context.Method)
                &&candidate.TryCreateRoot(context,out fragment)) {
                return true;
            }
        }
        return false;
    }

    internal static bool SupportsProviderNativeIterate(string methodName,string variantName) => descriptorsByKey.TryGetValue(new OperatorKey(methodName,variantName),out var descriptor)
        &&(descriptor.Capabilities&OperatorCapability.ProviderNativeIterate)!=0;

    internal static bool TryGetProviderNativeProfile(OperatorKey key,out ProviderNativeTerminalProfile profile) =>
        providerNativeProfiles.Value.TryGetValue(key,out profile);

    internal static ImmutableArray<string> ValidateForTesting(IEnumerable<IOperatorDescriptor> candidateDescriptors,IEnumerable<string> coverageNames) =>
        Validate(candidateDescriptors.ToImmutableArray(),coverageNames.ToImmutableArray());

    private static IOperatorDescriptor Create<TFragment,TArguments>(
        string methodName,
        string variantName,
        OperatorCategory category,
        OperatorCoverageKind coverage,
        OperatorCapability capabilities,
        int priority,
        Func<FLinqIncrementalGenerator.FragmentFactoryContext,TArguments?> bind,
        Func<DirectFragmentArguments,TArguments?> directBind,
        FragmentParameter[] parameters,
        Func<TArguments,TFragment> factory,
        Func<IMethodSymbol,bool>? isApplicable = null)
        where TFragment:Fragment
        where TArguments:FragmentArguments {
        var key=new OperatorKey(methodName,variantName);
        return new OperatorDescriptor<TFragment,TArguments>(
            key,
            category,
            coverage,
            capabilities,
            priority,
            parameters,
            bind,
            directBind,
            factory,
            isApplicable);
    }

    private static IOperatorDescriptor CoverageOnly(string methodName,OperatorCoverageKind coverage) =>
        new CoverageOnlyOperatorDescriptor(new OperatorKey(methodName,string.Empty),coverage);

    private static IRootOperatorDescriptor CreateAsEnumerableRoot() => new AsEnumerableRootOperatorDescriptor();

    private static IRootOperatorDescriptor CreateRoot<TFragment,TArguments>(
        string methodName,
        int priority,
        Func<FLinqIncrementalGenerator.RootFactoryContext,TArguments?> bind,
        Func<DirectFragmentArguments,TArguments?> directBind,
        FragmentParameter[] parameters,
        Func<TArguments,TFragment> factory,
        Func<IMethodSymbol,bool>? isApplicable = null)
        where TFragment:Enumerable
        where TArguments:FragmentArguments {
        return new RootOperatorDescriptor<TFragment,TArguments>(
            new OperatorKey(methodName,"root"),
            priority,
            parameters,
            bind,
            directBind,
            factory,
            isApplicable);
    }

    private static ImmutableArray<string> Validate(ImmutableArray<IOperatorDescriptor> candidateDescriptors,IEnumerable<string> coverageNames) {
        var coverageArray=coverageNames.ToArray();
        var manifest=OperatorCoverageManifest.Create(coverageArray);
        var errors=ImmutableArray.CreateBuilder<string>();
        foreach(var group in candidateDescriptors.GroupBy(static descriptor => descriptor.Key)) {
            if(group.Count()!=1) {
                errors.Add("Duplicate OperatorKey: "+group.Key);
            }
        }
        foreach(var methodName in coverageArray) {
            if(!candidateDescriptors.Any(descriptor => descriptor.Key.MethodName==methodName)) {
                errors.Add("Unregistered public LINQ API: "+methodName);
            }
        }
        foreach(var coverageName in coverageArray.GroupBy(static name => name,StringComparer.Ordinal).Where(static group => group.Count()!=1)) {
            errors.Add("Duplicate public LINQ API coverage: "+coverageName.Key);
        }
        foreach(var entry in manifest) {
            if(entry.ExceptionCoverage==OperatorExceptionCoverage.ExplicitlyExcluded) {
                continue;
            }
            var methodDescriptors=candidateDescriptors.Where(descriptor => descriptor.Key.MethodName==entry.MethodName).ToArray();
            if(entry.HasRootVariant&&!methodDescriptors.OfType<IRootOperatorDescriptor>().Any()) {
                errors.Add("Missing root coverage: "+entry.MethodName);
            }
            if(OperatorPipelineManifest.TryGet(entry.MethodName,out var pipelineDefinition)&&!methodDescriptors.Any(descriptor => descriptor.PipelineStage==pipelineDefinition.Stage)) {
                errors.Add("Missing pipeline coverage: "+entry.MethodName);
            }
        }
        foreach(var group in candidateDescriptors.GroupBy(static descriptor => descriptor.Key.MethodName,StringComparer.Ordinal)) {
            var priorities=new HashSet<int>();
            foreach(var descriptor in group) {
                if(!priorities.Add(descriptor.Priority)) {
                    errors.Add("Priority collision: "+group.Key+" ("+descriptor.Priority+")");
                }
                if(!typeof(Fragment).IsAssignableFrom(descriptor.FragmentType)
                    ||!typeof(FragmentArguments).IsAssignableFrom(descriptor.ArgumentsType)) {
                    errors.Add("Invalid factory types: "+descriptor.Key);
                }
                if((descriptor.Capabilities&OperatorCapability.ProviderNativeIterate)!=0
                    &&descriptor.Coverage!=OperatorCoverageKind.Fragment) {
                    errors.Add("Provider capability/coverage mismatch: "+descriptor.Key);
                }
                if(descriptor.Category==OperatorCategory.Root&&descriptor.Coverage!=OperatorCoverageKind.Root) {
                    errors.Add("Root category/coverage mismatch: "+descriptor.Key);
                }
                if(descriptor.Category!=OperatorCategory.CoverageOnly&&descriptor.Parameters.Count==0&&descriptor.Coverage!=OperatorCoverageKind.Excluded&&!(descriptor.Category==OperatorCategory.Root&&descriptor.Key.MethodName=="AsEnumerable")) {
                    errors.Add("Missing declarative parameters: "+descriptor.Key);
                }
            }
        }
        var nativeProfiles=providerNativeProfiles.Value;
        foreach(var descriptor in candidateDescriptors.Where(static descriptor => (descriptor.Capabilities&OperatorCapability.ProviderNativeIterate)!=0)) {
            if(!nativeProfiles.ContainsKey(descriptor.Key)) {
                errors.Add("Missing provider-native profile: "+descriptor.Key);
            }
        }
        foreach(var profile in nativeProfiles.Keys) {
            if(!candidateDescriptors.Any(descriptor => descriptor.Key.Equals(profile)
                &&(descriptor.Capabilities&OperatorCapability.ProviderNativeIterate)!=0)) {
                errors.Add("Provider-native profile without capability: "+profile);
            }
        }
        return errors.ToImmutable();
    }

    private sealed class CoverageOnlyOperatorDescriptor:IOperatorDescriptor {
        public CoverageOnlyOperatorDescriptor(OperatorKey key,OperatorCoverageKind coverage) {
            this.Key=key;
            this.Coverage=coverage;
        }

        public OperatorKey Key { get; }
        public OperatorCategory Category => OperatorCategory.CoverageOnly;
        public OperatorCoverageKind Coverage { get; }
        public OperatorCapability Capabilities => OperatorCapability.None;
        public int Priority => -1;
        public Type FragmentType => typeof(Fragment);
        public Type ArgumentsType => typeof(FragmentArguments);
        public IReadOnlyList<FragmentParameter> Parameters { get; } = Array.Empty<FragmentParameter>();
        public PipelineStageKind? PipelineStage => null;
        public PipelineStageCapability PipelineCapabilities => PipelineStageCapability.None;
        public bool SupportsProviderNativePipeline => false;
        public bool IsApplicable(IMethodSymbol method) => false;

        public bool TryCreate(FLinqIncrementalGenerator.FragmentFactoryContext context,out Fragment fragment) {
            fragment=null!;
            return false;
        }

        public bool TryCreateDirect(DirectFragmentArguments arguments,out Fragment fragment) {
            fragment=null!;
            return false;
        }
    }
}

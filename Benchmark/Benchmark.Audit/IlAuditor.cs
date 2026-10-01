using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Benchmark.Audit;

/// <summary>ベンチのソース種別。メモリ上4種と保存層。</summary>
internal enum SourceKind {
    Array,
    List,
    IEnumerable,
    Range,
    Persisted,
    Tsavorite,
    Unknown,
}

/// <summary>ベンチメソッド1つ分の監査結果。数値は IL 上の静的な出現数で、実行回数ではない。</summary>
internal sealed record AuditRow(
    string Assembly,
    string DeclaringType,
    string Method,
    SourceKind Source,
    bool Intercepted,
    bool ImplCalledDirectly,
    IReadOnlyList<string> EntryPoints,
    int IlSize,
    int LoopCount,
    int MaxLoopDepth,
    int InterfaceCallsInLoop,
    int InterfaceCallsOutsideLoop,
    int DelegateInvokesInLoop,
    int DelegateInvokesOutsideLoop,
    int AllocationsInLoop,
    int AllocationsOutsideLoop,
    int BoxesInLoop,
    int BoxesOutsideLoop,
    int CounterCallsInLoop,
    int CounterCallsOutsideLoop,
    int ExternalLinqCalls,
    IReadOnlyList<string> ExternalLinqTargets,
    bool ExpansionStub,
    IReadOnlyList<string> LibraryCalls,
    IReadOnlyList<string> InterfaceCallTargets,
    int HelperTypes,
    int UnusedHelperTypes);

/// <summary>メソッド参照(呼び出し先)の解決結果。</summary>
internal readonly record struct MethodTarget(string DeclaringTypeFullName,string Name,ResolvedType? DeclaringType,MethodDefinitionHandle LocalDefinition) {
    public bool IsLocal => !this.LocalDefinition.IsNil;
    public string DisplayName => this.DeclaringTypeFullName+"::"+this.Name;
}

/// <summary>
/// ベンチアセンブリの IL を読み、FLinq の置き換え(傍受)が起きたかと、置き換え後のコードの形を数える。
/// 傍受は `[BenchFact]` メソッドから `X_Impl` への呼び出しが `Intercept.*` の生成メソッドへ差し替わることで起きる。
/// </summary>
internal sealed class IlAuditor(MetadataCatalog catalog) {
    private const int DiscoveryDepth = 4;
    private const int TraversalDepth = 12;

    public IReadOnlyList<AuditRow> Audit(LoadedAssembly assembly) {
        var rows = new List<AuditRow>();
        var reader = assembly.Reader;
        foreach(var typeHandle in reader.TypeDefinitions) {
            var type = reader.GetTypeDefinition(typeHandle);
            foreach(var methodHandle in type.GetMethods()) {
                if(!this.HasAttribute(assembly,reader.GetMethodDefinition(methodHandle).GetCustomAttributes(),"BenchFactAttribute")) {
                    continue;
                }
                rows.Add(this.AuditBenchmark(assembly,typeHandle,methodHandle));
            }
        }
        return rows;
    }

    private AuditRow AuditBenchmark(LoadedAssembly assembly,TypeDefinitionHandle typeHandle,MethodDefinitionHandle methodHandle) {
        var reader = assembly.Reader;
        var methodName = reader.GetString(reader.GetMethodDefinition(methodHandle).Name);
        var implName = methodName+"_Impl";
        var entries = new List<MethodDefinitionHandle>();
        var implCalled = false;
        var externalLinqInImpl = new List<string>();
        var queue = new Queue<(MethodDefinitionHandle Handle,int Depth)>();
        var seen = new HashSet<MethodDefinitionHandle>();
        queue.Enqueue((methodHandle,0));
        seen.Add(methodHandle);
        while(queue.Count>0) {
            var (current,depth) = queue.Dequeue();
            var body = assembly.GetMethodBody(current);
            if(body is null) {
                continue;
            }
            foreach(var instruction in IlReader.Decode(body)) {
                if(!IsMethodOperand(instruction.Code)) {
                    continue;
                }
                var target = this.ResolveMethod(assembly,MetadataTokens.EntityHandle(instruction.Token));
                if(!target.IsLocal) {
                    continue;
                }
                if(this.IsInterceptMethod(assembly,target.LocalDefinition)) {
                    if(!entries.Contains(target.LocalDefinition)) {
                        entries.Add(target.LocalDefinition);
                    }
                    continue;
                }
                if(target.Name==implName) {
                    implCalled=true;
                    externalLinqInImpl.AddRange(this.ExternalLinqCalls(assembly,target.LocalDefinition));
                    continue;
                }
                if(depth+1<=DiscoveryDepth&&target.Name.StartsWith('<')&&seen.Add(target.LocalDefinition)) {
                    queue.Enqueue((target.LocalDefinition,depth+1));
                }
            }
        }

        var metrics = new Metrics();
        foreach(var entry in entries) {
            this.Traverse(assembly,entry,false,0,metrics,[]);
        }
        var helperTypes = this.HelperTypes(assembly,entries);
        var unused = helperTypes.Count(helper => !metrics.ReferencedTypes.Contains(helper));
        return new AuditRow(
            assembly.Name,
            MetadataNames.TypeDefinitionFullName(reader,typeHandle),
            methodName,
            Classify(assembly,reader,typeHandle),
            entries.Count>0,
            implCalled,
            entries.Select(entry => reader.GetString(reader.GetMethodDefinition(entry).Name)).ToArray(),
            metrics.IlSize,
            metrics.LoopCount,
            metrics.MaxLoopDepth,
            metrics.InterfaceCallsInLoop,
            metrics.InterfaceCallsOutsideLoop,
            metrics.DelegateInvokesInLoop,
            metrics.DelegateInvokesOutsideLoop,
            metrics.AllocationsInLoop,
            metrics.AllocationsOutsideLoop,
            metrics.BoxesInLoop,
            metrics.BoxesOutsideLoop,
            metrics.CounterCallsInLoop,
            metrics.CounterCallsOutsideLoop,
            metrics.ExternalLinqTargets.Count+externalLinqInImpl.Count,
            metrics.ExternalLinqTargets.Concat(externalLinqInImpl).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            metrics.ExpansionStub,
            metrics.LibraryCalls.Order(StringComparer.Ordinal).ToArray(),
            metrics.InterfaceCallTargets.Order(StringComparer.Ordinal).ToArray(),
            helperTypes.Count,
            unused);
    }

    private sealed class Metrics {
        public int IlSize;
        public int LoopCount;
        public int MaxLoopDepth;
        public int InterfaceCallsInLoop;
        public int InterfaceCallsOutsideLoop;
        public int DelegateInvokesInLoop;
        public int DelegateInvokesOutsideLoop;
        public int AllocationsInLoop;
        public int AllocationsOutsideLoop;
        public int BoxesInLoop;
        public int BoxesOutsideLoop;
        public int CounterCallsInLoop;
        public int CounterCallsOutsideLoop;
        public bool ExpansionStub;
        public List<string> ExternalLinqTargets { get; } = [];
        public HashSet<string> LibraryCalls { get; } = new(StringComparer.Ordinal);
        public HashSet<string> InterfaceCallTargets { get; } = new(StringComparer.Ordinal);
        public HashSet<TypeDefinitionHandle> ReferencedTypes { get; } = [];
        public HashSet<(MethodDefinitionHandle,bool)> Visited { get; } = [];
    }

    /// <summary>
    /// 置き換えメソッドから、同じアセンブリの Intercept 名前空間のメソッドだけを辿って数える。
    /// ループ内の呼び出しから辿った先は、呼び出し先の本体すべてをループ内として数える。
    /// </summary>
    private void Traverse(LoadedAssembly assembly,MethodDefinitionHandle handle,bool calledInLoop,int depth,Metrics metrics,List<MethodDefinitionHandle> stack) {
        if(depth>TraversalDepth||!metrics.Visited.Add((handle,calledInLoop))||stack.Contains(handle)) {
            return;
        }
        var reader = assembly.Reader;
        var definition = reader.GetMethodDefinition(handle);
        metrics.ReferencedTypes.Add(definition.GetDeclaringType());
        this.CollectSignatureTypes(reader,definition,metrics.ReferencedTypes);
        var body = assembly.GetMethodBody(handle);
        if(body is null) {
            return;
        }
        if(!body.LocalSignature.IsNil) {
            var collector = new SignatureTypeCollector();
            reader.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(collector,null);
            metrics.ReferencedTypes.UnionWith(collector.Definitions);
        }
        var instructions = IlReader.Decode(body);
        var loops = IlReader.FindLoops(instructions);
        metrics.IlSize+=body.Size;
        metrics.LoopCount+=loops.Count;
        stack.Add(handle);
        for(var index = 0;index<instructions.Count;index++) {
            var instruction = instructions[index];
            var next = default(short);
            if(index+1<instructions.Count) {
                next=instructions[index+1].Code;
            }
            var loopDepth = IlReader.LoopDepth(loops,instruction.Offset);
            var inLoop = calledInLoop||loopDepth>0;
            metrics.MaxLoopDepth=Math.Max(metrics.MaxLoopDepth,loopDepth);
            if(instruction.Token!=0) {
                this.CollectTokenTypes(assembly,MetadataTokens.EntityHandle(instruction.Token),metrics.ReferencedTypes);
            }
            if(instruction.Code==IlReader.Box) {
                // box の直後が型判定・分岐・即時の取り出しなら、JIT は割り当てを消すので数えない。
                if(!IlReader.IsBoxElidedBy(next)) {
                    Increment(ref metrics.BoxesInLoop,ref metrics.BoxesOutsideLoop,inLoop);
                }
                continue;
            }
            if(instruction.Code==IlReader.Newarr) {
                Increment(ref metrics.AllocationsInLoop,ref metrics.AllocationsOutsideLoop,inLoop);
                continue;
            }
            if(!IsMethodOperand(instruction.Code)) {
                continue;
            }
            var target = this.ResolveMethod(assembly,MetadataTokens.EntityHandle(instruction.Token));
            if(instruction.Code==IlReader.Newobj) {
                // 例外を作ってすぐ投げる経路は通常通らないので数えない。
                if(next==IlReader.Throw) {
                    continue;
                }
                if(target.DeclaringType is not { } created||!catalog.IsValueType(created)) {
                    Increment(ref metrics.AllocationsInLoop,ref metrics.AllocationsOutsideLoop,inLoop);
                }
                continue;
            }
            if(instruction.Code==IlReader.Ldftn||instruction.Code==IlReader.Ldvirtftn) {
                continue;
            }
            this.CountCall(target,instruction,inLoop,metrics);
            if(target.IsLocal&&this.IsInterceptMethod(assembly,target.LocalDefinition)) {
                this.Traverse(assembly,target.LocalDefinition,inLoop,depth+1,metrics,stack);
            }
        }
        stack.RemoveAt(stack.Count-1);
    }

    private void CountCall(MethodTarget target,IlInstruction instruction,bool inLoop,Metrics metrics) {
        var declaring = target.DeclaringTypeFullName;
        if(IsExternalLinq(declaring)) {
            metrics.ExternalLinqTargets.Add(target.DisplayName);
        }
        if(declaring.EndsWith("LinqCounterScope",StringComparison.Ordinal)) {
            Increment(ref metrics.CounterCallsInLoop,ref metrics.CounterCallsOutsideLoop,inLoop);
            return;
        }
        if(declaring.EndsWith("GeneratedCodeHelpers",StringComparison.Ordinal)
            &&target.Name is "UnsupportedLinqExpansion" or "LinqInterceptorFailure") {
            metrics.ExpansionStub=true;
        }
        if(target.DeclaringType is { } declaringType&&!target.IsLocal) {
            var assemblyName = declaringType.Assembly.Name;
            if(assemblyName.StartsWith("Seamless",StringComparison.Ordinal)) {
                metrics.LibraryCalls.Add(target.DisplayName);
            }
        }
        if(instruction.Code!=IlReader.Callvirt||instruction.Constrained||target.DeclaringType is not { } type) {
            return;
        }
        if(catalog.IsDelegate(type)&&target.Name=="Invoke") {
            Increment(ref metrics.DelegateInvokesInLoop,ref metrics.DelegateInvokesOutsideLoop,inLoop);
            return;
        }
        if(catalog.IsInterface(type)) {
            Increment(ref metrics.InterfaceCallsInLoop,ref metrics.InterfaceCallsOutsideLoop,inLoop);
            metrics.InterfaceCallTargets.Add(target.DisplayName);
        }
    }

    private static void Increment(ref int inLoopCount,ref int outsideCount,bool inLoop) {
        if(inLoop) {
            inLoopCount++;
        } else {
            outsideCount++;
        }
    }

    private IEnumerable<string> ExternalLinqCalls(LoadedAssembly assembly,MethodDefinitionHandle handle) {
        var body = assembly.GetMethodBody(handle);
        if(body is null) {
            yield break;
        }
        foreach(var instruction in IlReader.Decode(body)) {
            if(!IsMethodOperand(instruction.Code)) {
                continue;
            }
            var target = this.ResolveMethod(assembly,MetadataTokens.EntityHandle(instruction.Token));
            if(IsExternalLinq(target.DeclaringTypeFullName)) {
                yield return target.DisplayName;
            }
        }
    }

    private static bool IsExternalLinq(string declaringTypeFullName) =>
        declaringTypeFullName=="System.Linq.Enumerable"||declaringTypeFullName.StartsWith("ZLinq.",StringComparison.Ordinal);

    /// <summary>名前空間が条件に合う型の全メソッド本体から、呼び出し先が条件に合う呼び出しを「呼び出し元 -> 呼び出し先」で列挙する。</summary>
    public IReadOnlyList<string> FindCalls(LoadedAssembly assembly,Func<string,bool> callerNamespace,Func<string,bool> calleeType) {
        var reader = assembly.Reader;
        var found = new List<string>();
        foreach(var typeHandle in reader.TypeDefinitions) {
            if(!callerNamespace(MetadataNames.OutermostNamespace(reader,typeHandle))) {
                continue;
            }
            foreach(var methodHandle in reader.GetTypeDefinition(typeHandle).GetMethods()) {
                var body = assembly.GetMethodBody(methodHandle);
                if(body is null) {
                    continue;
                }
                foreach(var instruction in IlReader.Decode(body)) {
                    if(!IsMethodOperand(instruction.Code)) {
                        continue;
                    }
                    var target = this.ResolveMethod(assembly,MetadataTokens.EntityHandle(instruction.Token));
                    if(calleeType(target.DeclaringTypeFullName)) {
                        found.Add(MetadataNames.TypeDefinitionFullName(reader,typeHandle)+"::"+reader.GetString(reader.GetMethodDefinition(methodHandle).Name)+" -> "+target.DisplayName);
                    }
                }
            }
        }
        return found;
    }

    private static bool IsMethodOperand(short code) =>
        code==IlReader.Call||code==IlReader.Callvirt||code==IlReader.Newobj||code==IlReader.Ldftn||code==IlReader.Ldvirtftn;

    private bool IsInterceptMethod(LoadedAssembly assembly,MethodDefinitionHandle handle) {
        var declaring = assembly.Reader.GetMethodDefinition(handle).GetDeclaringType();
        var ns = MetadataNames.OutermostNamespace(assembly.Reader,declaring);
        return ns=="Intercept"||ns.StartsWith("Intercept.",StringComparison.Ordinal);
    }

    /// <summary>置き換えメソッドごとの補助型(名前が「置き換えメソッド名_」で始まる入れ子型)。</summary>
    private IReadOnlyList<TypeDefinitionHandle> HelperTypes(LoadedAssembly assembly,IReadOnlyList<MethodDefinitionHandle> entries) {
        var reader = assembly.Reader;
        var helpers = new List<TypeDefinitionHandle>();
        foreach(var entry in entries) {
            var prefix = reader.GetString(reader.GetMethodDefinition(entry).Name)+"_";
            var owner = reader.GetTypeDefinition(reader.GetMethodDefinition(entry).GetDeclaringType());
            foreach(var nested in owner.GetNestedTypes()) {
                if(reader.GetString(reader.GetTypeDefinition(nested).Name).StartsWith(prefix,StringComparison.Ordinal)) {
                    helpers.Add(nested);
                }
            }
        }
        return helpers;
    }

    public MethodTarget ResolveMethod(LoadedAssembly assembly,EntityHandle handle) {
        var reader = assembly.Reader;
        switch(handle.Kind) {
            case HandleKind.MethodDefinition: {
                var definitionHandle = (MethodDefinitionHandle)handle;
                var definition = reader.GetMethodDefinition(definitionHandle);
                var declaring = new ResolvedType(assembly,definition.GetDeclaringType());
                return new MethodTarget(declaring.FullName,reader.GetString(definition.Name),declaring,definitionHandle);
            }
            case HandleKind.MethodSpecification:
                return this.ResolveMethod(assembly,reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method);
            case HandleKind.MemberReference: {
                var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
                var name = reader.GetString(reference.Name);
                if(reference.Parent.Kind==HandleKind.MethodDefinition) {
                    return this.ResolveMethod(assembly,reference.Parent);
                }
                var declaring = catalog.Resolve(assembly,reference.Parent);
                var declaringName = DeclaringName(reader,reference.Parent,declaring);
                var local = default(MethodDefinitionHandle);
                if(declaring is { } declaringType&&ReferenceEquals(declaringType.Assembly,assembly)) {
                    local=FindMethod(reader,declaringType.Definition,name,reader.GetBlobBytes(reference.Signature));
                }
                return new MethodTarget(declaringName,name,declaring,local);
            }
            default:
                return new MethodTarget("?","?",null,default);
        }
    }

    private static string DeclaringName(MetadataReader reader,EntityHandle parent,ResolvedType? resolved) {
        if(resolved is { } type) {
            return type.FullName;
        }
        if(parent.Kind==HandleKind.TypeReference) {
            return MetadataNames.TypeReferenceFullName(reader,(TypeReferenceHandle)parent);
        }
        return "?";
    }

    private static MethodDefinitionHandle FindMethod(MetadataReader reader,TypeDefinition type,string name,byte[] signature) {
        var byName = default(MethodDefinitionHandle);
        foreach(var candidate in type.GetMethods()) {
            var definition = reader.GetMethodDefinition(candidate);
            if(reader.GetString(definition.Name)!=name) {
                continue;
            }
            if(reader.GetBlobBytes(definition.Signature).AsSpan().SequenceEqual(signature)) {
                return candidate;
            }
            byName=candidate;
        }
        return byName;
    }

    private void CollectSignatureTypes(MetadataReader reader,MethodDefinition definition,HashSet<TypeDefinitionHandle> into) {
        var collector = new SignatureTypeCollector();
        definition.DecodeSignature(collector,null);
        into.UnionWith(collector.Definitions);
    }

    private void CollectTokenTypes(LoadedAssembly assembly,EntityHandle handle,HashSet<TypeDefinitionHandle> into) {
        var reader = assembly.Reader;
        var collector = new SignatureTypeCollector();
        switch(handle.Kind) {
            case HandleKind.TypeDefinition:
                into.Add((TypeDefinitionHandle)handle);
                return;
            case HandleKind.TypeSpecification:
                reader.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(collector,null);
                break;
            case HandleKind.MethodDefinition:
                into.Add(reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType());
                return;
            case HandleKind.FieldDefinition: {
                var field = reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                into.Add(field.GetDeclaringType());
                field.DecodeSignature(collector,null);
                break;
            }
            case HandleKind.MethodSpecification: {
                var specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                specification.DecodeSignature(collector,null);
                this.CollectTokenTypes(assembly,specification.Method,into);
                break;
            }
            case HandleKind.MemberReference: {
                var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
                if(reference.GetKind()==MemberReferenceKind.Method) {
                    reference.DecodeMethodSignature(collector,null);
                } else {
                    reference.DecodeFieldSignature(collector,null);
                }
                this.CollectTokenTypes(assembly,reference.Parent,into);
                break;
            }
            default:
                return;
        }
        into.UnionWith(collector.Definitions);
    }

    private bool HasAttribute(LoadedAssembly assembly,CustomAttributeHandleCollection attributes,string attributeTypeName) {
        foreach(var attributeHandle in attributes) {
            var constructor = assembly.Reader.GetCustomAttribute(attributeHandle).Constructor;
            var declaring = this.ResolveMethod(assembly,constructor).DeclaringTypeFullName;
            if(declaring==attributeTypeName||declaring.EndsWith("."+attributeTypeName,StringComparison.Ordinal)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ソース種別。単一演算子ベンチはアセンブリ名で、最適化ベンチは型(とその外側の型)の名前で決める。
    /// </summary>
    private static SourceKind Classify(LoadedAssembly assembly,MetadataReader reader,TypeDefinitionHandle typeHandle) {
        var name = assembly.Name;
        if(name.Contains(".Range.",StringComparison.Ordinal)) {
            return SourceKind.Range;
        }
        if(name.Contains(".List.",StringComparison.Ordinal)) {
            return SourceKind.List;
        }
        if(name.Contains(".IEnumerable.",StringComparison.Ordinal)&&name.StartsWith("Benchmark.System.",StringComparison.Ordinal)) {
            return SourceKind.IEnumerable;
        }
        if(name.StartsWith("Benchmark.Persisted.",StringComparison.Ordinal)) {
            return SourceKind.Persisted;
        }
        if(name.StartsWith("Benchmark.Tsavorite.",StringComparison.Ordinal)) {
            return SourceKind.Tsavorite;
        }
        if(name.StartsWith("Benchmark.System.",StringComparison.Ordinal)) {
            return SourceKind.Array;
        }
        var current = typeHandle;
        while(!current.IsNil) {
            var type = reader.GetTypeDefinition(current);
            var typeName = reader.GetString(type.Name);
            switch(typeName) {
                case "Array`1":
                    return SourceKind.Array;
                case "List`1":
                    return SourceKind.List;
                case "IEnumerable`1":
                    return SourceKind.IEnumerable;
                case "Range":
                    return SourceKind.Range;
                case "Persisted`1":
                    return SourceKind.Persisted;
                case "Tsavorite`1":
                    return SourceKind.Tsavorite;
            }
            current=type.GetDeclaringType();
        }
        return SourceKind.Unknown;
    }
}

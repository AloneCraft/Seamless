namespace Seamless.SourceGenerator.Linq.Fragments;

internal abstract class ToLookupBase(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>string.Empty;
    protected override string StructSources=>source.GetEnumeratorStruct;
    protected override string ComputeMembers=>string.Empty;
    protected override string ComputeExpression=>throw new NotSupportedException();

    protected static string LookupImpl(string elementType,string keyType)=>$$"""
        private sealed class SeamlessGrouping:global::System.Linq.IGrouping<{{keyType}},{{elementType}}>{
            private readonly {{keyType}} key;
            private readonly global::System.Collections.Generic.List<{{elementType}}> elements;
            public SeamlessGrouping({{keyType}} key,global::System.Collections.Generic.List<{{elementType}}> elements){this.key=key;this.elements=elements;}
            public {{keyType}} Key=>this.key;
            public global::System.Collections.Generic.IEnumerator<{{elementType}}> GetEnumerator()=>this.elements.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
        }
        private sealed class SeamlessLookup:global::System.Linq.ILookup<{{keyType}},{{elementType}}>{
            private readonly global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>> map;
            private readonly global::System.Collections.Generic.List<{{keyType}}> order;
            public SeamlessLookup(global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>> map,global::System.Collections.Generic.List<{{keyType}}> order){this.map=map;this.order=order;}
            public int Count=>this.map.Count;
            public global::System.Collections.Generic.IEnumerable<{{elementType}}> this[{{keyType}} key]=>this.map.TryGetValue(key,out var g)?g:global::System.Linq.Enumerable.Empty<{{elementType}}>();
            public bool Contains({{keyType}} key)=>this.map.ContainsKey(key);
            public global::System.Collections.Generic.IEnumerator<global::System.Linq.IGrouping<{{keyType}},{{elementType}}>> GetEnumerator(){
                foreach(var k in this.order) yield return new SeamlessGrouping(k,this.map[k]);
            }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
        }
        """;
}

internal class ToLookup_keySelector(int 番号,Enumerable source,FuncFragment keySelector,string resultType):ToLookupBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers => $$"""
        {{LookupImpl(source.ElementType,FuncReturnTypeName(keySelector))}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}) {
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>();
            var order = new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector, nameof(keySelector), key, item)}}
                if(!map.TryGetValue({{key}}, out var {{group}})) {
                    {{group}} = new global::System.Collections.Generic.List<{{source.ElementType}}>();
                    map.Add({{key}}, {{group}});
                    order.Add({{key}});
                }
                {{group}}.Add({{item}});
            }
            return new SeamlessLookup(map, order);
        }
        """;
}

internal class ToLookup_keySelector_elementSelector(int 番号,Enumerable source,FuncFragment keySelector,FuncFragment elementSelector,string resultType):ToLookupBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[0].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[1].Type.TypeKind == TypeKind.Delegate;
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(keySelector, elementSelector)})";
    protected override string ComputeMembers => $$"""
        {{LookupImpl(FuncReturnTypeName(elementSelector),FuncReturnTypeName(keySelector))}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector, elementSelector)}}) {
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>>();
            var order = new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector, nameof(keySelector), key, item)}}
                {{FuncReturnTypeName(elementSelector)}} {{temp}} = default!;
                {{FuncAssign(elementSelector, nameof(elementSelector), temp, item)}}
                if(!map.TryGetValue({{key}}, out var {{group}})) {
                    {{group}} = new global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>();
                    map.Add({{key}}, {{group}});
                    order.Add({{key}});
                }
                {{group}}.Add({{temp}});
            }
            return new SeamlessLookup(map, order);
        }
        """;
}

internal class ToLookup_keySelector_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer,string resultType):ToLookupBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[0].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[1].Type.TypeKind != TypeKind.Delegate
        && method.Parameters[1].Name == "comparer";
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}, comparer{ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers => $$"""
        {{LookupImpl(source.ElementType,FuncReturnTypeName(keySelector))}}
        private static {{resultType}} Compute(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector)}}) {
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>(comparer);
            var order = new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector, nameof(keySelector), key, item)}}
                if(!map.TryGetValue({{key}}, out var {{group}})) {
                    {{group}} = new global::System.Collections.Generic.List<{{source.ElementType}}>();
                    map.Add({{key}}, {{group}});
                    order.Add({{key}});
                }
                {{group}}.Add({{item}});
            }
            return new SeamlessLookup(map, order);
        }
        """;
}

internal class ToLookup_keySelector_elementSelector_comparer(int 番号,Enumerable source,FuncFragment keySelector,FuncFragment elementSelector,string comparer,string resultType):ToLookupBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 3
        && method.Parameters[0].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[1].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[2].Name == "comparer";
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}, comparer{ClosureArgsSuffix(keySelector, elementSelector)})";
    protected override string ComputeMembers => $$"""
        {{LookupImpl(FuncReturnTypeName(elementSelector),FuncReturnTypeName(keySelector))}}
        private static {{resultType}} Compute(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector, elementSelector)}}) {
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>>(comparer);
            var order = new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector, nameof(keySelector), key, item)}}
                {{FuncReturnTypeName(elementSelector)}} {{temp}} = default!;
                {{FuncAssign(elementSelector, nameof(elementSelector), temp, item)}}
                if(!map.TryGetValue({{key}}, out var {{group}})) {
                    {{group}} = new global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>();
                    map.Add({{key}}, {{group}});
                    order.Add({{key}});
                }
                {{group}}.Add({{temp}});
            }
            return new SeamlessLookup(map, order);
        }
        """;
}

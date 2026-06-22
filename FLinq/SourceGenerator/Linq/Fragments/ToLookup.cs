namespace Seamless.FLinq.Linq.Fragments;

/*
ToLookupBase は ToLookup overload 群の Lookup/Grouping 実装と Compute 生成の共通基底。

入力例:
  source.ToLookup(x => x.Category)
  source.ToLookup(x => x.Category, x => x.Value, comparer)

生成例:
  var map = new Dictionary<TKey, List<TElement>>(capacity, comparer);
  var order = new List<TKey>(capacity);
  foreach(var item in source) {
      key = keySelector(item);
      element = elementSelector があれば elementSelector(item) else item;
      if(!map has key) { map[key] = new List<TElement>(); order.Add(key); }
      map[key].Add(element);
  }
  return new TsavoriteLookup(map, order);

設計意図:
  System.Linq.ILookup<TKey,TElement> を返すため、生成コード内に軽量な TsavoriteLookup/TsavoriteGrouping を同時に出す。
  order list を持ち、初出 key の順序で grouping を列挙する。
*/
internal abstract class ToLookupBase(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>string.Empty;
    protected override string StructSources=>source.GetEnumeratorStruct;
    protected override string ComputeMembers=>string.Empty;
    protected override string ComputeExpression=>throw new NotSupportedException();

    protected static string LookupImpl(string elementType,string keyType)=>$$"""
        private sealed class TsavoriteGrouping:global::System.Linq.IGrouping<{{keyType}},{{elementType}}>{
            private readonly {{keyType}} key;
            private readonly global::System.Collections.Generic.List<{{elementType}}> elements;
            public TsavoriteGrouping({{keyType}} key,global::System.Collections.Generic.List<{{elementType}}> elements){this.key=key;this.elements=elements;}
            public {{keyType}} Key=>this.key;
            public global::System.Collections.Generic.IEnumerator<{{elementType}}> GetEnumerator()=>this.elements.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
        }
        private sealed class TsavoriteLookup:global::System.Linq.ILookup<{{keyType}},{{elementType}}>{
            private readonly global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>> map;
            private readonly global::System.Collections.Generic.List<{{keyType}}> order;
            public TsavoriteLookup(global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>> map,global::System.Collections.Generic.List<{{keyType}}> order){this.map=map;this.order=order;}
            public int Count=>this.map.Count;
            public global::System.Collections.Generic.IEnumerable<{{elementType}}> this[{{keyType}} key]=>this.map.TryGetValue(key,out var g)?g:global::System.Linq.Enumerable.Empty<{{elementType}}>();
            public bool Contains({{keyType}} key)=>this.map.ContainsKey(key);
            public global::System.Collections.Generic.IEnumerator<global::System.Linq.IGrouping<{{keyType}},{{elementType}}>> GetEnumerator(){
                foreach(var k in this.order) yield return new TsavoriteGrouping(k,this.map[k]);
            }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
        }
        """;
}

internal class ToLookup_keySelector(int 番号,Enumerable source,FuncFragment keySelector,string resultType):ToLookupBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    public override string StructMethod => this.FuncSupport(keySelector, nameof(keySelector));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(keySelector)})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => $$"""
            {{LookupImpl(source.ElementType,FuncReturnTypeName(keySelector))}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}) {
                var __hasSpan=source.TryGetSpan(out var __span);
                var __capacity=__hasSpan
                    ? __span.Length
                    : source.TryGetNonEnumeratedCount(out var __count) ? __count : 0;
                var map = __capacity>0
                    ? new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>(__capacity)
                    : new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>();
                var order = __capacity>0
                    ? new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>(__capacity)
                    : new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
                if(__hasSpan) {
                    for(var __i=0;__i<__span.Length;__i++) {
                        var {{this.item}} = __span[__i];
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        ref var {{this.group}}=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,{{this.key}},out var __exists);
                        if(!__exists) {
                            {{this.group}} = new global::System.Collections.Generic.List<{{source.ElementType}}>();
                            order.Add({{this.key}});
                        }
                        {{this.group}}.Add({{this.item}});
                    }
                } else {
                    while(source.MoveNext()) {
                        var {{this.item}} = source.Current;
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        ref var {{this.group}}=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,{{this.key}},out var __exists);
                        if(!__exists) {
                            {{this.group}} = new global::System.Collections.Generic.List<{{source.ElementType}}>();
                            order.Add({{this.key}});
                        }
                        {{this.group}}.Add({{this.item}});
                    }
                }
                return new TsavoriteLookup(map, order);
            }
            """;
}

internal class ToLookup_keySelector_elementSelector(int 番号,Enumerable source,FuncFragment keySelector,FuncFragment elementSelector,string resultType):ToLookupBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[0].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[1].Type.TypeKind == TypeKind.Delegate;
    public override string StructMethod => this.FuncSupport(keySelector, nameof(keySelector));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => $$"""
            {{LookupImpl(FuncReturnTypeName(elementSelector),FuncReturnTypeName(keySelector))}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector, elementSelector)}}) {
                var __hasSpan=source.TryGetSpan(out var __span);
                var __capacity=__hasSpan
                    ? __span.Length
                    : source.TryGetNonEnumeratedCount(out var __count) ? __count : 0;
                var map = __capacity>0
                    ? new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>>(__capacity)
                    : new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>>();
                var order = __capacity>0
                    ? new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>(__capacity)
                    : new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
                if(__hasSpan) {
                    for(var __i=0;__i<__span.Length;__i++) {
                        var {{this.item}} = __span[__i];
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        {{FuncReturnTypeName(elementSelector)}} {{this.temp}} = default!;
                        {{this.FuncAssignIndented("            ",elementSelector, nameof(elementSelector),this.temp,this.item)}}
                        ref var {{this.group}}=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,{{this.key}},out var __exists);
                        if(!__exists) {
                            {{this.group}} = new global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>();
                            order.Add({{this.key}});
                        }
                        {{this.group}}.Add({{this.temp}});
                    }
                } else {
                    while(source.MoveNext()) {
                        var {{this.item}} = source.Current;
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        {{FuncReturnTypeName(elementSelector)}} {{this.temp}} = default!;
                        {{this.FuncAssignIndented("            ",elementSelector, nameof(elementSelector),this.temp,this.item)}}
                        ref var {{this.group}}=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,{{this.key}},out var __exists);
                        if(!__exists) {
                            {{this.group}} = new global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>();
                            order.Add({{this.key}});
                        }
                        {{this.group}}.Add({{this.temp}});
                    }
                }
                return new TsavoriteLookup(map, order);
            }
            """;
}

internal class ToLookup_keySelector_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer,string resultType):ToLookupBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[0].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[1].Type.TypeKind != TypeKind.Delegate
        && method.Parameters[1].Name == "comparer";
    public override string StructMethod => this.FuncSupport(keySelector, nameof(keySelector));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{this.ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}, comparer{this.ClosureArgsSuffix(keySelector)})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderComputeInline(destVar,destType,$"{comparer}{this.ClosureArgsSuffix(keySelector)}");
    protected override string ComputeMembers => $$"""
            {{LookupImpl(source.ElementType,FuncReturnTypeName(keySelector))}}
            private static {{resultType}} Compute(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{this.ClosureMethodParameterSuffix(keySelector)}}) {
                var __hasSpan=source.TryGetSpan(out var __span);
                var __capacity=__hasSpan
                    ? __span.Length
                    : source.TryGetNonEnumeratedCount(out var __count) ? __count : 0;
                var map = __capacity>0
                    ? new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>(__capacity,comparer)
                    : new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>(comparer);
                var order = __capacity>0
                    ? new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>(__capacity)
                    : new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
                if(__hasSpan) {
                    for(var __i=0;__i<__span.Length;__i++) {
                        var {{this.item}} = __span[__i];
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        ref var {{this.group}}=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,{{this.key}},out var __exists);
                        if(!__exists) {
                            {{this.group}} = new global::System.Collections.Generic.List<{{source.ElementType}}>();
                            order.Add({{this.key}});
                        }
                        {{this.group}}.Add({{this.item}});
                    }
                } else {
                    while(source.MoveNext()) {
                        var {{this.item}} = source.Current;
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        ref var {{this.group}}=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,{{this.key}},out var __exists);
                        if(!__exists) {
                            {{this.group}} = new global::System.Collections.Generic.List<{{source.ElementType}}>();
                            order.Add({{this.key}});
                        }
                        {{this.group}}.Add({{this.item}});
                    }
                }
                return new TsavoriteLookup(map, order);
            }
            """;
}

internal class ToLookup_keySelector_elementSelector_comparer(int 番号,Enumerable source,FuncFragment keySelector,FuncFragment elementSelector,string comparer,string resultType):ToLookupBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 3
        && method.Parameters[0].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[1].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[2].Name == "comparer";
    public override string StructMethod => this.FuncSupport(keySelector, nameof(keySelector));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{this.ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{this.ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}, comparer{this.ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderComputeInline(destVar,destType,$"{comparer}{this.ClosureArgsSuffix(keySelector, elementSelector)}");
    protected override string ComputeMembers => $$"""
            {{LookupImpl(FuncReturnTypeName(elementSelector),FuncReturnTypeName(keySelector))}}
            private static {{resultType}} Compute(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{this.ClosureMethodParameterSuffix(keySelector, elementSelector)}}) {
                var __hasSpan=source.TryGetSpan(out var __span);
                var __capacity=__hasSpan
                    ? __span.Length
                    : source.TryGetNonEnumeratedCount(out var __count) ? __count : 0;
                var map = __capacity>0
                    ? new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>>(__capacity,comparer)
                    : new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>>(comparer);
                var order = __capacity>0
                    ? new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>(__capacity)
                    : new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
                if(__hasSpan) {
                    for(var __i=0;__i<__span.Length;__i++) {
                        var {{this.item}} = __span[__i];
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        {{FuncReturnTypeName(elementSelector)}} {{this.temp}} = default!;
                        {{this.FuncAssignIndented("            ",elementSelector, nameof(elementSelector),this.temp,this.item)}}
                        ref var __group=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,{{this.key}},out var __exists);
                        if(!__exists) {
                            __group = new global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>();
                            order.Add({{this.key}});
                        }
                        __group.Add({{this.temp}});
                    }
                } else {
                    while(source.MoveNext()) {
                        var {{this.item}} = source.Current;
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        {{FuncReturnTypeName(elementSelector)}} {{this.temp}} = default!;
                        {{this.FuncAssignIndented("            ",elementSelector, nameof(elementSelector),this.temp,this.item)}}
                        ref var __group=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,{{this.key}},out var __exists);
                        if(!__exists) {
                            __group = new global::System.Collections.Generic.List<{{FuncReturnTypeName(elementSelector)}}>();
                            order.Add({{this.key}});
                        }
                        __group.Add({{this.temp}});
                    }
                }
                return new TsavoriteLookup(map, order);
            }
            """;
}

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

    protected static string CapacityExpression(FuncFragment keySelector,string sourceCountExpression) =>
        $"global::System.Math.Min({sourceCountExpression},1024)";
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
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}) {
                var __hasSpan=source.TryGetSpan(out var __span);
                var __capacity=__hasSpan
                    ? {{CapacityExpression(keySelector,"__span.Length")}}
                    : source.TryGetNonEnumeratedCount(out var __count) ? {{CapacityExpression(keySelector,"__count")}} : 0;
                var builder=new global::Seamless.System.Carrier.LookupBuilder<{{FuncReturnTypeName(keySelector)}},{{source.ElementType}}>(__capacity,null);
                try {
                if(__hasSpan) {
                    for(var __i=0;__i<__span.Length;__i++) {
                        var {{this.item}} = __span[__i];
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        builder.Add({{this.key}},{{this.item}});
                    }
                } else {
                    while(source.MoveNext()) {
                        var {{this.item}} = source.Current;
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        builder.Add({{this.key}},{{this.item}});
                    }
                }
                return builder.BuildAndClear();
                } catch {
                    builder.Dispose();
                    throw;
                }
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
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector, elementSelector)}}) {
                var __hasSpan=source.TryGetSpan(out var __span);
                var __capacity=__hasSpan
                    ? {{CapacityExpression(keySelector,"__span.Length")}}
                    : source.TryGetNonEnumeratedCount(out var __count) ? {{CapacityExpression(keySelector,"__count")}} : 0;
                var builder=new global::Seamless.System.Carrier.LookupBuilder<{{FuncReturnTypeName(keySelector)}},{{FuncReturnTypeName(elementSelector)}}>(__capacity,null);
                try {
                if(__hasSpan) {
                    for(var __i=0;__i<__span.Length;__i++) {
                        var {{this.item}} = __span[__i];
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        {{FuncReturnTypeName(elementSelector)}} {{this.temp}} = default!;
                        {{this.FuncAssignIndented("            ",elementSelector, nameof(elementSelector),this.temp,this.item)}}
                        builder.Add({{this.key}},{{this.temp}});
                    }
                } else {
                    while(source.MoveNext()) {
                        var {{this.item}} = source.Current;
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        {{FuncReturnTypeName(elementSelector)}} {{this.temp}} = default!;
                        {{this.FuncAssignIndented("            ",elementSelector, nameof(elementSelector),this.temp,this.item)}}
                        builder.Add({{this.key}},{{this.temp}});
                    }
                }
                return builder.BuildAndClear();
                } catch {
                    builder.Dispose();
                    throw;
                }
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
            private static {{resultType}} Compute(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{this.ClosureMethodParameterSuffix(keySelector)}}) {
                var __hasSpan=source.TryGetSpan(out var __span);
                var __capacity=__hasSpan
                    ? {{CapacityExpression(keySelector,"__span.Length")}}
                    : source.TryGetNonEnumeratedCount(out var __count) ? {{CapacityExpression(keySelector,"__count")}} : 0;
                var builder=new global::Seamless.System.Carrier.LookupBuilder<{{FuncReturnTypeName(keySelector)}},{{source.ElementType}}>(__capacity,comparer);
                try {
                if(__hasSpan) {
                    for(var __i=0;__i<__span.Length;__i++) {
                        var {{this.item}} = __span[__i];
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        builder.Add({{this.key}},{{this.item}});
                    }
                } else {
                    while(source.MoveNext()) {
                        var {{this.item}} = source.Current;
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        builder.Add({{this.key}},{{this.item}});
                    }
                }
                return builder.BuildAndClear();
                } catch {
                    builder.Dispose();
                    throw;
                }
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
            private static {{resultType}} Compute(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{this.ClosureMethodParameterSuffix(keySelector, elementSelector)}}) {
                var __hasSpan=source.TryGetSpan(out var __span);
                var __capacity=__hasSpan
                    ? {{CapacityExpression(keySelector,"__span.Length")}}
                    : source.TryGetNonEnumeratedCount(out var __count) ? {{CapacityExpression(keySelector,"__count")}} : 0;
                var builder=new global::Seamless.System.Carrier.LookupBuilder<{{FuncReturnTypeName(keySelector)}},{{FuncReturnTypeName(elementSelector)}}>(__capacity,comparer);
                try {
                if(__hasSpan) {
                    for(var __i=0;__i<__span.Length;__i++) {
                        var {{this.item}} = __span[__i];
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        {{FuncReturnTypeName(elementSelector)}} {{this.temp}} = default!;
                        {{this.FuncAssignIndented("            ",elementSelector, nameof(elementSelector),this.temp,this.item)}}
                        builder.Add({{this.key}},{{this.temp}});
                    }
                } else {
                    while(source.MoveNext()) {
                        var {{this.item}} = source.Current;
                        {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                        {{this.FuncAssignIndented("            ",keySelector, nameof(keySelector),this.key,this.item)}}
                        {{FuncReturnTypeName(elementSelector)}} {{this.temp}} = default!;
                        {{this.FuncAssignIndented("            ",elementSelector, nameof(elementSelector),this.temp,this.item)}}
                        builder.Add({{this.key}},{{this.temp}});
                    }
                }
                return builder.BuildAndClear();
                } catch {
                    builder.Dispose();
                    throw;
                }
            }
            """;
}

namespace Seamless.SourceGenerator.Linq.Fragments;

public class ToDictionary_keySelector(int 番号, Enumerable source, FuncFragment keySelector, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}) {
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                result=new(__count);
            }else{
                result=new();
            }
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector, nameof(keySelector), key, item)}}
                result.Add({{key}}, {{item}});
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class ToDictionary_keySelector_elementSelector(int 番号, Enumerable source, FuncFragment keySelector, FuncFragment elementSelector, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[0].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[1].Type.TypeKind == TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(keySelector, elementSelector)})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector, elementSelector)}}) {
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                result=new(__count);
            }else{
                result=new();
            }
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector, nameof(keySelector), key, item)}}
                {{FuncReturnTypeName(elementSelector)}} {{temp}} = default!;
                {{FuncAssign(elementSelector, nameof(elementSelector), temp, item)}}
                result.Add({{key}}, {{temp}});
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class ToDictionary_keySelector_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[0].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[1].Type.TypeKind != TypeKind.Delegate
        && method.Parameters[1].Name == "comparer";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}, comparer{ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector)}}) {
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector, nameof(keySelector), key, item)}}
                result.Add({{key}}, {{item}});
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class ToDictionary_keySelector_elementSelector_comparer(int 番号, Enumerable source, FuncFragment keySelector, FuncFragment elementSelector, string comparer, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 3
        && method.Parameters[0].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[1].Type.TypeKind == TypeKind.Delegate
        && method.Parameters[2].Name == "comparer";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}, comparer{ClosureArgsSuffix(keySelector, elementSelector)})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector, elementSelector)}}) {
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector, nameof(keySelector), key, item)}}
                {{FuncReturnTypeName(elementSelector)}} {{temp}} = default!;
                {{FuncAssign(elementSelector, nameof(elementSelector), temp, item)}}
                result.Add({{key}}, {{temp}});
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

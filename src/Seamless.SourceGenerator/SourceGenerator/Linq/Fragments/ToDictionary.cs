namespace Seamless.SourceGenerator.Linq.Fragments;

public abstract class ToDictionaryBase(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>string.Empty;
    protected override string StructSources=>source.GetEnumeratorStruct;
    protected override string GetEnumeratorDisposeBody=>$"{e}.Dispose();";
    protected string ComputeCore(string sourceExpression,string comparerExpression,string keyBody,string valueBody,string ctorArgs){
        return $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ctorArgs}}){
                {{resultType}} result;
                if(source.TryGetNonEnumeratedCount(out var __count)){
                    result=new(__count{{comparerExpression}});
                }else{
                    result=new({{comparerExpression}});
                }
                while(source.MoveNext()){
                    var {{item}}=source.Current;
                    {{keyBody}}
                    {{valueBody}}
                    result.Add({{key}},{{temp}});
                }
                return result;
            }
            """;
    }
}

public class ToDictionary_keySelector(int 番号,Enumerable source,FuncFragment keySelector,string resultType):ToDictionaryBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==1&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate;
    public override string StructMethod=>FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression=>$"Compute({e}{ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}){
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count);
            }else{
                result=new();
            }
            while(source.MoveNext()){
                var {{item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                result.Add({{key}},{{item}});
            }
            return result;
        }
        """;
}

public class ToDictionary_keySelector_elementSelector(int 番号,Enumerable source,FuncFragment keySelector,FuncFragment elementSelector,string resultType):ToDictionaryBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==2&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate&&method.Parameters[1].Type.TypeKind==TypeKind.Delegate;
    public override string StructMethod=>FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector,elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector,elementSelector)}";
    protected override string ComputeExpression=>$"Compute({e}{ClosureArgsSuffix(keySelector,elementSelector)})";
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector,elementSelector)}}){
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count);
            }else{
                result=new();
            }
            while(source.MoveNext()){
                var {{item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                {{FuncReturnTypeName(elementSelector)}} {{temp}}=default!;
                {{FuncAssign(elementSelector,nameof(elementSelector),temp,item)}}
                result.Add({{key}},{{temp}});
            }
            return result;
        }
        """;
}

public class ToDictionary_keySelector_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer,string resultType):ToDictionaryBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==2&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate&&method.Parameters[1].Type.TypeKind!=TypeKind.Delegate&&method.Parameters[1].Name=="comparer";
    public override string StructMethod=>FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {e},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression=>$"Compute({e}, comparer{ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector)}}){
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            while(source.MoveNext()){
                var {{item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                result.Add({{key}},{{item}});
            }
            return result;
        }
        """;
}

public class ToDictionary_keySelector_elementSelector_comparer(int 番号,Enumerable source,FuncFragment keySelector,FuncFragment elementSelector,string comparer,string resultType):ToDictionaryBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==3&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate&&method.Parameters[1].Type.TypeKind==TypeKind.Delegate&&method.Parameters[2].Name=="comparer";
    public override string StructMethod=>FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector,elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {e},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector,elementSelector)}";
    protected override string ComputeExpression=>$"Compute({e}, comparer{ClosureArgsSuffix(keySelector,elementSelector)})";
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector,elementSelector)}}){
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            while(source.MoveNext()){
                var {{item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                {{FuncReturnTypeName(elementSelector)}} {{temp}}=default!;
                {{FuncAssign(elementSelector,nameof(elementSelector),temp,item)}}
                result.Add({{key}},{{temp}});
            }
            return result;
        }
        """;
}

namespace Seamless.SourceGenerator.Linq.Fragments;

public abstract class ToDictionaryBase(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>string.Empty;
    protected override string StructSources=>source.GetEnumeratorStruct;
    protected override string GetEnumeratorDisposeBody=>$"{this.e}.Dispose();";
}

public class ToDictionary_keySelector(int 番号,Enumerable source,FuncFragment keySelector,string resultType):ToDictionaryBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==1&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate;
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression=>$"Compute({this.e}{this.ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count);
            }else{
                result=new();
            }
            while(source.MoveNext()){
                var {{this.item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                result.Add({{this.key}},{{this.item}});
            }
            return result;
        }
        """;
}

public class ToDictionary_keySelector_elementSelector(int 番号,Enumerable source,FuncFragment keySelector,FuncFragment elementSelector,string resultType):ToDictionaryBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==2&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate&&method.Parameters[1].Type.TypeKind==TypeKind.Delegate;
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector,elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector,elementSelector)}";
    protected override string ComputeExpression=>$"Compute({this.e}{this.ClosureArgsSuffix(keySelector,elementSelector)})";
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector,elementSelector)}}){
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count);
            }else{
                result=new();
            }
            while(source.MoveNext()){
                var {{this.item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                {{FuncReturnTypeName(elementSelector)}} {{this.temp}}=default!;
                {{this.FuncAssign(elementSelector,nameof(elementSelector),this.temp,this.item)}}
                result.Add({{this.key}},{{this.temp}});
            }
            return result;
        }
        """;
}

public class ToDictionary_keySelector_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer,string resultType):ToDictionaryBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==2&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate&&method.Parameters[1].Type.TypeKind!=TypeKind.Delegate&&method.Parameters[1].Name=="comparer";
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{this.ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression=>$"Compute({this.e}, comparer{this.ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{this.ClosureMethodParameterSuffix(keySelector)}}){
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            while(source.MoveNext()){
                var {{this.item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                result.Add({{this.key}},{{this.item}});
            }
            return result;
        }
        """;
}

public class ToDictionary_keySelector_elementSelector_comparer(int 番号,Enumerable source,FuncFragment keySelector,FuncFragment elementSelector,string comparer,string resultType):ToDictionaryBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==3&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate&&method.Parameters[1].Type.TypeKind==TypeKind.Delegate&&method.Parameters[2].Name=="comparer";
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{this.ClosureArgsSuffix(keySelector,elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{this.ClosurePrimaryCtorSuffix(keySelector,elementSelector)}";
    protected override string ComputeExpression=>$"Compute({this.e}, comparer{this.ClosureArgsSuffix(keySelector,elementSelector)})";
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{this.ClosureMethodParameterSuffix(keySelector,elementSelector)}}){
            {{resultType}} result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            while(source.MoveNext()){
                var {{this.item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                {{FuncReturnTypeName(elementSelector)}} {{this.temp}}=default!;
                {{this.FuncAssign(elementSelector,nameof(elementSelector),this.temp,this.item)}}
                result.Add({{this.key}},{{this.temp}});
            }
            return result;
        }
        """;
}

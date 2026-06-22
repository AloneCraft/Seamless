namespace Seamless.FLinq.Linq.Fragments;

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
    public override string? RenderInlineComputation(string destVar,string destType)=>
        this.RenderRangeIdentityToDictionaryInline(destVar,destType)
        ?? (TryRenderDirectLambda(keySelector) is { } keyLambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"ToDictionary",keyLambda)
            : this.RenderComputeInline(destVar,destType));
    private string? RenderRangeIdentityToDictionaryInline(string destVar,string destType) {
        if(source is not RangeRoot range
            || source.ElementType!="int"
            || !keySelector.IsIdentity
            || FuncReturnTypeName(keySelector)!=source.ElementType) {
            return null;
        }

        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var index=$"__i{番号}";
        var value=$"__value{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                {{destVar}}=new({{count}});
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {
                    var {{value}}={{start}} + {{index}};
                    {{destVar}}.Add({{value}},{{value}});
                }
            }
            """;
    }
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
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
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
    public override string? RenderInlineComputation(string destVar,string destType)=>
        this.RenderRangeIdentityToDictionaryInline(destVar,destType)
        ?? (TryRenderDirectLambda(keySelector) is { } keyLambda
        && TryRenderDirectLambda(elementSelector) is { } elementLambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"ToDictionary",keyLambda,elementLambda)
            : this.RenderComputeInline(destVar,destType));
    private string? RenderRangeIdentityToDictionaryInline(string destVar,string destType) {
        if(source is not RangeRoot range
            || source.ElementType!="int"
            || !keySelector.IsIdentity
            || FuncReturnTypeName(keySelector)!=source.ElementType
            || !elementSelector.CanInlineAssign
            || elementSelector.RequiresStatementInlining) {
            return null;
        }

        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var index=$"__i{番号}";
        var value=$"__value{番号}";
        var element=$"__element{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                {{destVar}}=new({{count}});
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {
                    var {{value}}={{start}} + {{index}};
                    {{FuncReturnTypeName(elementSelector)}} {{element}}=default!;
            {{IndentLines(this.FuncAssign(elementSelector,nameof(elementSelector),element,value),"        ")}}
                    {{destVar}}.Add({{value}},{{element}});
                }
            }
            """;
    }
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
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
                {{FuncReturnTypeName(elementSelector)}} {{this.temp}}=default!;
                {{this.FuncAssignIndented("        ",elementSelector,nameof(elementSelector),this.temp,this.item)}}
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
    public override string? RenderInlineComputation(string destVar,string destType)=>
        this.RenderRangeIdentityToDictionaryInline(destVar,destType)
        ?? (TryRenderDirectLambda(keySelector) is { } keyLambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"ToDictionary",keyLambda,comparer)
            : this.RenderComputeInline(destVar,destType));
    private string? RenderRangeIdentityToDictionaryInline(string destVar,string destType) {
        if(source is not RangeRoot range
            || source.ElementType!="int"
            || !keySelector.IsIdentity
            || FuncReturnTypeName(keySelector)!=source.ElementType) {
            return null;
        }

        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var comparerVar=$"__comparer{番号}";
        var index=$"__i{番号}";
        var value=$"__value{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{comparerVar}}={{comparer}};
                {{destVar}}=new({{count}},{{comparerVar}});
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {
                    var {{value}}={{start}} + {{index}};
                    {{destVar}}.Add({{value}},{{value}});
                }
            }
            """;
    }
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
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
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
    public override string? RenderInlineComputation(string destVar,string destType)=>
        TryRenderDirectLambda(keySelector) is { } keyLambda
        && TryRenderDirectLambda(elementSelector) is { } elementLambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"ToDictionary",keyLambda,elementLambda,comparer)
            : this.RenderComputeInline(destVar,destType);
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
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
                {{FuncReturnTypeName(elementSelector)}} {{this.temp}}=default!;
                {{this.FuncAssignIndented("        ",elementSelector,nameof(elementSelector),this.temp,this.item)}}
                result.Add({{this.key}},{{this.temp}});
            }
            return result;
        }
        """;
}

namespace Seamless.FLinq.Linq.Fragments;

/*
ToDictionaryBase は ToDictionary overload 群の direct enumerable fast path と共通設定を持つ基底。

入力例:
  source.ToDictionary(x => x.Id)
  source.ToDictionary(x => x.Id, x => x.Name, comparer)

生成例:
  dictionary = capacity が分かれば new(count, comparer) する;
  foreach(var item in source) {
      key = keySelector(item);
      value = elementSelector があれば elementSelector(item) else item;
      dictionary.Add(key, value);
  }

バリエーション:
  - array/list/ICollection 由来なら sourceExpression を直接 pattern match して capacity と span を使う。
  - keySelector が identity の RangeRoot<int> は key/value を start+i で直接作る。
  - comparer overload は Dictionary constructor と Compute 引数へ comparer を追加する。
*/
public abstract class ToDictionaryBase(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>string.Empty;
    protected override string StructSources=>source.GetEnumeratorStruct;
    protected override string GetEnumeratorDisposeBody=>$"{this.e}.Dispose();";

    protected string? RenderDirectEnumerableToDictionaryHybridInline(
        string destVar,
        string destType,
        FuncFragment keySelector,
        FuncFragment? elementSelector,
        string? comparerExpression=null) {
        if(source.DirectEnumerableExpression is not { } sourceExpression) return null;
        if(TryRenderDirectLambda(keySelector) is not { } keyLambda) return null;
        var elementLambda = elementSelector is null ? null : TryRenderDirectLambda(elementSelector);
        if(elementSelector is not null && elementLambda is null) return null;

        var sourceType = source.ElementType;
        var keyType = FuncReturnTypeName(keySelector);
        var valueType = elementSelector is null ? source.ElementType : FuncReturnTypeName(elementSelector);
        var comparer = comparerExpression ?? "null";
        var sourceVar=$"__src{this.result}";
        var arrayVar=$"__array{this.result}";
        var listVar=$"__list{this.result}";
        var collectionVar=$"__collection{this.result}";
        var spanVar=$"__span{this.result}";
        var itemVar=$"__item{this.result}";
        var keySelectorVar=$"__keySelector{this.result}";
        var elementSelectorVar=$"__elementSelector{this.result}";
        var addValue = elementSelector is null ? itemVar : $"{elementSelectorVar}({itemVar})";
        var elementSelectorDeclaration = elementSelector is null
            ? string.Empty
            : $"global::System.Func<{sourceType},{valueType}> {elementSelectorVar}={elementLambda};";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{sourceVar}}={{sourceExpression}};
                global::System.Func<{{sourceType}},{{keyType}}> {{keySelectorVar}}={{keyLambda}};
                {{elementSelectorDeclaration}}
                if({{sourceVar}} is {{sourceType}}[] {{arrayVar}}) {
                    {{destVar}}=new({{arrayVar}}.Length,{{comparer}});
                    var {{spanVar}}=global::System.MemoryExtensions.AsSpan({{arrayVar}});
                    for(var __i=0; __i<{{spanVar}}.Length; __i++) {
                        var {{itemVar}}={{spanVar}}[__i];
                        {{destVar}}.Add({{keySelectorVar}}({{itemVar}}),{{addValue}});
                    }
                } else if({{sourceVar}} is global::System.Collections.Generic.List<{{sourceType}}> {{listVar}}) {
                    var {{spanVar}}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{listVar}});
                    {{destVar}}=new({{spanVar}}.Length,{{comparer}});
                    for(var __i=0; __i<{{spanVar}}.Length; __i++) {
                        var {{itemVar}}={{spanVar}}[__i];
                        {{destVar}}.Add({{keySelectorVar}}({{itemVar}}),{{addValue}});
                    }
                } else if({{sourceVar}} is global::System.Collections.Generic.ICollection<{{sourceType}}> {{collectionVar}}) {
                    {{destVar}}=new({{collectionVar}}.Count,{{comparer}});
                    foreach(var {{itemVar}} in {{collectionVar}}) {
                        {{destVar}}.Add({{keySelectorVar}}({{itemVar}}),{{addValue}});
                    }
                } else {
                    {{destVar}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{valueType}}>({{comparer}});
                    foreach(var {{itemVar}} in {{sourceVar}}) {
                        {{destVar}}.Add({{keySelectorVar}}({{itemVar}}),{{addValue}});
                    }
                }
            }
            """;
    }
}

public class ToDictionary_keySelector(int 番号,Enumerable source,FuncFragment keySelector,string resultType):ToDictionaryBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==1&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate;
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression=>$"Compute({this.e}{this.ClosureArgsSuffix(keySelector)})";
    public override string? RenderInlineComputation(string destVar,string destType)=>
        this.RenderRangeIdentityToDictionaryInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType);
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
            var __hasSpan=source.TryGetSpan(out var __span);
            if(__hasSpan){
                result=new(__span.Length);
                for(var __i=0; __i<__span.Length; __i++){
                    var {{this.item}}=__span[__i];
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    result.Add({{this.key}},{{this.item}});
                }
            }else if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count);
            }else{
                result=new();
            }
            if(!__hasSpan){
                while(source.MoveNext()){
                    var {{this.item}}=source.Current;
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    result.Add({{this.key}},{{this.item}});
                }
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
        ??this.RenderComputeInline(destVar,destType);
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
            var __hasSpan=source.TryGetSpan(out var __span);
            if(__hasSpan){
                result=new(__span.Length);
                for(var __i=0; __i<__span.Length; __i++){
                    var {{this.item}}=__span[__i];
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    {{FuncReturnTypeName(elementSelector)}} {{this.temp}}=default!;
                    {{this.FuncAssignIndented("            ",elementSelector,nameof(elementSelector),this.temp,this.item)}}
                    result.Add({{this.key}},{{this.temp}});
                }
            }else if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count);
            }else{
                result=new();
            }
            if(!__hasSpan){
                while(source.MoveNext()){
                    var {{this.item}}=source.Current;
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    {{FuncReturnTypeName(elementSelector)}} {{this.temp}}=default!;
                    {{this.FuncAssignIndented("            ",elementSelector,nameof(elementSelector),this.temp,this.item)}}
                    result.Add({{this.key}},{{this.temp}});
                }
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
        ??this.RenderComputeInline(destVar,destType,$"{comparer}{this.ClosureArgsSuffix(keySelector)}");
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
            var __hasSpan=source.TryGetSpan(out var __span);
            if(__hasSpan){
                result=new(__span.Length,comparer);
                for(var __i=0; __i<__span.Length; __i++){
                    var {{this.item}}=__span[__i];
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    result.Add({{this.key}},{{this.item}});
                }
            }else if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            if(!__hasSpan){
                while(source.MoveNext()){
                    var {{this.item}}=source.Current;
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    result.Add({{this.key}},{{this.item}});
                }
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
        this.RenderComputeInline(destVar,destType,$"{comparer}{this.ClosureArgsSuffix(keySelector,elementSelector)}");
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{this.ClosureMethodParameterSuffix(keySelector,elementSelector)}}){
            {{resultType}} result;
            var __hasSpan=source.TryGetSpan(out var __span);
            if(__hasSpan){
                result=new(__span.Length,comparer);
                for(var __i=0; __i<__span.Length; __i++){
                    var {{this.item}}=__span[__i];
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    {{FuncReturnTypeName(elementSelector)}} {{this.temp}}=default!;
                    {{this.FuncAssignIndented("            ",elementSelector,nameof(elementSelector),this.temp,this.item)}}
                    result.Add({{this.key}},{{this.temp}});
                }
            }else if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            if(!__hasSpan){
                while(source.MoveNext()){
                    var {{this.item}}=source.Current;
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    {{FuncReturnTypeName(elementSelector)}} {{this.temp}}=default!;
                    {{this.FuncAssignIndented("            ",elementSelector,nameof(elementSelector),this.temp,this.item)}}
                    result.Add({{this.key}},{{this.temp}});
                }
            }
            return result;
        }
        """;
}

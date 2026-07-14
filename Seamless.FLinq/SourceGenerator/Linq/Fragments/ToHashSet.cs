namespace Seamless.FLinq.Linq.Fragments;

/*
ToHashSetBase は ToHashSet overload 群の共通基底。

入力例:
  source.ToHashSet()
  source.ToHashSet(comparer)

生成例:
  result = source.TryGetNonEnumeratedCount(out count)
      ? new HashSet<T>(count, comparer)
      : new HashSet<T>(comparer);
  foreach(var item in spanOrEnumerator) result.Add(item);

バリエーション:
  - comparer なしは既定比較器。
  - comparer ありは constructor/Compute に comparer を渡す。
  - RangeRoot<int> は start+i を直接 Add する inline を生成する。
*/
public abstract class ToHashSetBase(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}

public class ToHashSet(int 番号,Enumerable source,string resultType):ToHashSetBase(番号,source,resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length==0;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeToHashSetInline(destVar,destType)
        ??this.RenderDirectSpanToHashSetInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanToHashSetInline(string destVar,string destType) {
        var sourceName = $"__hashSetSource{番号}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }
        var spanName = $"__hashSetSpan{番号}";
        return $$"""
            {{destType}} {{destVar}};
            using(var {{sourceName}}={{source.GetEnumeratorExpression}}) {
                var {{spanName}}={{directSpanExpression}};
                {{destVar}}=new({{spanName}}.Length);
                for(var __i{{番号}}=0;__i{{番号}}<{{spanName}}.Length;__i{{番号}}++) {
                    {{destVar}}.Add({{spanName}}[__i{{番号}}]);
                }
            }
            """;
    }
    private string? RenderRangeToHashSetInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }

        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var index = $"__i{番号}";
        var value = $"__value{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                {{destVar}}=new({{count}});
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {
                    var {{value}}={{start}} + {{index}};
                    {{destVar}}.Add({{value}});
                }
            }
            """;
    }
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source){
            global::System.Collections.Generic.HashSet<{{source.ElementType}}> result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count);
            }else{
                result=new();
            }
            if(source.TryGetSpan(out var __span)){
                foreach(var item in __span) result.Add(item);
            }else{
                while(source.MoveNext()) result.Add(source.Current);
            }
            return result;
        }
        """;
}

public class ToHashSet_comparer(int 番号,Enumerable source,string comparer,string resultType):ToHashSetBase(番号,source,resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==1&&method.Parameters[0].Name=="comparer";
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression},{comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => $"Compute({this.e},comparer)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeToHashSetInline(destVar,destType)
        ??this.RenderDirectSpanToHashSetInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,comparer);
    private string? RenderDirectSpanToHashSetInline(string destVar,string destType) {
        var sourceName = $"__hashSetSource{番号}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }
        var spanName = $"__hashSetSpan{番号}";
        var comparerVar = $"__comparer{番号}";
        return $$"""
            {{destType}} {{destVar}};
            using(var {{sourceName}}={{source.GetEnumeratorExpression}}) {
                var {{spanName}}={{directSpanExpression}};
                var {{comparerVar}}={{comparer}};
                {{destVar}}=new({{spanName}}.Length,{{comparerVar}});
                for(var __i{{番号}}=0;__i{{番号}}<{{spanName}}.Length;__i{{番号}}++) {
                    {{destVar}}.Add({{spanName}}[__i{{番号}}]);
                }
            }
            """;
    }
    private string? RenderRangeToHashSetInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }

        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var comparerVar = $"__comparer{番号}";
        var index = $"__i{番号}";
        var value = $"__value{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{comparerVar}}={{comparer}};
                {{destVar}}=new({{count}},{{comparerVar}});
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {
                    var {{value}}={{start}} + {{index}};
                    {{destVar}}.Add({{value}});
                }
            }
            """;
    }
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
            global::System.Collections.Generic.HashSet<{{source.ElementType}}> result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            if(source.TryGetSpan(out var __span)){
                foreach(var item in __span) result.Add(item);
            }else{
                while(source.MoveNext()) result.Add(source.Current);
            }
            return result;
        }
        """;
}

namespace Seamless.FLinq;

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
internal abstract class ToHashSetBase(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    protected override string GetEnumeratorDisposeBody => string.Empty;
    // ToHashSet には inline 経路(RenderInlineComputation)と struct 経路(ComputeMembers)の2つがあり、
    // `.ToHashSet().Consume(...)` のように結果をさらに列挙する形では struct 経路になる。
    // inline 経路には RenderProviderNativeToHashSetInline で事前サイズ決定があるのに struct 経路には
    // 無く、生成 struct の TryGetNonEnumeratedCount(どのソースでも安全な汎用実装なので常に false)へ
    // 頼っていたため HashSet<T> が空から成長し、再ハッシュ分だけ余分に確保していた
    // (実測: Persisted<int> 4096要素で 1.86x、Persisted<ClassInt32> で 1.69x 多い)。
    // Persisted/Tsavorite の根は Count プロパティで O(1) の件数を持つので、それを
    // コンストラクタ引数として struct へ渡して両経路を揃える。
    // 対象を ProviderNativeRootBase に限るのは、根の式を2回評価する形が既に inline 経路で
    // 許容されている範囲に留め、他の根種へ新たな二重評価を持ち込まないため。
    protected string? ProviderNativeCountExpression {
        get {
            if(source is not ProviderNativeRootBase nativeRoot) {
                return null;
            }
            return $"checked((int)({nativeRoot.NativeCountExpression}))";
        }
    }
    protected string PresetCountName => $"__presetCount{番号}";
}

internal class ToHashSet(int 番号,Enumerable source,string resultType):ToHashSetBase(番号,source,resultType) {
    public static bool IsApplicableVariant(IMethodSymbol method) => method.Parameters.Length==0;
    public override string GetEnumeratorExpression {
        get {
            if(this.ProviderNativeCountExpression is { } countExpression) {
                return RenderEnumeratorExpressionSeparated(this.EnumeratorTypeName,",",source.GetEnumeratorExpression,countExpression);
            }
            return RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
        }
    }
    public override string GetEnumeratorPrimaryConstructorParameters {
        get {
            if(this.ProviderNativeCountExpression is not null) {
                return $"TEnum {this.e},int {this.PresetCountName}";
            }
            return $"TEnum {this.e}";
        }
    }
    protected override string ComputeExpression {
        get {
            if(this.ProviderNativeCountExpression is not null) {
                return $"Compute({this.e},{this.PresetCountName})";
            }
            return $"Compute({this.e})";
        }
    }
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeToHashSetInline(destVar,destType)
        ??this.RenderDirectSpanToHashSetInline(destVar,destType)
        ??this.RenderProviderNativeToHashSetInline(destVar,destType)
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
            using(var {{sourceName}}={{source.GetEnumeratorExpression}}){
                var {{spanName}}={{directSpanExpression}};
                {{destVar}}=new({{spanName}}.Length);
                for(var __i{{番号}}=0;__i{{番号}}<{{spanName}}.Length;__i{{番号}}++){
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
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++){
                    var {{value}}={{start}} + {{index}};
                    {{destVar}}.Add({{value}});
                }
            }
            """;
    }
    // Persisted/Tsavorite の根(ProviderNativeRootBase)は Count プロパティで O(1) の件数を持つが、
    // 生成 struct の TryGetNonEnumeratedCount はどのルートでも使える汎用実装のため、この能力を知らない。
    // ComputeMembers の TryGetNonEnumeratedCount 経由に任せると事前サイズ決定に失敗し、HashSet<T> が
    // 空から成長する分だけ余分に確保する(実測: Persisted<int> 2000要素で 1.85x 多く割り当てていた)。
    // ここで NativeCountExpression を直接埋め込み、System.Linq への委譲ではなく FLinq 自身が
    // 事前サイズ決定込みの inline を生成する。
    private string? RenderProviderNativeToHashSetInline(string destVar,string destType) {
        if(source is not ProviderNativeRootBase nativeRoot) {
            return null;
        }
        var sourceName = $"__hashSetSource{番号}";
        var countVar = $"__hashSetCount{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{countVar}}=checked((int)({{nativeRoot.NativeCountExpression}}));
                {{destVar}}=new({{countVar}});
                using(var {{sourceName}}={{source.GetEnumeratorExpression}}){
                    while({{sourceName}}.TryGetNext(out var __item{{番号}})) {{destVar}}.Add(__item{{番号}});
                }
            }
            """;
    }
    protected override string ComputeMembers {
        get {
            if(this.ProviderNativeCountExpression is not null) {
                return $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source,int {{this.PresetCountName}}){
                        var result=new global::System.Collections.Generic.HashSet<{{source.ElementType}}>({{this.PresetCountName}});
                        while(source.TryGetNext(out var __mn86)) result.Add(__mn86);
                        return result;
                    }
                    """;
            }
            return $$"""
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
                        while(source.TryGetNext(out var __mn86)) result.Add(__mn86);
                    }
                    return result;
                }
                """;
        }
    }
}

internal class ToHashSet_comparer(int 番号,Enumerable source,string comparer,string resultType):ToHashSetBase(番号,source,resultType) {
    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==1&&method.Parameters[0].Name=="comparer";
    public override string GetEnumeratorExpression {
        get {
            if(this.ProviderNativeCountExpression is { } countExpression) {
                return RenderEnumeratorExpressionSeparated(this.EnumeratorTypeName,",",source.GetEnumeratorExpression,comparer,countExpression);
            }
            return RenderEnumeratorExpressionSeparated(this.EnumeratorTypeName,",",source.GetEnumeratorExpression,comparer);
        }
    }
    public override string GetEnumeratorPrimaryConstructorParameters {
        get {
            if(this.ProviderNativeCountExpression is not null) {
                return $"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer,int {this.PresetCountName}";
            }
            return $"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
        }
    }
    protected override string ComputeExpression {
        get {
            if(this.ProviderNativeCountExpression is not null) {
                return $"Compute({this.e},comparer,{this.PresetCountName})";
            }
            return $"Compute({this.e},comparer)";
        }
    }
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeToHashSetInline(destVar,destType)
        ??this.RenderDirectSpanToHashSetInline(destVar,destType)
        ??this.RenderProviderNativeToHashSetInline(destVar,destType)
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
            using(var {{sourceName}}={{source.GetEnumeratorExpression}}){
                var {{spanName}}={{directSpanExpression}};
                var {{comparerVar}}={{comparer}};
                {{destVar}}=new({{spanName}}.Length,{{comparerVar}});
                for(var __i{{番号}}=0;__i{{番号}}<{{spanName}}.Length;__i{{番号}}++){
                    {{destVar}}.Add({{spanName}}[__i{{番号}}]);
                }
            }
            """;
    }
    // ToHashSet(コメント参照)と同じ理由。comparer 付きは HashSet<T>(int,comparer) を直接呼ぶ。
    private string? RenderProviderNativeToHashSetInline(string destVar,string destType) {
        if(source is not ProviderNativeRootBase nativeRoot) {
            return null;
        }
        var sourceName = $"__hashSetSource{番号}";
        var countVar = $"__hashSetCount{番号}";
        var comparerVar = $"__comparer{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{countVar}}=checked((int)({{nativeRoot.NativeCountExpression}}));
                var {{comparerVar}}={{comparer}};
                {{destVar}}=new({{countVar}},{{comparerVar}});
                using(var {{sourceName}}={{source.GetEnumeratorExpression}}){
                    while({{sourceName}}.TryGetNext(out var __item{{番号}})) {{destVar}}.Add(__item{{番号}});
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
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++){
                    var {{value}}={{start}} + {{index}};
                    {{destVar}}.Add({{value}});
                }
            }
            """;
    }
    protected override string ComputeMembers {
        get {
            if(this.ProviderNativeCountExpression is not null) {
                return $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer,int {{this.PresetCountName}}){
                        var result=new global::System.Collections.Generic.HashSet<{{source.ElementType}}>({{this.PresetCountName}},comparer);
                        while(source.TryGetNext(out var __mn87)) result.Add(__mn87);
                        return result;
                    }
                    """;
            }
            return $$"""
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
                        while(source.TryGetNext(out var __mn87)) result.Add(__mn87);
                    }
                    return result;
                }
                """;
        }
    }
}

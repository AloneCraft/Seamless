using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

internal class OrderBy(int 番号,Enumerable source,FuncFragment keySelector):OrderBy_comparer(番号,source,keySelector,"null") { }
internal class OrderByDescending(int 番号,Enumerable source,FuncFragment keySelector):OrderByDescending_comparer(番号,source,keySelector,"null") { }

internal class OrderBy_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer):OrderingBase(番号,source,keySelector,comparer,descending: false) { }
internal class OrderByDescending_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer):OrderingBase(番号,source,keySelector,comparer,descending: true) { }

/*
OrderingBase は OrderBy/ThenBy 系を、全件 buffer + stable sort + buffer enumerator に変換する。

入力例:
  source.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)

生成例:
  var items = new List<SortItem>();
  foreach(var item in source) {
      items.Add(new SortItem(item, keySelector(item), originalIndex++));
  }
  items.Sort((left, right) => CompareKeysThenOriginalIndex(left, right));
  return items.Select(x => x.Value).ToList();

バリエーション:
  - descending=true なら比較結果の符号を反転する。
  - source が既に OrderingBase の場合、一次 key と二次 key を SortItem に同居させる。
  - originalIndex を最後に比較し、System.Linq と同じ安定ソートの結果にする。
  - MaxTakeCountExpression があり、かつ k <= 64 の場合は、全件バッファリングを完全にバイパスし、
    TopKBuffer / TopKCombinedBuffer によるストリーミング Top-K 抽出（ゼロ全件確保・最小バッファ）を行う。
*/
internal abstract class OrderingBase(int 番号,Enumerable source,FuncFragment keySelector,string comparer,bool descending,bool isThenBy = false,bool evaluateSingletonKeys=false):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(keySelector is { } __visitorChild_keySelector) {
            yield return __visitorChild_keySelector;
        }
    }

    // 同じordered sourceをmaterializeする利用者へFirstの需要を漏らさない。
    internal static Enumerable BindFirstConsumer(Enumerable source){
        if(source is not OrderingBase ordering){return source;}
        // 二重OrderByの内側は列挙される。ThenByの同一sort連鎖には需要を伝播しない。
        var boundSource=ordering.OwnSource;
        if(!ordering.OwnIsThenBy){
            boundSource=BindFirstConsumer(boundSource);
        }
        var bound=new FirstConsumerOrdering(ordering.FragmentIndex,boundSource,ordering.OwnKeySelector,
            ordering.OwnRawComparer,ordering.OwnDescending,ordering.OwnIsThenBy){
            MaxTakeCountExpression=ordering.MaxTakeCountExpression,
            SourceContainsAnchor=ordering.SourceContainsAnchor
        };
        bound.ApplyRenderContext(ordering.RenderContext);
        return bound;
    }
    // 返却queryは未開始の値型列挙子を保持し、既存の遅延sortを列挙ごとに使う。
    // 一段のarray OrderByだけを対象とし、融合・最適化の設定は変更しない。
    internal PipelineEnumerable BindEscapingArrayConsumer(){
        var kind=PipelineStageKind.OrderBy;
        if(this.OwnDescending){kind=PipelineStageKind.OrderByDescending;}
        var stage=new PipelineStage(kind,this.OwnKeySelector,this.OwnRawComparer,this.ElementType,this.ElementType,
            keyType:FuncReturnTypeName(this.OwnKeySelector),descending:this.OwnDescending);
        var pipeline=new PipelineEnumerable(this.FragmentIndex,this.OwnSource,[stage],this.ElementType){
            SourceContainsAnchor=this.SourceContainsAnchor
        };
        pipeline.ApplyRenderContext(this.RenderContext);
        return pipeline;
    }
    private sealed class FirstConsumerOrdering(int number,Enumerable source,FuncFragment selector,string comparer,bool descending,bool isThenBy)
        :OrderingBase(number,source,selector,comparer,descending,isThenBy,evaluateSingletonKeys:true){}
    private string SingletonKeyParameter=>GeneratedNames.Current?.StableLocal($"OrderingSingleton:{this.FragmentIndex}",$"__evaluateSingletonOrderKeys{this.result}")??$"__evaluateSingletonOrderKeys{this.result}";
    private string SingletonKeyArgument=>evaluateSingletonKeys.ToString().ToLowerInvariant();

    // チェーン（ThenBy）検出用に一次ソースを公開
    internal FuncFragment OwnKeySelector => keySelector;
    internal string OwnRawComparer => comparer;
    internal string OwnComparer => ComparerExpressionSupport.RenderOrderComparer(comparer,FuncReturnTypeName(keySelector));
    internal bool OwnDescending => descending;
    internal Enumerable OwnSource => source;
    internal string? MaxTakeCountExpression { get; set; }

    internal bool OwnIsThenBy => isThenBy;

    private static bool CanUseDirectDefaultCompare(string comparerExpression,string keyType) =>
        ComparerExpressionSupport.IsDefaultOrderComparer(comparerExpression,keyType)
        &&(keyType is "byte" or "sbyte"
            or "short" or "ushort"
            or "int" or "uint"
            or "long" or "ulong"
            or "float" or "double" or "decimal"
            or "char" or "string"
            or "global::System.Byte" or "global::System.SByte"
            or "global::System.Int16" or "global::System.UInt16"
            or "global::System.Int32" or "global::System.UInt32"
            or "global::System.Int64" or "global::System.UInt64"
            or "global::System.Single" or "global::System.Double" or "global::System.Decimal"
            or "global::System.Char" or "global::System.String");

    // ThenBy のとき、直前の順序段(OrderBy/ThenBy)を取得する。一次キーと二次キーを 1 回の並べ替えにまとめるための情報。
    // OrderBy が OrderBy の後に来るのは ThenBy ではない: 後の OrderBy が主キーで、前の順序は「キーが等しいときの並び」として
    // 残る(LINQ は安定ソート)。これを ThenBy と同じにまとめると主キーと副キーが逆になるので、通常のソースとして並べ替える。
    private OrderingBase? PrimaryOrdering => isThenBy ? source as OrderingBase : null;
    // ソート前の元データソース。ThenBy は、遡った先の最初の OrderBy(主キー)の入力までを 1 回の並べ替えにまとめる。
    private Enumerable UnorderedSource {
        get {
            if(!isThenBy) {
                return source;
            }
            Enumerable s = source;
            while(s is OrderingBase oo) {
                s=oo.OwnSource;
                if(!oo.OwnIsThenBy) {
                    break;
                }
            }
            return s;
        }
    }

    protected override Enumerable PrimarySource => this.UnorderedSource;
    public override string ElementType => this.UnorderedSource.ElementType;
    public override string? NonEnumeratedCountExpression => this.UnorderedSource.NonEnumeratedCountExpression;

    public override bool CanProvideSpan => this.MaxTakeCountExpression is not null || base.CanProvideSpan;
    public override bool CanTryProvideSpan => this.MaxTakeCountExpression is not null || base.CanTryProvideSpan;

    public override string StructMethod =>
        this.PrimaryOrdering is { } po
            ? JoinStaticMethods(this.FuncSupport(keySelector,nameof(keySelector)),this.FuncSupport(po.OwnKeySelector,"primaryKeySelector"))
            : this.FuncSupport(keySelector,nameof(keySelector));

    public override string GetEnumeratorStruct => $$"""
        {{this.UnorderedSource.GetEnumeratorStruct}}
        {{this.DefineEnumeratorStructCore(this.ElementType,this.GetEnumeratorDisposeBody)}}
        """;

    public override string GetEnumeratorExpression =>
        this.PrimaryOrdering is { } po
            ? (this.MaxTakeCountExpression is { } maxTakeCombined
                ? RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector,po.OwnKeySelector },this.UnorderedSource.GetEnumeratorExpression,this.OwnComparer,po.OwnComparer,this.SingletonKeyArgument,maxTakeCombined)
                : RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector,po.OwnKeySelector },this.UnorderedSource.GetEnumeratorExpression,this.OwnComparer,po.OwnComparer,this.SingletonKeyArgument))
            : (this.MaxTakeCountExpression is { } maxTakeSingle
                ? RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector },source.GetEnumeratorExpression,this.OwnComparer,this.SingletonKeyArgument,maxTakeSingle)
                : RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector },source.GetEnumeratorExpression,this.OwnComparer,this.SingletonKeyArgument));

    private string MaxTakeCtorParamSuffix => this.MaxTakeCountExpression is not null ? ", int __maxTakeCount" : string.Empty;
    private string MaxTakeArgSuffix => this.MaxTakeCountExpression is not null ? ",__maxTakeCount" : string.Empty;

    public override string GetEnumeratorPrimaryConstructorParameters =>
        this.PrimaryOrdering is { } po
            ? $"TEnum source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(po.OwnKeySelector)}>? primaryComparer,bool {this.SingletonKeyParameter}{this.MaxTakeCtorParamSuffix}{this.ClosurePrimaryCtorSuffix(keySelector,po.OwnKeySelector)}"
            : $"TEnum source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer,bool {this.SingletonKeyParameter}{this.MaxTakeCtorParamSuffix}{this.ClosurePrimaryCtorSuffix(keySelector)}";

    public override string GetEnumeratorConstructor =>
        this.PrimaryOrdering is { } po
            ? $"this.Build(comparer,primaryComparer,{this.SingletonKeyParameter}{this.MaxTakeArgSuffix}{this.ClosureArgsSuffixForCtorBody(keySelector,po.OwnKeySelector)});"
            : $"this.Build(comparer,{this.SingletonKeyParameter}{this.MaxTakeArgSuffix}{this.ClosureArgsSuffixForCtorBody(keySelector)});";

    public override string GetEnumeratorFields =>
        this.PrimaryOrdering is { } po ? this.BuildMembersCombined(po) : this.BuildMembersSingle();

    private string BuildMembersSingle() {
        var keyType = FuncReturnTypeName(keySelector);
        var directComparison=CanUseDirectDefaultCompare(comparer,keyType);
        var comparerParameter=string.Empty;var comparerArgument=string.Empty;
        var topKComparerParam=string.Empty;var topKComparerArg=string.Empty;
        if(!directComparison){
            comparerParameter=$"global::System.Collections.Generic.IComparer<{keyType}> comparer,";
            comparerArgument="comparer,";
            topKComparerParam=$"global::System.Collections.Generic.IComparer<{keyType}> comparer";
            topKComparerArg="comparer";
        }
        var compareExpr = directComparison
            ? "keys[left].CompareTo(keys[right])"
            : "comparer.Compare(keys[left],keys[right])";
        var compareExprDirect = directComparison
            ? "left.CompareTo(right)"
            : "this.comparer.Compare(left,right)";
        var sortCall = this.MaxTakeCountExpression is not null
            ? $"global::Seamless.Linq.GeneratedCodeHelpers.PartialSortIndexes(this.indexMap!,this.count,0,global::System.Math.Max(0,__maxTakeCount)-1,new IndexComparer({comparerArgument}this.keys!));"
            : $"global::Seamless.Linq.GeneratedCodeHelpers.SortIndexes(this.indexMap!,this.count,new IndexComparer({comparerArgument}this.keys!));";

        var topKFields = this.MaxTakeCountExpression is not null
            ? $$"""
            private bool useTopK;
            private global::Seamless.Linq.Internal.TopKBuffer<{{this.ElementType}}, {{keyType}}, TopKComparer> topK;
            private readonly struct TopKComparer({{topKComparerParam}}) : global::System.Collections.Generic.IComparer<{{keyType}}> {
                {{(directComparison ? "" : $"private readonly global::System.Collections.Generic.IComparer<{keyType}> comparer = comparer;")}}
                public int Compare({{keyType}} left,{{keyType}} right){
                    var compare={{compareExprDirect}};
                    return {{(descending ? "-compare" : "compare")}};
                }
            }
            """
            : string.Empty;

        var topKBuildBranch = this.MaxTakeCountExpression is not null
            ? $$"""
            if (__maxTakeCount <= 64) {
                this.useTopK = true;
                this.topK = new global::Seamless.Linq.Internal.TopKBuffer<{{this.ElementType}}, {{keyType}}, TopKComparer>(__maxTakeCount, new TopKComparer({{topKComparerArg}}));
                try {
                    {{this.SortLoopTopKSingle}}
                } catch {
                    this.topK.Dispose();
                    this.source.Dispose();
                    throw;
                }
                return;
            }
            """
            : string.Empty;

        return $$"""
        private {{this.ElementType}}[]? values;
        private {{keyType}}[]? keys;
        private int[]? indexMap;
        private int count;
        private int position = -1;
        {{topKFields}}
        private readonly struct IndexComparer({{comparerParameter}}{{keyType}}[] keys) : global::System.Collections.Generic.IComparer<int> {
            public int Compare(int left,int right){
                var compare={{compareExpr}};
                if(compare!=0) return {{(descending ? "-compare" : "compare")}};
                return left.CompareTo(right);
            }
        }
        private static void ReturnArray<TArrayItem>(TArrayItem[]? array){
            if(array is not null){
                global::System.Buffers.ArrayPool<TArrayItem>.Shared.Return(array,clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TArrayItem>());
            }
        }
        private void DisposeBuffers(){
            ReturnArray(this.values);
            this.values=null;
            ReturnArray(this.keys);
            this.keys=null;
            if(this.indexMap is not null){
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.indexMap,clearArray: false);
                this.indexMap=null;
            }
            this.count=0;
            this.position=-1;
        }
        private void EnsureCapacity(int minimum){
            if(this.values is not null && this.values.Length>=minimum){
                return;
            }
            var capacity=this.values is null ? 4 : this.values.Length * 2;
            if(capacity<minimum){
                capacity=minimum;
            }
            var newValues=global::System.Buffers.ArrayPool<{{this.ElementType}}>.Shared.Rent(capacity);
            var newKeys=global::System.Buffers.ArrayPool<{{keyType}}>.Shared.Rent(capacity);
            var newIndexMap=global::System.Buffers.ArrayPool<int>.Shared.Rent(capacity);
            if(this.count!=0){
                global::System.Array.Copy(this.values!,newValues,this.count);
                global::System.Array.Copy(this.keys!,newKeys,this.count);
                global::System.Array.Copy(this.indexMap!,newIndexMap,this.count);
            }
            ReturnArray(this.values);
            ReturnArray(this.keys);
            if(this.indexMap is not null){
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.indexMap,clearArray: false);
            }
            this.values=newValues;
            this.keys=newKeys;
            this.indexMap=newIndexMap;
        }
        private void AddItem({{this.ElementType}} value){
            this.EnsureCapacity(this.count+1);
            this.values![this.count]=value;
            this.indexMap![this.count]=this.count;
            this.count++;
        }
        private void Build(global::System.Collections.Generic.IComparer<{{keyType}}>? comparer,bool {{this.SingletonKeyParameter}}{{this.MaxTakeCtorParamSuffix}}{{this.ClosureMethodParameterSuffix(keySelector)}}){
            comparer ??= global::System.Collections.Generic.Comparer<{{keyType}}>.Default;
            {{topKBuildBranch}}
            try {
                if(this.source.TryGetNonEnumeratedCount(out var __count) && __count>0){
                    this.EnsureCapacity(__count);
                }
                {{this.SortLoopSingle}}
                // 通常のmaterializationは単一keyを省く。First系の消費側は単一keyも評価する。
                if(this.count>1||({{this.SingletonKeyParameter}}&&this.count==1)){
                    {{this.KeyLoopSingle}}
                }
                if(this.count>1){
                    {{sortCall}}
                }
            }catch{
                this.DisposeBuffers();
                this.source.Dispose();
                throw;
            }
        }
        """;
    }

    private string SortLoopSingle => this.UnorderedSource.CanProvideSpan
        ? $$"""
                this.source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span){
                    this.AddItem({{this.item}});
                }
            """
        : $$"""
                while(this.source.TryGetNext(out var {{this.item}})){
                    this.AddItem({{this.item}});
                }
            """;

    private string SortLoopTopKSingle => this.UnorderedSource.CanProvideSpan
        ? $$"""
                this.source.TryGetSpan(out var __span);
                for(var __i = 0; __i < __span.Length; __i++){
                    var {{this.item}} = __span[__i];
                    {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                    {{this.FuncAssignIndented("                    ", keySelector, nameof(keySelector), this.key, this.item)}}
                    this.topK.Push({{this.item}}, {{this.key}});
                }
            """
        : $$"""
                while(this.source.TryGetNext(out var {{this.item}})){
                    {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                    {{this.FuncAssignIndented("                    ", keySelector, nameof(keySelector), this.key, this.item)}}
                    this.topK.Push({{this.item}}, {{this.key}});
                }
            """;

    private string KeyLoopSingle => $$"""
                    for(var __keyIndex=0;__keyIndex<this.count;__keyIndex++){
                        var {{this.item}}=this.values![__keyIndex];
                        {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                        {{this.FuncAssignIndented("                        ",keySelector,nameof(keySelector),this.key,this.item)}}
                        this.keys![__keyIndex]={{this.key}};
                    }
            """;

    private string BuildMembersCombined(OrderingBase po) {
        var primaryKeyType = FuncReturnTypeName(po.OwnKeySelector);
        var keyType = FuncReturnTypeName(keySelector);
        var directPrimary=CanUseDirectDefaultCompare(po.OwnRawComparer,primaryKeyType);var directSecondary=CanUseDirectDefaultCompare(comparer,keyType);
        var parameters=new List<string>();var arguments=new List<string>();
        var topKParams=new List<string>();var topKArgs=new List<string>();
        if(!directPrimary){
            parameters.Add($"global::System.Collections.Generic.IComparer<{primaryKeyType}> primaryComparer");
            arguments.Add("primaryComparer");
            topKParams.Add($"global::System.Collections.Generic.IComparer<{primaryKeyType}> primaryComparer");
            topKArgs.Add("primaryComparer");
        }
        if(!directSecondary){
            parameters.Add($"global::System.Collections.Generic.IComparer<{keyType}> comparer");
            arguments.Add("comparer");
            topKParams.Add($"global::System.Collections.Generic.IComparer<{keyType}> comparer");
            topKArgs.Add("comparer");
        }
        parameters.Add($"{primaryKeyType}[] primaryKeys");parameters.Add($"{keyType}[] keys");arguments.Add("this.primaryKeys!");arguments.Add("this.keys!");
        var constructorParameters=string.Join(",",parameters);var constructorArguments=string.Join(",",arguments);
        var topKConstructorParams=string.Join(",",topKParams);var topKConstructorArgs=string.Join(",",topKArgs);

        var primaryCompareExpr = directPrimary
            ? "primaryKeys[left].CompareTo(primaryKeys[right])"
            : "primaryComparer.Compare(primaryKeys[left],primaryKeys[right])";
        var compareExpr = directSecondary
            ? "keys[left].CompareTo(keys[right])"
            : "comparer.Compare(keys[left],keys[right])";

        var primaryCompareExprDirect = directPrimary
            ? "lp.CompareTo(rp)"
            : "this.primaryComparer.Compare(lp,rp)";
        var compareExprDirect = directSecondary
            ? "ls.CompareTo(rs)"
            : "this.comparer.Compare(ls,rs)";

        var sortCall = this.MaxTakeCountExpression is not null
            ? $"global::Seamless.Linq.GeneratedCodeHelpers.PartialSortIndexes(this.indexMap!,this.count,0,global::System.Math.Max(0,__maxTakeCount)-1,new IndexComparer({constructorArguments}));"
            : $"global::Seamless.Linq.GeneratedCodeHelpers.SortIndexes(this.indexMap!,this.count,new IndexComparer({constructorArguments}));";

        var topKCombinedFields = this.MaxTakeCountExpression is not null
            ? $$"""
            private bool useTopK;
            private global::Seamless.Linq.Internal.TopKCombinedBuffer<{{this.ElementType}}, {{primaryKeyType}}, {{keyType}}, TopKCombinedComparer> topKCombined;
            private readonly struct TopKCombinedComparer({{topKConstructorParams}}) : global::Seamless.Linq.Internal.ITopKCombinedComparer<{{primaryKeyType}}, {{keyType}}> {
                {{(directPrimary ? "" : $"private readonly global::System.Collections.Generic.IComparer<{primaryKeyType}> primaryComparer = primaryComparer;")}}
                {{(directSecondary ? "" : $"private readonly global::System.Collections.Generic.IComparer<{keyType}> comparer = comparer;")}}
                public int Compare({{primaryKeyType}} lp, {{keyType}} ls, {{primaryKeyType}} rp, {{keyType}} rs){
                    var primaryCompare = {{primaryCompareExprDirect}};
                    if(primaryCompare != 0) return {{(po.OwnDescending ? "-primaryCompare" : "primaryCompare")}};
                    var compare = {{compareExprDirect}};
                    return {{(descending ? "-compare" : "compare")}};
                }
            }
            """
            : string.Empty;

        var topKCombinedBuildBranch = this.MaxTakeCountExpression is not null
            ? $$"""
            if (__maxTakeCount <= 64) {
                this.useTopK = true;
                this.topKCombined = new global::Seamless.Linq.Internal.TopKCombinedBuffer<{{this.ElementType}}, {{primaryKeyType}}, {{keyType}}, TopKCombinedComparer>(__maxTakeCount, new TopKCombinedComparer({{topKConstructorArgs}}));
                try {
                    {{this.SortLoopTopKCombined(po)}}
                } catch {
                    this.topKCombined.Dispose();
                    this.source.Dispose();
                    throw;
                }
                return;
            }
            """
            : string.Empty;

        return $$"""
        private {{this.ElementType}}[]? values;
        private {{primaryKeyType}}[]? primaryKeys;
        private {{keyType}}[]? keys;
        private int[]? indexMap;
        private int count;
        private int position = -1;
        {{topKCombinedFields}}
        private readonly struct IndexComparer({{constructorParameters}}) : global::System.Collections.Generic.IComparer<int> {
            public int Compare(int left,int right){
                var primaryCompare={{primaryCompareExpr}};
                if(primaryCompare!=0) return {{(po.OwnDescending ? "-primaryCompare" : "primaryCompare")}};
                var compare={{compareExpr}};
                if(compare!=0) return {{(descending ? "-compare" : "compare")}};
                return left.CompareTo(right);
            }
        }
        private static void ReturnArray<TArrayItem>(TArrayItem[]? array){
            if(array is not null){
                global::System.Buffers.ArrayPool<TArrayItem>.Shared.Return(array,clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TArrayItem>());
            }
        }
        private void DisposeBuffers(){
            ReturnArray(this.values);
            this.values=null;
            ReturnArray(this.primaryKeys);
            this.primaryKeys=null;
            ReturnArray(this.keys);
            this.keys=null;
            if(this.indexMap is not null){
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.indexMap,clearArray: false);
                this.indexMap=null;
            }
            this.count=0;
            this.position=-1;
        }
        private void EnsureCapacity(int minimum){
            if(this.values is not null && this.values.Length>=minimum){
                return;
            }
            var capacity=this.values is null ? 4 : this.values.Length * 2;
            if(capacity<minimum){
                capacity=minimum;
            }
            var newValues=global::System.Buffers.ArrayPool<{{this.ElementType}}>.Shared.Rent(capacity);
            var newPrimaryKeys=global::System.Buffers.ArrayPool<{{primaryKeyType}}>.Shared.Rent(capacity);
            var newKeys=global::System.Buffers.ArrayPool<{{keyType}}>.Shared.Rent(capacity);
            var newIndexMap=global::System.Buffers.ArrayPool<int>.Shared.Rent(capacity);
            if(this.count!=0){
                global::System.Array.Copy(this.values!,newValues,this.count);
                global::System.Array.Copy(this.primaryKeys!,newPrimaryKeys,this.count);
                global::System.Array.Copy(this.keys!,newKeys,this.count);
                global::System.Array.Copy(this.indexMap!,newIndexMap,this.count);
            }
            ReturnArray(this.values);
            ReturnArray(this.primaryKeys);
            ReturnArray(this.keys);
            if(this.indexMap is not null){
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.indexMap,clearArray: false);
            }
            this.values=newValues;
            this.primaryKeys=newPrimaryKeys;
            this.keys=newKeys;
            this.indexMap=newIndexMap;
        }
        private void AddItem({{this.ElementType}} value){
            this.EnsureCapacity(this.count+1);
            this.values![this.count]=value;
            this.indexMap![this.count]=this.count;
            this.count++;
        }
        private void Build(global::System.Collections.Generic.IComparer<{{keyType}}>? comparer,global::System.Collections.Generic.IComparer<{{primaryKeyType}}>? primaryComparer,bool {{this.SingletonKeyParameter}}{{this.MaxTakeCtorParamSuffix}}{{this.ClosureMethodParameterSuffix(keySelector,po.OwnKeySelector)}}){
            comparer ??= global::System.Collections.Generic.Comparer<{{keyType}}>.Default;
            primaryComparer ??= global::System.Collections.Generic.Comparer<{{primaryKeyType}}>.Default;
            {{topKCombinedBuildBranch}}
            try {
                if(this.source.TryGetNonEnumeratedCount(out var __count) && __count>0){
                    this.EnsureCapacity(__count);
                }
                {{this.SortLoopCombined(po)}}
                // 通常のmaterializationは単一keyを省く。First系の消費側は単一keyも評価する。
                if(this.count>1||({{this.SingletonKeyParameter}}&&this.count==1)){
                    {{this.KeyLoopCombined(po)}}
                }
                if(this.count>1){
                    {{sortCall}}
                }
            }catch{
                this.DisposeBuffers();
                this.source.Dispose();
                throw;
            }
        }
        """;
    }

    private string SortLoopCombined(OrderingBase po) => this.UnorderedSource.CanProvideSpan
        ? $$"""
                this.source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span){
                    this.AddItem({{this.item}});
                }
            """
        : $$"""
                while(this.source.TryGetNext(out var {{this.item}})){
                    this.AddItem({{this.item}});
                }
            """;

    private string SortLoopTopKCombined(OrderingBase po) => this.UnorderedSource.CanProvideSpan
        ? $$"""
                this.source.TryGetSpan(out var __span);
                for(var __i = 0; __i < __span.Length; __i++){
                    var {{this.item}} = __span[__i];
                    {{FuncReturnTypeName(po.OwnKeySelector)}} {{this.a}} = default!;
                    {{this.FuncAssignIndented("                    ", po.OwnKeySelector, "primaryKeySelector", this.a, this.item)}}
                    {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                    {{this.FuncAssignIndented("                    ", keySelector, nameof(keySelector), this.key, this.item)}}
                    this.topKCombined.Push({{this.item}}, {{this.a}}, {{this.key}});
                }
            """
        : $$"""
                while(this.source.TryGetNext(out var {{this.item}})){
                    {{FuncReturnTypeName(po.OwnKeySelector)}} {{this.a}} = default!;
                    {{this.FuncAssignIndented("                    ", po.OwnKeySelector, "primaryKeySelector", this.a, this.item)}}
                    {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                    {{this.FuncAssignIndented("                    ", keySelector, nameof(keySelector), this.key, this.item)}}
                    this.topKCombined.Push({{this.item}}, {{this.a}}, {{this.key}});
                }
            """;

    private string KeyLoopCombined(OrderingBase po) => $$"""
                    for(var __keyIndex=0;__keyIndex<this.count;__keyIndex++){
                        var {{this.item}}=this.values![__keyIndex];
                        {{FuncReturnTypeName(po.OwnKeySelector)}} {{this.a}}=default!;
                        {{this.FuncAssignIndented("                        ",po.OwnKeySelector,"primaryKeySelector",this.a,this.item)}}
                        {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                        {{this.FuncAssignIndented("                        ",keySelector,nameof(keySelector),this.key,this.item)}}
                        this.keys![__keyIndex]={{this.key}};
                        this.primaryKeys![__keyIndex]={{this.a}};
                    }
            """;

    protected override string GetTryGetSpanBody => this.MaxTakeCountExpression is not null
        ? (this.PrimaryOrdering is { }
            ? $$"""
                if(this.useTopK) return this.topKCombined.TryGetSpan(out span);
                span = default;
                return false;
                """
            : $$"""
                if(this.useTopK) return this.topK.TryGetSpan(out span);
                span = default;
                return false;
                """)
        : "span = default; return false;";

    protected override string GetTryCopyToBody => this.MaxTakeCountExpression is not null
        ? (this.PrimaryOrdering is { }
            ? $$"""
                if(this.useTopK) return this.topKCombined.TryCopyTo(destination, __offset);
                return false;
                """
            : $$"""
                if(this.useTopK) return this.topK.TryCopyTo(destination, __offset);
                return false;
                """)
        : "return false;";

    protected override string GetTryGetNonEnumeratedCountBody => this.MaxTakeCountExpression is not null
        ? (this.PrimaryOrdering is { }
            ? $$"""
                if(this.useTopK) return this.topKCombined.TryGetNonEnumeratedCount(out count);
                count=this.count-(this.position+1);
                if(count<0) count=0;
                return true;
                """
            : $$"""
                if(this.useTopK) return this.topK.TryGetNonEnumeratedCount(out count);
                count=this.count-(this.position+1);
                if(count<0) count=0;
                return true;
                """)
        : $$"""
            count=this.count-(this.position+1);
            if(count<0) count=0;
            return true;
            """;

    public override string GetTryGetNextBody => this.MaxTakeCountExpression is not null
        ? (this.PrimaryOrdering is { }
            ? $$"""
                if(this.useTopK) {
                    if(this.topKCombined.TryGetNext(out var __item)) {
                        current = this.Current = __item;
                        return true;
                    }
                    current = default!;
                    return false;
                }
                var next=this.position+1;
                if(next>=this.count || this.values is null || this.indexMap is null){
                    current=default!;
                    return false;
                }
                this.position=next;
                current=this.Current=this.values[this.indexMap[next]];
                return true;
                """
            : $$"""
                if(this.useTopK) {
                    if(this.topK.TryGetNext(out var __item)) {
                        current = this.Current = __item;
                        return true;
                    }
                    current = default!;
                    return false;
                }
                var next=this.position+1;
                if(next>=this.count || this.values is null || this.indexMap is null){
                    current=default!;
                    return false;
                }
                this.position=next;
                current=this.Current=this.values[this.indexMap[next]];
                return true;
                """)
        : $$"""
            var next=this.position+1;
            if(next>=this.count || this.values is null || this.indexMap is null){
                current=default!;
                return false;
            }
            this.position=next;
            current=this.Current=this.values[this.indexMap[next]];
            return true;
            """;

    protected override string GetEnumeratorDisposeBody => this.MaxTakeCountExpression is not null
        ? (this.PrimaryOrdering is { }
            ? $$"""
                if(this.useTopK) {
                    this.topKCombined.Dispose();
                    this.source.Dispose();
                    return;
                }
                this.DisposeBuffers();
                this.source.Dispose();
                """
            : $$"""
                if(this.useTopK) {
                    this.topK.Dispose();
                    this.source.Dispose();
                    return;
                }
                this.DisposeBuffers();
                this.source.Dispose();
                """)
        : $$"""
            this.DisposeBuffers();
            this.source.Dispose();
            """;
}

namespace Test.System.Linq.Intercept;

// GroupBy の後ろに順序段(OrderBy/ThenBy)が続く連鎖の融合。
// 2026-09-30 の差分ファズで次の不具合を確認して直した:
//  - GroupBy → Select → OrderBy が、キー選択子をグループ型へ適用する不正なコードを出してビルドが通らない
//  - GroupBy 後の順序段が間の Take/Skip/TakeWhile を飛び越えてソートされ、結果が変わる
//  - GroupBy より前の OrderBy が捨てられる
//  - グループのソートが不安定で、キーが等しいグループの順が LINQ と変わる
//  - OrderBy を重ねた連鎖(後の OrderBy だけで並べ、前の順序を捨てていた)
//  - 順序段を含む連鎖で ToList/ToArray が enumerator の複製を Dispose し、プール配列の二重 Return で
//    後続の無関係なクエリのデータが壊れる
public sealed class GroupByOrder {
    private static List<int> Source() {
        var values = new List<int>();
        for(var i = 0;i<100;i++) {
            values.Add(i*37%53);
        }
        return values;
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_Select_OrderBy() {
        Assert.Equal(SelectOrderBy_NoOptimized(Source()),SelectOrderBy_Optimized(Source()));
        Assert.Equal(SelectAnonymousOrderBy_NoOptimized(Source()),SelectAnonymousOrderBy_Optimized(Source()));
        Assert.Equal(SelectWhereOrderByDescending_NoOptimized(Source()),SelectWhereOrderByDescending_Optimized(Source()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SelectOrderBy_Optimized(List<int> source) => source.GroupBy(x => x%3).Select(g => g.Count()).OrderBy(x => x).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> SelectOrderBy_NoOptimized(List<int> source) => source.GroupBy(x => x%3).Select(g => g.Count()).OrderBy(x => x).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SelectAnonymousOrderBy_Optimized(List<int> source) => source.GroupBy(x => x%4).Select(g => new { g.Key,C = g.Count() }).OrderBy(r => r.C).Select(r => r.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> SelectAnonymousOrderBy_NoOptimized(List<int> source) => source.GroupBy(x => x%4).Select(g => new { g.Key,C = g.Count() }).OrderBy(r => r.C).Select(r => r.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SelectWhereOrderByDescending_Optimized(List<int> source) => source.GroupBy(x => x%7).Where(g => g.Key>1).Select(g => g.Key).OrderByDescending(k => k).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> SelectWhereOrderByDescending_NoOptimized(List<int> source) => source.GroupBy(x => x%7).Where(g => g.Key>1).Select(g => g.Key).OrderByDescending(k => k).ToList();

    [Fact, Trait("Category","Unit")]
    public void GroupBy_OrderBy_EqualKeys_KeepFirstAppearanceOrder() {
        // 52 グループのうちキー(0/1)が等しいものがほとんど。ソートが不安定だとグループの並びが LINQ と変わる。
        Assert.Equal(EqualKeys_NoOptimized(Source()),EqualKeys_Optimized(Source()));
        Assert.Equal(ThreeKeys_NoOptimized(Source()),ThreeKeys_Optimized(Source()));
        Assert.Equal(TwoOrderBy_NoOptimized(Source()),TwoOrderBy_Optimized(Source()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> EqualKeys_Optimized(List<int> source) => source.GroupBy(x => x).OrderBy(g => g.Key%2).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> EqualKeys_NoOptimized(List<int> source) => source.GroupBy(x => x).OrderBy(g => g.Key%2).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ThreeKeys_Optimized(List<int> source) => source.GroupBy(x => x).OrderBy(g => g.Key%2).ThenByDescending(g => g.Key%3).ThenBy(g => g.Key%5).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> ThreeKeys_NoOptimized(List<int> source) => source.GroupBy(x => x).OrderBy(g => g.Key%2).ThenByDescending(g => g.Key%3).ThenBy(g => g.Key%5).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TwoOrderBy_Optimized(List<int> source) => source.GroupBy(x => x%30).OrderBy(g => g.Key%3).OrderByDescending(g => g.Key%2).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TwoOrderBy_NoOptimized(List<int> source) => source.GroupBy(x => x%30).OrderBy(g => g.Key%3).OrderByDescending(g => g.Key%2).Select(g => g.Key).ToList();

    [Fact, Trait("Category","Unit")]
    public void GroupBy_TakeSkip_ThenOrderBy_OrdersOnlyTheRemainingGroups() {
        // 順序段はグループを絞った「後」に効く。絞る前にソートすると、残るグループが変わる。
        Assert.Equal(TakeOrderBy_NoOptimized(Source()),TakeOrderBy_Optimized(Source()));
        Assert.Equal(SkipOrderByDescending_NoOptimized(Source()),SkipOrderByDescending_Optimized(Source()));
        Assert.Equal(SkipTakeOrderBy_NoOptimized(Source()),SkipTakeOrderBy_Optimized(Source()));
        Assert.Equal(TakeWhileOrderByDescending_NoOptimized(Source()),TakeWhileOrderByDescending_Optimized(Source()));
        Assert.Equal(WhereWithIndexOrderBy_NoOptimized(Source()),WhereWithIndexOrderBy_Optimized(Source()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeOrderBy_Optimized(List<int> source) => source.GroupBy(x => x%7).Take(3).OrderBy(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TakeOrderBy_NoOptimized(List<int> source) => source.GroupBy(x => x%7).Take(3).OrderBy(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SkipOrderByDescending_Optimized(List<int> source) => source.GroupBy(x => x%7).Skip(1).OrderByDescending(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> SkipOrderByDescending_NoOptimized(List<int> source) => source.GroupBy(x => x%7).Skip(1).OrderByDescending(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SkipTakeOrderBy_Optimized(List<int> source) => source.GroupBy(x => x%7).Skip(2).Take(3).OrderBy(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> SkipTakeOrderBy_NoOptimized(List<int> source) => source.GroupBy(x => x%7).Skip(2).Take(3).OrderBy(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeWhileOrderByDescending_Optimized(List<int> source) => source.GroupBy(x => x%7).TakeWhile(g => g.Key<5).OrderByDescending(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TakeWhileOrderByDescending_NoOptimized(List<int> source) => source.GroupBy(x => x%7).TakeWhile(g => g.Key<5).OrderByDescending(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> WhereWithIndexOrderBy_Optimized(List<int> source) => source.GroupBy(x => x%7).Where((g,i) => i>1).OrderBy(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> WhereWithIndexOrderBy_NoOptimized(List<int> source) => source.GroupBy(x => x%7).Where((g,i) => i>1).OrderBy(g => g.Key).Select(g => g.Key).ToList();

    [Fact, Trait("Category","Unit")]
    public void OrderBy_BeforeGroupBy_DeterminesElementAndGroupOrder() {
        Assert.Equal(OrderByThenGroupBy_NoOptimized(Source()),OrderByThenGroupBy_Optimized(Source()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> OrderByThenGroupBy_Optimized(List<int> source) => source.OrderBy(x => x%5).GroupBy(x => x%7).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> OrderByThenGroupBy_NoOptimized(List<int> source) => source.OrderBy(x => x%5).GroupBy(x => x%7).Select(g => g.Key).ToList();

    [Fact, Trait("Category","Unit")]
    public void OrderBy_OrderBy_EqualKeysKeepThePreviousOrder() {
        // OrderBy を重ねると、後の OrderBy が主キーで、前の順序はキーが等しいときの並びとして残る(GroupBy に限らない)。
        Assert.Equal(TwoOrderBy_Plain_NoOptimized(Source()),TwoOrderBy_Plain_Optimized(Source()));
        Assert.Equal(ThenByThenOrderBy_Plain_NoOptimized(Source()),ThenByThenOrderBy_Plain_Optimized(Source()));
        // 3 つ以上の OrderBy は、後の OrderBy を前の順序への ThenBy として 1 回の並べ替えにまとめる展開になっていた(主キーと副キーが逆)。
        Assert.Equal(ThreeOrderBy_Plain_NoOptimized(Source()),ThreeOrderBy_Plain_Optimized(Source()));
        Assert.Equal(ThreeOrderByWithThenBy_Plain_NoOptimized(Source()),ThreeOrderByWithThenBy_Plain_Optimized(Source()));
        Assert.Equal(ThreeKeys_ThenZip_NoOptimized(Source()),ThreeKeys_ThenZip_Optimized(Source()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ThreeOrderBy_Plain_Optimized(List<int> source) => source.OrderBy(x => x%5).OrderBy(x => x&3).OrderBy(x => x%3).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> ThreeOrderBy_Plain_NoOptimized(List<int> source) => source.OrderBy(x => x%5).OrderBy(x => x&3).OrderBy(x => x%3).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ThreeOrderByWithThenBy_Plain_Optimized(List<int> source) => source.OrderBy(x => x%5).ThenBy(x => x%4).OrderBy(x => x&3).ThenBy(x => x%4).OrderBy(x => x%3).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> ThreeOrderByWithThenBy_Plain_NoOptimized(List<int> source) => source.OrderBy(x => x%5).ThenBy(x => x%4).OrderBy(x => x&3).ThenBy(x => x%4).OrderBy(x => x%3).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ThreeKeys_ThenZip_Optimized(List<int> source) => source.OrderBy(x => x%5).ThenBy(x => x&3).ThenBy(x => x%3).Zip(new List<int> { 1,2,3,4,5,6,7,8 },(a,b) => a*10+b).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> ThreeKeys_ThenZip_NoOptimized(List<int> source) => source.OrderBy(x => x%5).ThenBy(x => x&3).ThenBy(x => x%3).Zip(new List<int> { 1,2,3,4,5,6,7,8 },(a,b) => a*10+b).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TwoOrderBy_Plain_Optimized(List<int> source) => source.OrderBy(x => x%3).OrderByDescending(x => x%2).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TwoOrderBy_Plain_NoOptimized(List<int> source) => source.OrderBy(x => x%3).OrderByDescending(x => x%2).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ThenByThenOrderBy_Plain_Optimized(List<int> source) => source.OrderBy(x => x%3).ThenBy(x => x%5).OrderBy(x => x%2).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> ThenByThenOrderBy_Plain_NoOptimized(List<int> source) => source.OrderBy(x => x%3).ThenBy(x => x%5).OrderBy(x => x%2).ToList();

    [Fact, Trait("Category","Unit")]
    public void ToListToArray_AfterOrderStage_ReturnPooledArraysOnce() {
        // 順序段は、ArrayPool の配列をコンストラクタで借りる enumerator になる。ToList/ToArray の Compute が複製を Dispose すると
        // 呼び出し元の Dispose との二重 Return になり、プールが同じ配列を 2 回貸し出す(後続の無関係なクエリのデータが壊れる)。
        Assert.Equal(new List<int> { 0,2,5 },PooledOrderToList_Optimized(Source()));
        AssertPoolHasNoDuplicates();
        Assert.Equal(new[] { 0,2,5 },PooledOrderToArray_Optimized(Source()));
        AssertPoolHasNoDuplicates();
        Assert.Equal(new List<int> { 6,5,4,3,2 },SelectWhereOrderByDescending_Optimized(Source()));
        AssertPoolHasNoDuplicates();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> PooledOrderToList_Optimized(List<int> source) => source.GroupBy(x => x%7).Take(3).OrderBy(g => g.Key).Select(g => g.Key).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] PooledOrderToArray_Optimized(List<int> source) => source.GroupBy(x => x%7).Take(3).OrderBy(g => g.Key).Select(g => g.Key).ToArray();

    [Fact, Trait("Category","Unit")]
    public void MinMax_AfterOrderStage_ReturnPooledArraysOnce() {
        // Min/Max(セレクター無し)はライブラリ関数へ列挙子の複製を値渡しし、ライブラリがその複製を破棄する。呼び出し元も本体を破棄すると二重 Return になる。
        Assert.Equal(OrderByPrependMin_NoOptimized(Source()),OrderByPrependMin_Optimized(Source()));
        AssertPoolHasNoDuplicates();
        Assert.Equal(OrderByThenByMax_NoOptimized(Source()),OrderByThenByMax_Optimized(Source()));
        AssertPoolHasNoDuplicates();
        Assert.Equal(PrependOrderByAppendMin_NoOptimized(Source()),PrependOrderByAppendMin_Optimized(Source()));
        AssertPoolHasNoDuplicates();
        Assert.Equal(ConcatOrderByExceptMax_NoOptimized(Source(),Second()),ConcatOrderByExceptMax_Optimized(Source(),Second()));
        AssertPoolHasNoDuplicates();
        Assert.Equal(TakeOrderByDescendingSelectMax_NoOptimized(Source()),TakeOrderByDescendingSelectMax_Optimized(Source()));
        AssertPoolHasNoDuplicates();
    }

    private static List<int> Second() => [4,5,6];

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int OrderByPrependMin_Optimized(List<int> source) => source.OrderBy(x => x%5).Prepend(1).Min();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int OrderByPrependMin_NoOptimized(List<int> source) => source.OrderBy(x => x%5).Prepend(1).Min();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int OrderByThenByMax_Optimized(List<int> source) => source.OrderBy(x => x%5).ThenBy(x => x).Max();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int OrderByThenByMax_NoOptimized(List<int> source) => source.OrderBy(x => x%5).ThenBy(x => x).Max();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int PrependOrderByAppendMin_Optimized(List<int> source) => source.Prepend(8).OrderBy(x => -x).ThenBy(x => x%4).Append(1).Min();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int PrependOrderByAppendMin_NoOptimized(List<int> source) => source.Prepend(8).OrderBy(x => -x).ThenBy(x => x%4).Append(1).Min();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int ConcatOrderByExceptMax_Optimized(List<int> source,List<int> second) => source.Concat(second).OrderBy(x => x%5).Except(second).Max();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int ConcatOrderByExceptMax_NoOptimized(List<int> source,List<int> second) => source.Concat(second).OrderBy(x => x%5).Except(second).Max();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int TakeOrderByDescendingSelectMax_Optimized(List<int> source) => source.Where(x => true).Take(11).OrderByDescending(x => -x).Select(x => x*3).Max();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int TakeOrderByDescendingSelectMax_NoOptimized(List<int> source) => source.Where(x => true).Take(11).OrderByDescending(x => -x).Select(x => x*3).Max();

    private static void AssertPoolHasNoDuplicates() {
        // 二重 Return されていると、同じ大きさの配列を続けて借りたときに同じ配列が複数回出てくる。
        // ArrayPool はスレッドごと・コアごとのキャッシュを持ち、テストの実行状況によっては二重 Return があっても現れないことがある
        // (修正を外しても通る場合がある)ので、これは補助の確認。確実な検出は生成コードの形を見る PerformanceShapeTests 側
        // (EnumerableToListUsesSegmentedBufferHelper / MinMaxCompute_OwnsTheEnumeratorCopy_CallerDoesNotDisposeAgain)で行う。
        var rented = new List<int[]>();
        var seen = new global::System.Collections.Generic.HashSet<int[]>(global::System.Collections.Generic.ReferenceEqualityComparer.Instance);
        var duplicated = false;
        try {
            for(var i = 0;i<80;i++) {
                var array = global::System.Buffers.ArrayPool<int>.Shared.Rent(8);
                if(seen.Add(array)) {
                    rented.Add(array);
                } else {
                    duplicated=true;
                }
            }
        } finally {
            foreach(var array in rented) {
                global::System.Buffers.ArrayPool<int>.Shared.Return(array);
            }
        }
        Assert.False(duplicated,"ArrayPool が同じ配列を二重に貸し出した(二重 Return)。");
    }
}

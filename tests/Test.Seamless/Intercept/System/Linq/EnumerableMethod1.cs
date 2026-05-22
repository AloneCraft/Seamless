using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableMethod1 {
    private readonly int instanceOffset = 1;

    // インターセプトされるメソッド: なし
    // 理由: インスタンスデリゲート（this.InstanceSelector等）はラムダ式ではないため、
    //       Linq傍受対象生成のIsInlineDelegateTypeチェック(143行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_InstanceDelegate() {
        Aggregate_func_InstanceDelegate_Impl();
    }
    private void Aggregate_func_InstanceDelegate_Impl() {
        var x = 1;
        var expected=SystemEnumerable.Aggregate(Source(),(left,right) => left+right+x);
        var actual=Source().Aggregate((left,right) => left+right+x);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: Aggregate_func_StaticDelegate_Impl()
    // 理由: Linq呼び出しを含むローカルメソッド呼び出し自体をインターセプトし、
    //       生成インターセプターから元メソッドへフォワードするため
    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_StaticDelegate() {
        var generated = AssertInterceptorGenerated();
        Assert.Contains(nameof(Aggregate_func_StaticDelegate_Impl),generated);
        Aggregate_func_StaticDelegate_Impl();
    }
    private static void Aggregate_func_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Aggregate(Source(),static (left,right) => left+right);
        var actual=Source().Aggregate(static (left,right) => left+right);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: インスタンスデリゲートはラムダ式ではないためIsInlineDelegateTypeに該当しnullを返す
    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_InstanceDelegate() {
        Aggregate_seed_func_InstanceDelegate_Impl();
    }

    private void Aggregate_seed_func_InstanceDelegate_Impl() {
        var expected=SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1);
        var actual=Source().Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: Aggregate_seed_func_StaticDelegate_Impl()
    // 理由: Fragment 実装から overload を自動解決するようになり、
    //       seed 付き Aggregate もローカルメソッド再生成でインライン展開できるため
    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_StaticDelegate() {
        var generated = AssertInterceptorGenerated();
        Assert.Contains(nameof(Aggregate_seed_func_StaticDelegate_Impl),generated);
        Aggregate_seed_func_StaticDelegate_Impl();
    }

    private static void Aggregate_seed_func_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value);
        var actual=Source().Aggregate(10,static (sum,value) => sum+value);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: インスタンスデリゲートはラムダ式ではないためIsInlineDelegateTypeに該当しnullを返す
    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_InstanceDelegate() {
        Aggregate_seed_func_resultSelector_InstanceDelegate_Impl();
    }

    private void Aggregate_seed_func_resultSelector_InstanceDelegate_Impl() {
        var expected=SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1);
        var actual=Source().Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: Aggregate_seed_func_resultSelector_StaticDelegate_Impl()
    // 理由: Fragment 実装から overload を自動解決するようになり、
    //       resultSelector 付き Aggregate もローカルメソッド再生成でインライン展開できるため
    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_StaticDelegate() {
        var generated = AssertInterceptorGenerated();
        Assert.Contains(nameof(Aggregate_seed_func_resultSelector_StaticDelegate_Impl),generated);
        Aggregate_seed_func_resultSelector_StaticDelegate_Impl();
    }

    private static void Aggregate_seed_func_resultSelector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value,static sum => sum*2);
        var actual=Source().Aggregate(10,static (sum,value) => sum+value,static sum => sum*2);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: AggregateByの戻り値IEnumerable<TSource>?は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_StaticDelegate() {
        AggregateBy_keySelector_seed_func_StaticDelegate_Impl();
    }

    private static void AggregateBy_keySelector_seed_func_StaticDelegate_Impl() {
        var expected=SystemEnumerable.AggregateBy(Source(),static value => value%2,0,static (sum,value) => sum+value);
        var actual=Source().AggregateBy(static value => value%2,0,static (sum,value) => sum+value);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: All_predicate_StaticDelegate_Impl()
    // 理由: All Fragment を使ってローカルメソッド再生成できるため
    [Fact, Trait("Category","Unit")]
    public void All_predicate_StaticDelegate() {
        var generated = AssertInterceptorGenerated();
        Assert.Contains(nameof(All_predicate_StaticDelegate_Impl),generated);
        All_predicate_StaticDelegate_Impl();
    }

    private static void All_predicate_StaticDelegate_Impl() {
        var expected=SystemEnumerable.All(Source(),static value => value>0);
        var actual=Source().All(static value => value>0);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Anyの戻り値boolは型パラメータを含まないが、GetInterceptableLocation(83行目)がnullを返す
    [Fact, Trait("Category","Unit")]
    public void Any_NoDelegate() {
        Any_NoDelegate_Impl();
    }

    private static void Any_NoDelegate_Impl() {
        var expected=SystemEnumerable.Any(Source());
        var actual=Source().Any();
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Anyの戻り値boolは型パラメータを含まないが、GetInterceptableLocation(83行目)がnullを返す
    [Fact, Trait("Category","Unit")]
    public void Any_predicate_StaticDelegate() {
        Any_predicate_StaticDelegate_Impl();
    }

    private static void Any_predicate_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Any(Source(),static value => value>3);
        var actual=Source().Any(static value => value>3);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: Select_Any_StaticDelegate_Impl()
    // 理由: ローカルメソッド再生成が Aggregate 専用ではなくなり、
    //       Select -> Any のような別フラグメント連結も再構成できるようになったため
    [Fact, Trait("Category","Unit")]
    public void Select_Any_StaticDelegate() {
        var generated = AssertInterceptorGenerated();
        Assert.Contains(nameof(Select_Any_StaticDelegate_Impl),generated);
        Select_Any_StaticDelegate_Impl();
    }

    private static void Select_Any_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Any(SystemEnumerable.Select(Source(),static value => value*2));
        var actual=Source().Select(static value => value*2).Any();
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Appendの戻り値IEnumerable<TSource>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void Append_element_NoDelegate() {
        Append_element_NoDelegate_Impl();
    }

    private static void Append_element_NoDelegate_Impl() {
        var expected=SystemEnumerable.Append(Source(),5);
        var actual=Source().Append(5);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: AsEnumerableの戻り値IEnumerable<TSource>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void AsEnumerable_NoDelegate() {
        AsEnumerable_NoDelegate_Impl();
    }

    private static void AsEnumerable_NoDelegate_Impl() {
        var expected=SystemEnumerable.AsEnumerable(Source());
        var actual=Source().AsEnumerable();
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: Source().Average()
    // 理由: Average(this IEnumerable<int>)の戻り値doubleは型パラメータを含まず、
    //       パラメータなしのためGetInterceptableLocationが有効な位置を返す
    [Fact, Trait("Category","Unit")]
    public void Average_int_NoDelegate() {
        Average_int_NoDelegate_Impl();
    }

    private static void Average_int_NoDelegate_Impl() {
        var expected=SystemEnumerable.Average(Source());
        var actual=Source().Average();
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Average(selector)の戻り値double?は型パラメータを含まないが、
    //       GetInterceptableLocation(83行目)がnullを返す
    [Fact, Trait("Category","Unit")]
    public void Average_selector_StaticDelegate() {
        Average_selector_StaticDelegate_Impl();
    }

    private static void Average_selector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Average(Rows(),static row => row.Value);
        var actual=Rows().Average(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Castの戻り値IEnumerable<TResult>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void Cast_NoDelegate() {
        Cast_NoDelegate_Impl();
    }

    private static void Cast_NoDelegate_Impl() {
        var expected=SystemEnumerable.Cast<int>(CastObjects());
        var actual=CastObjects().Cast<int>();
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: Chunk_size_NoDelegate_Impl()
    // 理由: Chunk Fragment が実装され、ローカルメソッド再生成でも評価できるため
    [Fact, Trait("Category","Unit")]
    public void Chunk_size_NoDelegate() {
        var generated = AssertInterceptorGenerated();
        Assert.Contains(nameof(Chunk_size_NoDelegate_Impl),generated);
        Chunk_size_NoDelegate_Impl();
    }

    private static void Chunk_size_NoDelegate_Impl() {
        var expected=SystemEnumerable.Chunk(Source(),2);
        var actual=Source().Chunk(2);
        AssertNestedSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Concatの戻り値IEnumerable<TSource>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void Concat_second_NoDelegate() {
        Concat_second_NoDelegate_Impl();
    }

    private static void Concat_second_NoDelegate_Impl() {
        var expected=SystemEnumerable.Concat(Source(),Second());
        var actual=Source().Concat(Second());
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Containsの戻り値boolは型パラメータを含まないが、GetInterceptableLocation(83行目)がnullを返す
    [Fact, Trait("Category","Unit")]
    public void Contains_value_NoDelegate() {
        Contains_value_NoDelegate_Impl();
    }

    private static void Contains_value_NoDelegate_Impl() {
        var expected=SystemEnumerable.Contains(Source(),3);
        var actual=Source().Contains(3);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Countの戻り値intは型パラメータを含まないが、GetInterceptableLocation(83行目)がnullを返す
    [Fact, Trait("Category","Unit")]
    public void Count_predicate_StaticDelegate() {
        Count_predicate_StaticDelegate_Impl();
    }

    private static void Count_predicate_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Count(Source(),static value => value>2);
        var actual=Source().Count(static value => value>2);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Distinctの戻り値IEnumerable<TSource>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void Distinct_NoDelegate() {
        Distinct_NoDelegate_Impl();
    }

    private static void Distinct_NoDelegate_Impl() {
        var expected=SystemEnumerable.Distinct(Duplicates());
        var actual=Duplicates().Distinct();
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: DistinctByの戻り値IEnumerable<TSource>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_StaticDelegate() {
        DistinctBy_keySelector_StaticDelegate_Impl();
    }

    private static void DistinctBy_keySelector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.DistinctBy(Rows(),static row => row.Key);
        var actual=Rows().DistinctBy(static row => row.Key);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Firstの戻り値TSourceは型パラメータのため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void First_predicate_StaticDelegate() {
        First_predicate_StaticDelegate_Impl();
    }

    private static void First_predicate_StaticDelegate_Impl() {
        var expected=SystemEnumerable.First(Source(),static value => value>2);
        var actual=Source().First(static value => value>2);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: GroupByの戻り値IEnumerable<IGrouping<TKey,TElement>>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_StaticDelegate() {
        GroupBy_keySelector_StaticDelegate_Impl();
    }

    private static void GroupBy_keySelector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key);
        var actual=Rows().GroupBy(static row => row.Key);
        AssertGroupSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: Join_keySelectors_resultSelector_StaticDelegate_Impl()
    // 理由: Join Fragment がローカルメソッド再生成の対象に含まれるため
    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_StaticDelegate() {
        var generated = AssertInterceptorGenerated();
        Assert.Contains(nameof(Join_keySelectors_resultSelector_StaticDelegate_Impl),generated);
        Join_keySelectors_resultSelector_StaticDelegate_Impl();
    }

    private static void Join_keySelectors_resultSelector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Join(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue);
        var actual=Source().Join(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: インスタンスデリゲートはラムダ式ではないためIsInlineDelegateTypeに該当しnullを返す
    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_InstanceDelegate() {
        Join_keySelectors_resultSelector_InstanceDelegate_Impl();
    }

    private void Join_keySelectors_resultSelector_InstanceDelegate_Impl() {
        var expected=SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actual=Source().Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Maxの戻り値TSource?は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void Max_selector_StaticDelegate() {
        Max_selector_StaticDelegate_Impl();
    }

    private static void Max_selector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Max(Rows(),static row => row.Value);
        var actual=Rows().Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: OrderByの戻り値IOrderedEnumerable<TElement>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_StaticDelegate() {
        OrderBy_keySelector_StaticDelegate_Impl();
    }

    private static void OrderBy_keySelector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.OrderBy(Rows(),static row => row.Key);
        var actual=Rows().OrderBy(static row => row.Key);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: Select_selector_StaticDelegate_Impl()
    // 理由: Select Fragment と private アサート呼び出しの反射フォワードにより、
    //       ローカルメソッド本体を再生成できるため
    [Fact, Trait("Category","Unit")]
    public void Select_selector_StaticDelegate() {
        Select_selector_StaticDelegate_Impl();
    }

    private static void Select_selector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Select(Source(),static value => value*2);
        var actual=Source().Select(static value => value*2);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: インスタンスデリゲートはラムダ式ではないためIsInlineDelegateTypeに該当しnullを返す
    [Fact, Trait("Category","Unit")]
    public void Select_selector_InstanceDelegate() {
        Select_selector_InstanceDelegate_Impl();
    }

    private void Select_selector_InstanceDelegate_Impl() {
        var expected=SystemEnumerable.Select(Source(),this.InstanceSelector);
        var actual=Source().Select(this.InstanceSelector);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: SelectManyの戻り値IEnumerable<TResult>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_StaticDelegate() {
        SelectMany_selector_StaticDelegate_Impl();
    }

    private static void SelectMany_selector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 });
        var actual=Source().SelectMany(static value => new[] { value,value+10 });
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: SkipWhileの戻り値IEnumerable<TSource>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_StaticDelegate() {
        SkipWhile_predicate_StaticDelegate_Impl();
    }

    private static void SkipWhile_predicate_StaticDelegate_Impl() {
        var expected=SystemEnumerable.SkipWhile(Source(),static value => value<3);
        var actual=Source().SkipWhile(static value => value<3);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Sumの戻り値int?は型パラメータを含まないが、GetInterceptableLocation(83行目)がnullを返す
    [Fact, Trait("Category","Unit")]
    public void Sum_selector_StaticDelegate() {
        Sum_selector_StaticDelegate_Impl();
    }

    private static void Sum_selector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Sum(Rows(),static row => row.Value);
        var actual=Rows().Sum(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Takeの戻り値IEnumerable<TSource>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void Take_count_NoDelegate() {
        Take_count_NoDelegate_Impl();
    }

    private static void Take_count_NoDelegate_Impl() {
        var expected=SystemEnumerable.Take(Source(),3);
        var actual=Source().Take(3);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: ToArrayの戻り値TSource[]は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void ToArray_NoDelegate() {
        ToArray_NoDelegate_Impl();
    }

    private static void ToArray_NoDelegate_Impl() {
        var expected=SystemEnumerable.ToArray(Source());
        var actual=Source().ToArray();
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: ToListの戻り値List<TSource>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void ToList_NoDelegate() {
        ToList_NoDelegate_Impl();
    }

    private static void ToList_NoDelegate_Impl() {
        var expected=SystemEnumerable.ToList(Source());
        var actual=Source().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    // インターセプトされるメソッド: Where_predicate_StaticDelegate_Impl()
    // 理由: Where Fragment と private アサート呼び出しの反射フォワードにより、
    //       ローカルメソッド本体を再生成できるため
    public void Where_predicate_StaticDelegate() {
        var generated = AssertInterceptorGenerated();
        Assert.Contains(nameof(Where_predicate_StaticDelegate_Impl),generated);
        Where_predicate_StaticDelegate_Impl();
    }

    private static void Where_predicate_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Where(Source(),static value => value>2);
        var actual=Source().Where(static value => value>2);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: インスタンスデリゲートはラムダ式ではないためIsInlineDelegateTypeに該当しnullを返す
    [Fact, Trait("Category","Unit")]
    public void Where_predicate_InstanceDelegate() {
        Where_predicate_InstanceDelegate_Impl();
    }

    private void Where_predicate_InstanceDelegate_Impl() {
        var expected=SystemEnumerable.Where(Source(),this.InstancePredicate);
        var actual=Source().Where(this.InstancePredicate);
        AssertSequenceEqual(expected,actual);
    }

    // インターセプトされるメソッド: なし
    // 理由: Zipの戻り値IEnumerable<TResult>は型パラメータを含むため、
    //       ContainsTypeParameterチェック(107行目)でnullを返す
    [Fact, Trait("Category","Unit")]
    public void Zip_second_resultSelector_StaticDelegate() {
        Zip_second_resultSelector_StaticDelegate_Impl();
    }

    private static void Zip_second_resultSelector_StaticDelegate_Impl() {
        var expected=SystemEnumerable.Zip(Source(),Second(),static (first,second) => first+second);
        var actual=Source().Zip(Second(),static (first,second) => first+second);
        AssertSequenceEqual(expected,actual);
    }

    private bool InstancePredicate(int value) => value>this.instanceOffset;
    private int InstanceSelector(int value) => value*this.instanceOffset;
    private int InstanceKeySelector(int value) => value;
    private int InstanceResultSelector(int outerValue,int innerValue) => outerValue+innerValue+this.instanceOffset-1;

    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
    public static int[] Third() => [10,20,30];
    public static int[] Duplicates() => [1,2,2,3,3,4];
    public static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    public static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    public sealed record Row(int Key,int Value,string Name);

    private static void AssertSequenceEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual) {
        Assert.Equal(expected,actual);
    }

    private static void AssertNestedSequenceEqual<T>(IEnumerable<IEnumerable<T>> expected,IEnumerable<IEnumerable<T>> actual) {
        using var expectedEnumerator = expected.GetEnumerator();
        using var actualEnumerator = actual.GetEnumerator();
        while(true) {
            var hasExpected = expectedEnumerator.MoveNext();
            var hasActual = actualEnumerator.MoveNext();
            Assert.Equal(hasExpected,hasActual);
            if(!hasExpected) {
                return;
            }

            Assert.Equal(expectedEnumerator.Current,actualEnumerator.Current);
        }
    }

    private static void AssertGroupSequenceEqual<TKey,TElement>(IEnumerable<IGrouping<TKey,TElement>> expected,IEnumerable<IGrouping<TKey,TElement>> actual) {
        using var expectedEnumerator = expected.GetEnumerator();
        using var actualEnumerator = actual.GetEnumerator();
        while(true) {
            var hasExpected = expectedEnumerator.MoveNext();
            var hasActual = actualEnumerator.MoveNext();
            Assert.Equal(hasExpected,hasActual);
            if(!hasExpected) {
                return;
            }

            Assert.Equal(expectedEnumerator.Current.Key,actualEnumerator.Current.Key);
            Assert.Equal(expectedEnumerator.Current,actualEnumerator.Current);
        }
    }

    private static string AssertInterceptorGenerated([global::System.Runtime.CompilerServices.CallerMemberName] string methodName = "") {
        var generated = GeneratedInterceptorAssert.SourceFor<EnumerableMethod1>(methodName);
        Assert.Contains(methodName,generated);
        return generated;
    }
}

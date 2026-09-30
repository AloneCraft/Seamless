namespace Test.System.Linq.Intercept;

// Range を入力にした終端の展開が、呼び出し元のメソッドの中でそのまま書ける(コンパイルできる)コードになること。
// 2026-09-30 の差分ファズで、次の 2 つが生成コードのコンパイルエラー(CS0103)になるのを確認して直した。
//  - ElementAt の範囲外例外が nameof(index) を使い、呼び出し元に index という名前が無いと失敗する
//  - 述語の定数式(-2 など)を「不変式の巻き上げ」の対象にし、定義していない __hoistN を参照する
public sealed class RangeInline {
    [Fact, Trait("Category","Unit")]
    public void ElementAt_OnRange_CompilesAndMatchesSystemLinq() {
        Assert.Equal(ElementAt_NoOptimized(10),ElementAt_Optimized(10));
        Assert.Equal(Assert.Throws<ArgumentOutOfRangeException>(() => ElementAt_NoOptimized(3)).ParamName,Assert.Throws<ArgumentOutOfRangeException>(() => ElementAt_Optimized(3)).ParamName);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int ElementAt_Optimized(int count) => global::System.Linq.Enumerable.Range(0,count).ElementAt(6);

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int ElementAt_NoOptimized(int count) => global::System.Linq.Enumerable.Range(0,count).ElementAt(6);

    [Fact, Trait("Category","Unit")]
    public void Predicate_WithNegativeConstant_CompilesAndMatchesSystemLinq() {
        Assert.Equal(AnyNegative_NoOptimized(5),AnyNegative_Optimized(5));
        Assert.Equal(AnyNegativeBelow_NoOptimized(5),AnyNegativeBelow_Optimized(5));
        Assert.Equal(CountNegative_NoOptimized(9),CountNegative_Optimized(9));
        Assert.Equal(AllNegative_NoOptimized(0),AllNegative_Optimized(0));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool AnyNegative_Optimized(int count) => global::System.Linq.Enumerable.Range(0,count).Any(x => x>=-2);

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static bool AnyNegative_NoOptimized(int count) => global::System.Linq.Enumerable.Range(0,count).Any(x => x>=-2);

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool AnyNegativeBelow_Optimized(int count) => global::System.Linq.Enumerable.Range(0,count).Any(x => x< -5);

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static bool AnyNegativeBelow_NoOptimized(int count) => global::System.Linq.Enumerable.Range(0,count).Any(x => x< -5);

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int CountNegative_Optimized(int count) => global::System.Linq.Enumerable.Range(0,count).Count(x => x>-3);

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int CountNegative_NoOptimized(int count) => global::System.Linq.Enumerable.Range(0,count).Count(x => x>-3);

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool AllNegative_Optimized(int count) => global::System.Linq.Enumerable.Range(0,count).All(x => x>=-1);

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static bool AllNegative_NoOptimized(int count) => global::System.Linq.Enumerable.Range(0,count).All(x => x>=-1);
}

namespace Test.System.Linq.Intercept;

/// <summary>
/// ExceptBy / IntersectBy の第2引数は「要素」ではなく「キー」の列で、System.Linq はキー選択子を第2引数に適用しない。
/// 要素の型とキーの型が同じとき(int と int など)も、第2引数の値をそのままキーとして使う。
/// 各テストの式は [LinqIntercept(true)] を付けた private static のメソッドへ置く(付けないと FLinq ではなく System.Linq が実行される)。
/// </summary>
public sealed class SetByKeySelectorOnSecond {
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ExceptByConsumeImpl() {
        var sink = new Consumer<int>();
        new[] { 1,2,3,14 }.ExceptBy(new[] { 14 },static v => v%10).Consume(sink);
        return sink.Items.ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void ExceptByConsumeUsesSecondAsKeys() => Assert.Equal([1,2,3,14],ExceptByConsumeImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ExceptByToArrayImpl() => new[] { 1,2,3,14 }.ExceptBy(new[] { 14 },static v => v%10).ToArray();
    [Fact, Trait("Category","Unit")]
    public void ExceptByToArrayUsesSecondAsKeys() => Assert.Equal([1,2,3,14],ExceptByToArrayImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] IntersectByConsumeImpl() {
        var sink = new Consumer<int>();
        new[] { 1,2,3,14 }.IntersectBy(new[] { 14 },static v => v%10).Consume(sink);
        return sink.Items.ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void IntersectByConsumeUsesSecondAsKeys() => Assert.Empty(IntersectByConsumeImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] IntersectByToArrayImpl() => new[] { 1,2,3,14 }.IntersectBy(new[] { 4 },static v => v%10).ToArray();
    [Fact, Trait("Category","Unit")]
    public void IntersectByToArrayUsesSecondAsKeys() => Assert.Equal([14],IntersectByToArrayImpl());
}

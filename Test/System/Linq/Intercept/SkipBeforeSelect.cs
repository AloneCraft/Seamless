namespace Test.System.Linq.Intercept;

/// <summary>
/// System.Linq は、配列・List を直接 Select した後の Skip で、飛ばす要素の選択子を呼ばない。
/// 選択子が 0 除算になる要素を飛ばす形で、FLinq も同じ結果になる(以前は、融合したループが飛ばす要素にも選択子を呼んで例外になった)。
/// それ以外の源(IEnumerable)では System.Linq も飛ばす要素に選択子を呼ぶので、FLinq も同じ例外になる。
/// </summary>
public sealed class SkipBeforeSelect {
    private static IEnumerable<int> Lazy(params int[] values) {
        foreach(var value in values) {
            yield return value;
        }
    }

    [Fact, Trait("Category","Unit")]
    public void ArraySelectSkipTakeConsumeDoesNotEvaluateSkippedElement() {
        var sink = new Consumer<int>();
        new[] { 1,2,3 }.Select(static v => 10/(v-1)).Skip(1).Take(2).Consume(sink);
        Assert.Equal([10,5],sink.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void ListSelectSkipToArrayDoesNotEvaluateSkippedElement() =>
        Assert.Equal([10,5],new List<int> { 1,2,3 }.Select(static v => 10/(v-1)).Skip(1).ToArray());

    [Fact, Trait("Category","Unit")]
    public void EnumerableSelectSkipEvaluatesSkippedElementLikeSystemLinq() =>
        Assert.Throws<DivideByZeroException>(() => Lazy(1,2,3).Select(static v => 10/(v-1)).Skip(1).ToArray());

    [Fact, Trait("Category","Unit")]
    public void ArrayWhereSelectSkipEvaluatesSkippedElementLikeSystemLinq() =>
        Assert.Throws<DivideByZeroException>(() => new[] { 1,2,3 }.Where(static v => true).Select(static v => 10/(v-1)).Skip(1).ToArray());
}

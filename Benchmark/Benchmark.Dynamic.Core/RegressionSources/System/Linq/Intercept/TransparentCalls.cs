namespace Test.System.Linq.Intercept;

/// <summary>
/// FLinqTransparentAttribute(名前で見分ける)が付いた静的メソッドは、最後の引数をそのまま返す「恒等の包み」とみなされる。
/// ラムダの本体がこの包みで囲まれていても、本体の形を見る最適化(恒等の Select を除く・SelectMany の内側の Select を除く)が効く。
/// 評価回数の検査(Benchmark.Dynamic の EvaluationProbe.Hit)が、本体を包んでも同じ最適化を見るためのもの。
/// 各テストの式は [LinqIntercept(true)] を付けた private static のメソッドへ置く(付けないと FLinq ではなく System.Linq が実行される)。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class FLinqTransparentAttribute:Attribute {
}

internal static class TransparentProbe {
    internal static int wraps;
    internal static int calls;

    [FLinqTransparent]
    internal static T Wrap<T>(int lambda,T value) {
        wraps++;
        return value;
    }

    internal static int Count(int value) {
        calls++;
        return value;
    }

    internal static int NotTransparent<T>(int lambda,T value) => 0;
}

public sealed class TransparentCalls {
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] WrappedIdentitySelectImpl() {
        TransparentProbe.wraps=0;
        return new[] { 1,2,3 }.Select(static x => TransparentProbe.Wrap(0,x)).Where(static x => x>1).ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void WrappedIdentitySelectIsRemoved() {
        Assert.Equal([2,3],WrappedIdentitySelectImpl());
        Assert.Equal(0,TransparentProbe.wraps);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool WrappedSelectManyAnyImpl() {
        TransparentProbe.calls=0;
        var inner = new[] { 1,2,3 };
        return new[] { 10,20 }.SelectMany(o => TransparentProbe.Wrap(1,inner.Select(i => TransparentProbe.Count(o+i)))).Any();
    }
    [Fact, Trait("Category","Unit")]
    public void WrappedSelectManyAnyDropsInnerSelector() {
        Assert.True(WrappedSelectManyAnyImpl());
        Assert.Equal(0,TransparentProbe.calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] OrdinaryCallIsNotIdentityImpl() => new[] { 1,2,3 }.Select(static x => TransparentProbe.NotTransparent(0,x)).ToArray();
    [Fact, Trait("Category","Unit")]
    public void CallWithoutAttributeIsNotIdentity() => Assert.Equal([0,0,0],OrdinaryCallIsNotIdentityImpl());
}

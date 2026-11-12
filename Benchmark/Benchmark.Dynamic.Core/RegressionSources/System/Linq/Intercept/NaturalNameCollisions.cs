namespace Test.System.Linq.Intercept;

/// <summary>
/// 生成コードの名前は item・enumerator・count・span のような自然な名前になる。
/// 利用者のコードが同じ名前の変数・引数・ラムダの引数を持っていても、名前がぶつからずに同じ結果になる。
/// </summary>
public sealed class NaturalNameCollisions {
    private static int[] Numbers() => [5,3,8,3,1,8,2];

    [Fact, Trait("Category","Unit")]
    public void LocalsNamedLikeGeneratedNames() {
        var item = 2;
        var enumerator = 1;
        var count = 3;
        var span = 4;
        var source = Numbers();
        var list = source.Where(x => x>item).Select(x => x+enumerator).Distinct().OrderBy(x => x).Take(count+span).ToList();
        Assert.Equal([4,6,9],list);
        Assert.Equal(100,source.Sum(x => x+item+enumerator+count+span));
    }

    [Fact, Trait("Category","Unit")]
    public void LocalsNamedLikeGeneratedResultAndIndexNames() {
        var result = 1;
        var index = 2;
        var destination = 3;
        var arrayResult = 4;
        var listResult = 5;
        var sumResult = 6;
        var source = Numbers();
        Assert.Equal([3,8,3,1,8,2],source.Skip(result).ToArray());
        Assert.Equal([5,3,8],source.Take(destination).ToList());
        Assert.Equal(30+index+arrayResult+listResult+sumResult,source.Sum()+index+arrayResult+listResult+sumResult);
    }

    [Fact, Trait("Category","Unit")]
    public void LambdaParametersNamedLikeGeneratedNames() {
        var source = Numbers();
        var groups = source.GroupBy(item => item%3,(key,items) => key*100+items.Count()).OrderBy(count => count).ToArray();
        Assert.Equal([2,101,204],groups);
    }

    [Fact, Trait("Category","Unit")]
    public void LambdaParametersNamedLikeGeneratedNamesInChain() {
        var source = Numbers();
        var values = source.Select(enumerator => enumerator*2).Where(span => span>4).Select(current => current+1).ToArray();
        Assert.Equal([11,7,17,7,17],values);
    }
}

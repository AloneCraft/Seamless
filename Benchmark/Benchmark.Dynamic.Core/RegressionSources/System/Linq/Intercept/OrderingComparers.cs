namespace Test.System.Linq.Intercept;

public sealed class OrderingComparers{
    private static readonly IComparer<int> Reverse=Comparer<int>.Create(static(left,right)=>right.CompareTo(left));
    [global::Seamless.FLinq.LinqIntercept(true)]private static int[] Defaults(IEnumerable<int> rows)=>rows.OrderBy(static value=>value%3).ThenBy(static value=>value%5).ToArray();
    [global::Seamless.FLinq.LinqIntercept(true)]private static int[] CustomFirst(IEnumerable<int> rows)=>rows.OrderBy(static value=>value%3,Reverse).ThenBy(static value=>value%5).ToArray();
    [global::Seamless.FLinq.LinqIntercept(true)]private static int[] CustomSecond(IEnumerable<int> rows)=>rows.OrderBy(static value=>value%3).ThenBy(static value=>value%5,Reverse).ToArray();
    [global::Seamless.FLinq.LinqIntercept(true)]private static int[] Both(IEnumerable<int> rows)=>rows.OrderBy(static value=>value%3,Reverse).ThenBy(static value=>value%5,Reverse).ToArray();
    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Expected(IEnumerable<int> rows,IComparer<int> first,IComparer<int> second)=>rows.OrderBy(static value=>value%3,first).ThenBy(static value=>value%5,second).ToArray();
    [Fact]public void KeepsMixedComparers(){
        var rows=Enumerable.Range(0,24).Select(static value=>value*7%19).ToArray();
        Assert.Equal(Expected(rows,Comparer<int>.Default,Comparer<int>.Default),Defaults(rows));
        Assert.Equal(Expected(rows,Reverse,Comparer<int>.Default),CustomFirst(rows));
        Assert.Equal(Expected(rows,Comparer<int>.Default,Reverse),CustomSecond(rows));
        Assert.Equal(Expected(rows,Reverse,Reverse),Both(rows));
    }
}

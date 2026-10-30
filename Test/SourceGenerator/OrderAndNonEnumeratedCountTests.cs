using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Test.SourceGenerator;

public sealed class OrderAndNonEnumeratedCountTests {
    [Fact]
    public void Order_And_OrderDescending_Optimize_And_Produce_Correct_Result(){
        var raw = new[] { 5, 2, 8, 1, 9, 3 };

        var sortedAsc = RunOrder(raw);
        var sortedDesc = RunOrderDescending(raw);

        Assert.Equal(new[] { 1, 2, 3, 5, 8, 9 },sortedAsc);
        Assert.Equal(new[] { 9, 8, 5, 3, 2, 1 },sortedDesc);
    }

    [Fact]
    public void TryGetNonEnumeratedCount_Resolves_Fast_And_Accurate(){
        var raw = new[] { 10, 20, 30, 40 };
        var success = RunTryGetCount(raw,out var count);

        Assert.True(success);
        Assert.Equal(4,count);
    }

    private static int[] RunOrder(int[] source){
        return source.Order().ToArray();
    }

    private static int[] RunOrderDescending(int[] source){
        return source.OrderDescending().ToArray();
    }

    private static bool RunTryGetCount(IEnumerable<int> source,out int count){
        return source.TryGetNonEnumeratedCount(out count);
    }
}

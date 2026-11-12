using Seamless.Linq.Internal;
using Xunit;

namespace Benchmark.Dynamic.Tests;

public class TopKBufferTests {
    private readonly struct AscendingIntComparer : IComparer<int> {
        public int Compare(int x, int y) => x.CompareTo(y);
    }

    private readonly struct DescendingIntComparer : IComparer<int> {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    private readonly struct CombinedAscendingComparer : ITopKCombinedComparer<int, int> {
        public int Compare(int lp, int ls, int rp, int rs) {
            var c = lp.CompareTo(rp);
            return c != 0 ? c : ls.CompareTo(rs);
        }
    }

    [Fact]
    public void TopKBuffer_Ascending_ExtractsSmallestK() {
        var items = new[] { 50, 20, 80, 10, 30, 90, 40, 70, 60 };
        using var buffer = new TopKBuffer<int, int, AscendingIntComparer>(3, default);
        foreach (var item in items) {
            buffer.Push(item, item);
        }

        Assert.Equal(3, buffer.Count);
        Assert.True(buffer.TryGetSpan(out var span));
        Assert.Equal([10, 20, 30], span.ToArray());
    }

    [Fact]
    public void TopKBuffer_Descending_ExtractsLargestK() {
        var items = new[] { 50, 20, 80, 10, 30, 90, 40, 70, 60 };
        using var buffer = new TopKBuffer<int, int, DescendingIntComparer>(3, default);
        foreach (var item in items) {
            buffer.Push(item, item);
        }

        Assert.Equal(3, buffer.Count);
        Assert.True(buffer.TryGetSpan(out var span));
        Assert.Equal([90, 80, 70], span.ToArray());
    }

    [Fact]
    public void TopKBuffer_StableSort_PreservesOriginalOrderForEqualKeys() {
        var items = new (int Key, string Tag)[] {
            (2, "A"), (1, "B1"), (3, "C"), (1, "B2"), (1, "B3"), (2, "D")
        };
        using var buffer = new TopKBuffer<(int Key, string Tag), int, AscendingIntComparer>(3, default);
        foreach (var item in items) {
            buffer.Push(item, item.Key);
        }

        Assert.Equal(3, buffer.Count);
        Assert.True(buffer.TryGetSpan(out var span));
        var result = span.ToArray();
        Assert.Equal("B1", result[0].Tag);
        Assert.Equal("B2", result[1].Tag);
        Assert.Equal("B3", result[2].Tag);
    }

    [Fact]
    public void TopKBuffer_BoundaryConditions_HandledCorrectly() {
        // k = 0
        using (var buffer0 = new TopKBuffer<int, int, AscendingIntComparer>(0, default)) {
            buffer0.Push(10, 10);
            Assert.Equal(0, buffer0.Count);
            Assert.False(buffer0.TryGetNext(out _));
        }

        // k > N
        using (var bufferMore = new TopKBuffer<int, int, AscendingIntComparer>(10, default)) {
            bufferMore.Push(3, 3);
            bufferMore.Push(1, 1);
            Assert.Equal(2, bufferMore.Count);
            Assert.True(bufferMore.TryGetSpan(out var span));
            Assert.Equal([1, 3], span.ToArray());
        }
    }

    [Fact]
    public void TopKCombinedBuffer_ExtractsTopKWithMultipleKeys() {
        var items = new[] {
            (Pri: 10, Sec: 200, Val: "A"),
            (Pri: 20, Sec: 100, Val: "B"),
            (Pri: 10, Sec: 150, Val: "C"),
            (Pri: 5,  Sec: 300, Val: "D"),
            (Pri: 20, Sec: 50,  Val: "E"),
        };
        using var buffer = new TopKCombinedBuffer<(int Pri, int Sec, string Val), int, int, CombinedAscendingComparer>(3, default);
        foreach (var item in items) {
            buffer.Push(item, item.Pri, item.Sec);
        }

        Assert.Equal(3, buffer.Count);
        Assert.True(buffer.TryGetSpan(out var span));
        var res = span.ToArray();
        Assert.Equal("D", res[0].Val); // Pri: 5
        Assert.Equal("C", res[1].Val); // Pri: 10, Sec: 150
        Assert.Equal("A", res[2].Val); // Pri: 10, Sec: 200
    }
}

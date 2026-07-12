namespace Test.BenchmarkWorker;

public sealed class BenchmarkFormattingTests {
    [Fact]
    public void FormatNanoseconds_UsesGreekMicroSign() {
        Assert.Equal("332.11 μs",global::UI.BenchmarkFormatting.FormatNanoseconds(332_110));
    }
}

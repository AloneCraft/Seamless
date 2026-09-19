namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerImportSizerTests {
    [Theory,Trait("Category","Unit")]
    [InlineData(0,0,4096,64)]
    [InlineData(5000,1000,16384,1024)]
    [InlineData(100000,3308,131072,4096)]
    public void Calculate_SelectsPowerOfTwoStoreSizes(
        long maximumRowBytes,
        long maximumTableRows,
        long expectedPageSize,
        long expectedIndexSize) {
        var sizing=global::Seamless.Database.SqlServer.SqlServerImportSizer.Calculate(
            maximumRowBytes,maximumTableRows);

        Assert.Equal(expectedPageSize,sizing.PageSize);
        Assert.Equal(expectedPageSize,sizing.SegmentSize);
        Assert.Equal(expectedIndexSize,sizing.IndexSize);
        Assert.Equal(sizing.PageSize*2,sizing.MemorySize);
    }
}

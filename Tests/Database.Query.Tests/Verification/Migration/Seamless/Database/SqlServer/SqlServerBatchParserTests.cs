namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerBatchParserTests {
    [Fact,Trait("Category","Unit")]
    public void Split_RecognizesGoLinesCommentsAndCounts() {
        var batches=global::Seamless.Database.SqlServer.SqlServerBatchParser.Split("""
            SELECT 1;
            GO -- first
            SELECT 2;
            go 2
            """);

        Assert.Equal(3,batches.Count);
        Assert.Equal("SELECT 1;",batches[0]);
        Assert.Equal("SELECT 2;",batches[1]);
        Assert.Equal(batches[1],batches[2]);
    }

    [Fact,Trait("Category","Unit")]
    public void Split_DoesNotTreatGoInsideSqlAsSeparator() {
        var batches=global::Seamless.Database.SqlServer.SqlServerBatchParser.Split("SELECT 'GO';");

        Assert.Equal(["SELECT 'GO';"],batches);
    }
}

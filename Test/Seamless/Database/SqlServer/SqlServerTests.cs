namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerTests {
    [Fact,Trait("Category","Unit")]
    public void SYSDATETIME_ReturnsCurrentLocalDateTimeWithoutTimeZone() {
        var before=global::System.DateTime.Now;

        var actual=global::Seamless.Database.SqlServer.SqlServer.SYSDATETIME();

        var after=global::System.DateTime.Now;
        Assert.Equal(global::System.DateTimeKind.Unspecified,actual.Kind);
        Assert.InRange(actual,before,after);
    }
}

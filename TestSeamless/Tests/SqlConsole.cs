namespace TestSeamless.Tests;
public sealed class SqlConsole{
    [Fact]public void ValidateSourceSql(){
        global::TestSeamless.SqlConsole.ValidateSourceSql("Northwind","SELECT COUNT(*) FROM dbo.Orders");
        Assert.Throws<InvalidOperationException>(()=>global::TestSeamless.SqlConsole.ValidateSourceSql("Northwind","DELETE FROM dbo.Orders"));
        global::TestSeamless.SqlConsole.ValidateSourceSql("Seamless_Comparison_Fixture","BEGIN TRAN; INSERT dbo.FixtureParent(Id,Name) VALUES(3,N'x'); ROLLBACK;");
        Assert.Throws<InvalidOperationException>(()=>global::TestSeamless.SqlConsole.ValidateSourceSql("Seamless_Comparison_Fixture","DELETE FROM Northwind.dbo.Orders"));
        Assert.Throws<InvalidOperationException>(()=>global::TestSeamless.SqlConsole.ValidateSourceSql("Seamless_Comparison_Fixture","SELECT * FROM [remote].[Northwind].dbo.Orders"));
        Assert.Throws<InvalidOperationException>(()=>global::TestSeamless.SqlConsole.ValidateSourceSql("Northwind","SELECT * INTO dbo.NewOrders FROM dbo.Orders"));
    }
}

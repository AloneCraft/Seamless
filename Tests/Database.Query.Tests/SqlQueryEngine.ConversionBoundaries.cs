using Seamless.Database.Query;

namespace Database.Query.Tests;

public sealed partial class SqlQueryEngine{
    [Fact]
    public void UniqueIdentifierConversionUsesSqlTextTruncationAndBinaryLayout(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());var expected=Guid.Parse("12345678-1234-4321-abcd-123456789012");
        foreach(var expression in new[]{"CONVERT(uniqueidentifier,N'12345678-1234-4321-abcd-123456789012extra')","CONVERT(uniqueidentifier,N'{12345678-1234-4321-abcd-123456789012}')","CAST(0x7856341234122143ABCD123456789012 AS uniqueidentifier)"}){Assert.Equal(expected,Assert.Single(engine.Prepare("SELECT "+expression,"Original DB").Execute().Rows)[0]);}
        foreach(var value in new[]{"1234567812344321abcd123456789012"," 12345678-1234-4321-abcd-123456789012 "}){Assert.Equal(8169,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT CONVERT(uniqueidentifier,N'"+value+"')","Original DB").Execute().Rows.ToArray()).Number);}
        Assert.Equal(529,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT CONVERT(uniqueidentifier,1)","Original DB").Execute().Rows.ToArray()).Number);
    }
    [Fact]
    public void IntegerBinaryConversionsUseNativeWidthsAndLeftPadding(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());var row=Assert.Single(engine.Prepare("SELECT CAST(264 AS binary(2)),CAST(264 AS binary(6)),CAST(264 AS varbinary(6)),CAST(-1 AS binary(8))","Original DB").Execute().Rows);
        Assert.Equal(new byte[]{1,8},Assert.IsType<byte[]>(row[0]));Assert.Equal(new byte[]{0,0,0,0,1,8},Assert.IsType<byte[]>(row[1]));Assert.Equal(new byte[]{0,0,1,8},Assert.IsType<byte[]>(row[2]));Assert.Equal(new byte[]{0,0,0,0,255,255,255,255},Assert.IsType<byte[]>(row[3]));
    }
    [Fact]
    public void MoneyAndDateBoundariesMatchSqlServer(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());var row=Assert.Single(engine.Prepare("SELECT CAST(-922337203685477.5808 AS money),CAST(214748.3647 AS smallmoney),CAST(-214748.3648 AS smallmoney),CAST(1.23455 AS money),CAST(-1.23455 AS smallmoney),CONVERT(date,'0001-01-01'),CONVERT(date,'9999-12-31')","Original DB").Execute().Rows);
        Assert.Equal(new object?[]{-922337203685477.5808m,214748.3647m,-214748.3648m,1.2346m,-1.2346m,DateOnly.MinValue,DateOnly.MaxValue},row);
        Assert.Equal(8115,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT CAST(922337203685477.5808 AS money)","Original DB").Execute().Rows.ToArray()).Number);
    }
}

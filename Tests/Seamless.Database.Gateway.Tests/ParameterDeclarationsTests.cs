using System.Reflection;
using System.Data;
using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Query;
using SqlParameter=Seamless.Database.Query.SqlParameter;

namespace Seamless.Database.Gateway.Tests;

public sealed class ParameterDeclarationsTests{
    [Fact,Trait("Category","Integration")]
    public async Task SqlClientPreparedRpcUsesMultipleDeclaredTypes(){
        using var database=new RuntimeSqlGeneratedRoot(DatabaseContainerOptions.InMemory with{Transactions=DatabaseTransactionMode.SharedV2});
        var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("fixture",adapter);
        using var server=new TdsServer(35482,adapter,new SqlExecutionEngine(registry));server.Start();
        var options=new SqlConnectionStringBuilder{DataSource="127.0.0.1,35482",InitialCatalog="fixture",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};
        using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
        command.CommandText="SELECT @id AS n, @name AS s, @amount AS amount";
        command.Parameters.Add("@id",SqlDbType.Int).Value=7;command.Parameters.Add("@name",SqlDbType.NVarChar,40).Value="hello";
        var amount=command.Parameters.Add("@amount",SqlDbType.Decimal);amount.Precision=12;amount.Scale=2;amount.Value=12.34m;
        await command.PrepareAsync();using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(7,reader.GetInt32(0));Assert.Equal("hello",reader.GetString(1));Assert.Equal(12.34m,reader.GetDecimal(2));Assert.False(await reader.ReadAsync());
    }
    private static IReadOnlyList<SqlParameter> Parse(string text){
        var type=typeof(TdsServer).Assembly.GetType("Seamless.Database.Gateway.Tds.ParameterDeclarations",true)!;
        try{return (IReadOnlyList<SqlParameter>)type.GetMethod("Parse",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[text])!;}
        catch(TargetInvocationException error) when(error.InnerException is not null){global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
    }
    [Fact]
    public void SupportedDeclarationsRetainTypeAttributesAndOrder(){
        var values=Parse("@a int, @b nvarchar(42), @c decimal(12,2), @d varbinary(max), @e datetime2(7), @f float(53), @g uniqueidentifier");
        Assert.Equal(new[]{"@a","@b","@c","@d","@e","@f","@g"},values.Select(p=>p.Name));
        Assert.Equal(typeof(int),values[0].Type.ClrType);Assert.Equal(42,values[1].Type.Length);
        Assert.Equal((byte)12,values[2].Type.Precision);Assert.Equal((byte)2,values[2].Type.Scale);
        Assert.Equal(-1,values[3].Type.Length);Assert.Equal((byte)7,values[4].Type.Scale);Assert.Equal(53,values[5].Type.Length);Assert.Equal(typeof(double),values[5].Type.ClrType);
        Assert.Equal(typeof(Guid),values[6].Type.ClrType);
        Assert.Empty(Parse(" "));var unicode=Parse("@名前 /*comment*/ int, @識別値2 nvarchar(8)");Assert.Equal(new[]{"@名前","@識別値2"},unicode.Select(p=>p.Name));
    }
    [Fact]
    public void RejectsLegacyAcceptedDuplicateAssignmentAndTrailingStatement(){
        foreach(var text in new[]{"@x int,@X bigint","@x int = 1","@x int; SELECT 1"}){
            var parser=new global::Microsoft.SqlServer.TransactSql.ScriptDom.TSql160Parser(true);
            _=parser.Parse(new StringReader("DECLARE "+text+";"),out var errors);
            Assert.Empty(errors);Assert.Throws<SqlQueryException>(()=>Parse(text));
        }
        foreach(var text in new[]{"@x int OUT","@x int OUTPUT"}){
            var parser=new global::Microsoft.SqlServer.TransactSql.ScriptDom.TSql160Parser(true);
            _=parser.Parse(new StringReader("DECLARE "+text+";"),out var errors);
            Assert.NotEmpty(errors);Assert.Equal(40515,Assert.Throws<SqlQueryException>(()=>Parse(text)).Number);
        }
    }
    [Theory]
    [InlineData("@x int OUTPUT",40515)]
    [InlineData("@x int OUT,@y int",40515)]
    [InlineData("@x foobar",40515)]
    [InlineData("@x int,@X bigint",134)]
    [InlineData("@x decimal(39,2)",102)]
    [InlineData("@x decimal(4,5)",102)]
    [InlineData("@x nvarchar(4001)",102)]
    [InlineData("@x float(1)",40515)]
    [InlineData("@x float(24)",40515)]
    [InlineData("@x float(25)",40515)]
    [InlineData("@x float(52)",40515)]
    [InlineData("@x float(0)",102)]
    [InlineData("@x float(54)",102)]
    [InlineData("@x int(2)",102)]
    [InlineData("@x int; SELECT 1",102)]
    [InlineData("@x int, ",102)]
    [InlineData("@x int = 1",102)]
    public void RejectUnsupportedInvalidAndUnconsumedInput(string text,int number){Assert.Equal(number,Assert.Throws<SqlQueryException>(()=>Parse(text)).Number);}
}

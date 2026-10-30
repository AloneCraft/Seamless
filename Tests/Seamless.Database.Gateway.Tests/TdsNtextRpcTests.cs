using System.Reflection;
using System.Text;
using System.Data;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class TdsNtextRpcTests{
    private static readonly MethodInfo Parse=typeof(TdsServer).Assembly.GetType("Seamless.Database.Gateway.Tds.TdsRpcRequest",true)!.GetMethod("Parse")!;

    [Theory,Trait("Category","Unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("日本語😀")]
    public void NtextAndFollowingParameterAreDecoded(string? value){
        using var stream=Start();using var writer=new BinaryWriter(stream,Encoding.Unicode,true);
        WriteNtext(writer,"@stmt","SELECT @value");WriteNtext(writer,"@value",value);
        writer.Write((byte)2);writer.Write(Encoding.Unicode.GetBytes("@n"));writer.Write((byte)0);writer.Write((byte)0x38);writer.Write(42);
        var parameters=Read(stream.ToArray());Assert.Equal("SELECT @value",parameters[0].Value);Assert.Equal(value,parameters[1].Value);Assert.Equal(42,parameters[2].Value);
    }

    [Theory,Trait("Category","Unit")]
    [InlineData(-2,10)]
    [InlineData(3,10)]
    [InlineData(12,10)]
    [InlineData(16777218,16777218)]
    public void InvalidLengthsAreRejected(int length,int maximum){
        using var stream=Start();using var writer=new BinaryWriter(stream,Encoding.Unicode,true);
        writer.Write((byte)0);writer.Write((byte)0);writer.Write((byte)0x63);writer.Write(maximum);writer.Write(new byte[5]);writer.Write(length);
        var error=Assert.Throws<TargetInvocationException>(()=>Read(stream.ToArray()));Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact,Trait("Category","Unit")]
    public void TruncatedNtextIsRejected(){
        using var stream=Start();using var writer=new BinaryWriter(stream,Encoding.Unicode,true);
        writer.Write((byte)0);writer.Write((byte)0);writer.Write((byte)0x63);writer.Write(20);writer.Write(new byte[5]);writer.Write(4);writer.Write((ushort)65);
        var error=Assert.Throws<TargetInvocationException>(()=>Read(stream.ToArray()));Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact,Trait("Category","Integration")]
    public async Task SqlClientNtextAcrossPacketsPreservesFollowingParameter(){
        var adapter=new EmptyAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5,PacketSize=512};
        await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();
        using var command=connection.CreateCommand();command.CommandText="SELECT CAST(@value AS nvarchar(max)) AS value, @marker AS marker";var value=new string('日',6000)+"😀";
        command.Parameters.Add("@value",SqlDbType.NText).Value=value;command.Parameters.Add("@marker",SqlDbType.Int).Value=42;
        await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(value,reader.GetString(0));Assert.Equal(42,reader.GetInt32(1));Assert.False(await reader.ReadAsync());
    }

    [Fact,Trait("Category","Integration")]
    public async Task SqlClientExecutesNtextStatementThroughRpcHandler(){
        var adapter=new EmptyAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
        await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();
        using var command=connection.CreateCommand();command.CommandType=CommandType.StoredProcedure;command.CommandText="sp_executesql";
        command.Parameters.Add("@stmt",SqlDbType.NText).Value="SELECT @value";
        command.Parameters.Add("@params",SqlDbType.NVarChar,100).Value="@value int";
        command.Parameters.Add("@value",SqlDbType.Int).Value=42;
        Assert.Equal(42,Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    private sealed class EmptyAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata> GetTables()=>[];public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string n,IReadOnlyList<string>? c,Func<object,bool>? p,SessionContext s)=>throw new NotSupportedException();
        public int ExecuteInsert(string n,IReadOnlyDictionary<string,object?> v,SessionContext s)=>throw new NotSupportedException();
        public int ExecuteUpdate(string n,IReadOnlyDictionary<string,object?> v,Func<object,bool>? p,SessionContext s)=>throw new NotSupportedException();
        public int ExecuteDelete(string n,Func<object,bool>? p,SessionContext s)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext s)=>throw new NotSupportedException();public void CommitTransaction(SessionContext s)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext s)=>throw new NotSupportedException();
    }

    private static MemoryStream Start(){var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.Unicode,true);writer.Write(ushort.MaxValue);writer.Write((ushort)10);writer.Write((ushort)0);return stream;}
    private static void WriteNtext(BinaryWriter writer,string name,string? value){writer.Write((byte)name.Length);writer.Write(Encoding.Unicode.GetBytes(name));writer.Write((byte)0);writer.Write((byte)0x63);writer.Write(int.MaxValue);writer.Write(new byte[5]);if(value is null){writer.Write(-1);}else{var bytes=Encoding.Unicode.GetBytes(value);writer.Write(bytes.Length);writer.Write(bytes);}}
    private static IReadOnlyList<TdsRpcParameter> Read(byte[] payload){var request=Parse.Invoke(null,[payload])!;return (IReadOnlyList<TdsRpcParameter>)request.GetType().GetProperty("Parameters")!.GetValue(request)!;}
}

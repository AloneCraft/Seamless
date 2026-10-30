using System.Data.Common;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace TestSeamless.Tests;

internal sealed class ProtocolMetadataAdapter:IDatabaseAdapter{
    private static readonly ColumnMetadata[] Columns=[new("Japanese",typeof(string),"varchar",false,false,0,20,collation:"Japanese_CI_AS"),new("Western",typeof(string),"varchar",false,false,1,20,collation:"SQL_Latin1_General_CP1_CI_AS"),new("Optional",typeof(int),"int",true,false,2),new("Node",typeof(byte[]),"hierarchyid",true,false,3),new("Location",typeof(byte[]),"geography",true,false,4),new("Stamp",typeof(byte[]),"timestamp",false,false,5,8),new("Modern",typeof(string),"nvarchar",false,false,6,20,collation:"Latin1_General_100_CI_AS"),new("BinaryOrder",typeof(string),"nvarchar",false,false,7,20,collation:"Latin1_General_BIN2"),new("LegacyText",typeof(string),"text",true,false,8,collation:"Japanese_CI_AS"),new("LegacyNtext",typeof(string),"ntext",true,false,9,collation:"SQL_Latin1_General_CP1_CI_AS"),new("LegacyImage",typeof(byte[]),"image",true,false,10),new("EmptyText",typeof(string),"text",true,false,11,collation:"SQL_Latin1_General_CP1_CI_AS"),new("NullText",typeof(string),"text",true,false,12,collation:"SQL_Latin1_General_CP1_CI_AS"),new("NullImage",typeof(byte[]),"image",true,false,13),new("Shape",typeof(byte[]),"geometry",true,false,14)];
    private static readonly TableMetadata Table=new("dbo","Metadata",Columns);
    public string DatabaseName=>"first";
    public IReadOnlyList<TableMetadata> GetTables()=>[Table];
    public TableMetadata? FindTable(string name){if(name.Equals("dbo.Metadata",StringComparison.OrdinalIgnoreCase)||name.Equals("Metadata",StringComparison.OrdinalIgnoreCase)){return Table;}return null;}
    public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columnNames,Func<object,bool>? predicate,SessionContext session)=>new(Columns,[["日本語","café",null,new byte[]{0x58},new byte[]{1,2,3},new byte[]{0,0,0,0,0,0,0,1},"é","A","日本語","Unicode日本語",new byte[]{3,4,5},"",null,null,new byte[]{7,8}]],1);
    public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?> values,SessionContext session)=>throw new NotSupportedException();
    public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?> values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
    public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
    public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();
    public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();
    public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
}

[Collection("Protocol serial")]public sealed class ProtocolMetadata{
    [Fact]public void VarcharCollationAndNullability(){
        var adapter=new ProtocolMetadataAdapter();var registry=new DatabaseRegistry();registry.Register("first",adapter);using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();
        var settings=new SqlConnectionStringBuilder{DataSource="127.0.0.1,14943",InitialCatalog="first",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};using var connection=new SqlConnection(settings.ConnectionString);connection.Open();using var command=connection.CreateCommand();command.CommandText="SELECT * FROM dbo.Metadata";
        using var reader=command.ExecuteReader();var columns=reader.GetColumnSchema();Assert.Equal("varchar",columns[0].DataTypeName);Assert.False(columns[0].AllowDBNull);Assert.True(columns[2].AllowDBNull);Assert.True(reader.Read());Assert.Equal("日本語",reader.GetString(0));Assert.Equal("café",reader.GetString(1));Assert.True(reader.IsDBNull(2));Assert.Equal("first.sys.hierarchyid",reader.GetDataTypeName(3));Assert.Equal("first.sys.geography",reader.GetDataTypeName(4));var node=new byte[8];Assert.Equal(1,reader.GetBytes(3,0,node,0,node.Length));Assert.Equal(0x58,node[0]);var geo=new byte[8];Assert.Equal(3,reader.GetBytes(4,0,geo,0,geo.Length));Assert.Equal(new byte[]{1,2,3},geo[..3]);Assert.Equal("timestamp",reader.GetDataTypeName(5));Assert.Equal(new byte[]{0,0,0,0,0,0,0,1},(byte[])reader.GetValue(5));Assert.Equal("é",reader.GetString(6));Assert.Equal("A",reader.GetString(7));Assert.Equal("text",reader.GetDataTypeName(8));Assert.Equal(2147483647,columns[8].ColumnSize);Assert.Equal("日本語",reader.GetString(8));Assert.Equal("ntext",reader.GetDataTypeName(9));Assert.True(columns[9].ColumnSize==1073741823,$"ntext metadata: type={columns[9].DataTypeName}; size={columns[9].ColumnSize}; ordinal={columns[9].ColumnOrdinal}; package={Environment.GetEnvironmentVariable("SEAMLESS_PROTOCOL_PACKAGE_VERSION")}; gateway-version={typeof(TdsServer).Assembly.GetName().Version}; gateway-info={typeof(TdsServer).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion}; gateway-path={typeof(TdsServer).Assembly.Location}");Assert.Equal("Unicode日本語",reader.GetString(9));Assert.Equal("image",reader.GetDataTypeName(10));Assert.Equal(2147483647,columns[10].ColumnSize);Assert.Equal(new byte[]{3,4,5},(byte[])reader.GetValue(10));Assert.Equal("",reader.GetString(11));Assert.True(reader.IsDBNull(12));Assert.True(reader.IsDBNull(13));
    }
}

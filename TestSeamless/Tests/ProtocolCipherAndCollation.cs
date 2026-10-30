using System.Data.Common;
using System.Data;
using System.Text.Json;
using MemoryPack;
using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Rdbms;

namespace TestSeamless.Tests;

[MemoryPackable]public readonly partial record struct ProtocolCipherRow(int Id,byte[] Secret);
public sealed class ProtocolCipherDatabase:DatabaseContainer{
    [return:Seamless.Linq.UnorderedEnumerable]public Table<ProtocolCipherRow> GetRows()=>this.Table<ProtocolCipherRow>("Rows");
    public Table<ProtocolCipherRow> Rows{[return:Seamless.Linq.UnorderedEnumerable]get=>this.GetRows();}
}

[Collection("Protocol serial")]public sealed class ProtocolCipherAndCollation{
    private static SqlConnection Connect(){var settings=new SqlConnectionStringBuilder{DataSource="127.0.0.1,14943",InitialCatalog="first",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};var connection=new SqlConnection(settings.ConnectionString);connection.Open();return connection;}
    [Fact]public void EncryptedResultMetadata(){
        var type=JsonSerializer.Deserialize<DatabaseType>("""{"Kind":9,"IsNullable":true,"SqlTypeName":"nvarchar","SourceMaximumLength":22,"ResultSqlTypeName":"varbinary","ResultMaximumLength":81}""")!;
        using var database=new ProtocolCipherDatabase();database.Rows.Add(new(1,[1,2,3]));var bindings=new[]{new GeneratedTableBinding("Rows","dbo","Rows",[new("Id","Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int")),new("Secret","Secret",type)])};
        var adapter=new InProcessDatabaseAdapter(database,bindings);Assert.True(SystemCatalogEmulator.TryCatalog("columns",adapter,[],out var catalog));var secret=catalog.Rows.Single(row=>string.Equals(row[1] as string,"Secret",StringComparison.Ordinal));Assert.Equal((byte)231,secret[3]);Assert.Equal((short)22,secret[4]);var registry=new DatabaseRegistry();registry.Register("first",adapter);using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();using var connection=Connect();
        using(var metadata=connection.CreateCommand()){metadata.CommandText="SELECT system_type_id,max_length FROM sys.columns WHERE name=N'Secret'";using var schemaReader=metadata.ExecuteReader();Assert.True(schemaReader.Read());Assert.Equal((byte)231,schemaReader.GetByte(0));Assert.Equal((short)22,schemaReader.GetInt16(1));Assert.False(schemaReader.Read());}
        using var command=connection.CreateCommand();command.CommandText="SELECT Secret FROM dbo.Rows";using var reader=command.ExecuteReader();var schema=reader.GetColumnSchema();Assert.Equal("varbinary",schema[0].DataTypeName);Assert.Equal(81,schema[0].ColumnSize);Assert.True(reader.Read());Assert.Equal(new byte[]{1,2,3},(byte[])reader.GetValue(0));Assert.Equal("nvarchar",type.SqlTypeName);Assert.Equal<short?>(22,type.SourceMaximumLength);
    }
    [Fact]public void JapaneseVarcharRpcAndDatabaseSwitch(){
        var adapter=new ProtocolMetadataAdapter();var registry=new DatabaseRegistry();var register=typeof(DatabaseRegistry).GetMethod("Register")!;Assert.Equal(7,register.GetParameters().Length);
        register.Invoke(registry,["first",adapter,false,"",null,null,"Japanese_CI_AS"]);register.Invoke(registry,["second",adapter,false,"",null,null,"SQL_Latin1_General_CP1_CI_AS"]);
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();using var connection=Connect();
        void Verify(){using var command=connection.CreateCommand();command.CommandText="SELECT @value";var value=command.Parameters.Add("@value",SqlDbType.VarChar,20);value.LocaleId=1041;value.Value="日本語";Assert.Equal("日本語",command.ExecuteScalar());}
        Verify();using(var command=connection.CreateCommand()){command.CommandText="USE second";command.ExecuteNonQuery();command.CommandText="USE first";command.ExecuteNonQuery();}Verify();
    }
    [Fact]public void JapaneseCollationReturnsAfterPoolReset(){
        var adapter=new ProtocolMetadataAdapter();var registry=new DatabaseRegistry();var register=typeof(DatabaseRegistry).GetMethod("Register")!;
        register.Invoke(registry,["first",adapter,false,"",null,null,"Japanese_CI_AS"]);register.Invoke(registry,["second",adapter,false,"",null,null,"SQL_Latin1_General_CP1_CI_AS"]);
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();
        var settings=new SqlConnectionStringBuilder{DataSource="127.0.0.1,14943",InitialCatalog="first",UserID="sa",Password="test",Encrypt=false,Pooling=true,ConnectTimeout=5};
        using var connection=new SqlConnection(settings.ConnectionString);
        try{
            connection.Open();var clientId=connection.ClientConnectionId;
            using(var command=connection.CreateCommand()){command.CommandText="USE second";command.ExecuteNonQuery();}
            connection.Close();connection.Open();Assert.Equal(clientId,connection.ClientConnectionId);
            using(var command=connection.CreateCommand()){command.CommandText="SELECT DB_NAME()";Assert.Equal("first",command.ExecuteScalar());}
            using(var command=connection.CreateCommand()){command.CommandText="SELECT @value";command.Parameters.Add("@value",SqlDbType.VarChar,20).Value="日本語";Assert.Equal("日本語",command.ExecuteScalar());}
        }finally{SqlConnection.ClearPool(connection);}
    }
    [Fact]public void CatalogFingerprintPreservesOldDataAndIncludesNewMetadata(){
        var old=JsonSerializer.Deserialize<DatabaseCatalog>("""{"Name":"first","Schemas":[]}""")!;
        var oldRoundTrip=JsonSerializer.Deserialize<DatabaseCatalog>(JsonSerializer.Serialize(old))!;
        static string Fingerprint(DatabaseCatalog catalog)=>CSharpDatabaseGenerator.Generate(catalog).SchemaFingerprint;
        Assert.Equal("35BDF491F61088CFD874773D208657A2C10B86047CABFDB923610B3764C3D833",Fingerprint(old));
        Assert.Equal(Fingerprint(old),Fingerprint(oldRoundTrip));
        Assert.NotEqual(Fingerprint(old),Fingerprint(old with{DefaultCollation="Japanese_CI_AS"}));
        var baseType=new DatabaseType(DatabaseTypeKind.Binary,true,SqlTypeName:"nvarchar",SourceMaximumLength:22);
        static DatabaseCatalog WithType(DatabaseType type)=>new("first",[new SchemaCatalog("dbo",[new TableCatalog("Rows",[new ColumnCatalog(0,"Secret",type,false)],null)])]);
        var physical=WithType(baseType with{ResultSqlTypeName="varbinary",ResultMaximumLength=81});
        var restored=JsonSerializer.Deserialize<DatabaseCatalog>(JsonSerializer.Serialize(physical))!;
        Assert.Equal(Fingerprint(physical),Fingerprint(restored));
        Assert.NotEqual(Fingerprint(WithType(baseType)),Fingerprint(physical));
        Assert.NotEqual(Fingerprint(WithType(baseType)),Fingerprint(WithType(baseType with{ResultMaximumLength=81})));
        Assert.Equal(81,restored.Schemas[0].Tables[0].Columns[0].Type.ResultMaximumLength);
    }
    [Fact]public void OldCatalogTypesHaveCanonicalSqlNames(){
        foreach(var (kind,name) in new[]{(DatabaseTypeKind.Boolean,"bit"),(DatabaseTypeKind.Byte,"tinyint"),(DatabaseTypeKind.Int16,"smallint"),(DatabaseTypeKind.Int32,"int"),(DatabaseTypeKind.Int64,"bigint"),(DatabaseTypeKind.Decimal,"decimal"),(DatabaseTypeKind.Single,"real"),(DatabaseTypeKind.Double,"float"),(DatabaseTypeKind.String,"nvarchar"),(DatabaseTypeKind.Binary,"varbinary"),(DatabaseTypeKind.Guid,"uniqueidentifier"),(DatabaseTypeKind.Date,"date"),(DatabaseTypeKind.Time,"time"),(DatabaseTypeKind.DateTime,"datetime2"),(DatabaseTypeKind.DateTimeOffset,"datetimeoffset"),(DatabaseTypeKind.Xml,"xml")}){
            var type=ComparisonHost.ToSqlType(new(kind,false));Assert.Equal(name,type.Name);TdsPacketWriter.ValidateMetadata([new("Value",type.ClrType,type.Name,false,false,0)]);
        }
        Assert.Throws<NotSupportedException>(()=>ComparisonHost.ToSqlType(new(DatabaseTypeKind.Object,false)));
        Assert.Throws<NotSupportedException>(()=>ComparisonHost.ToSqlType(new(DatabaseTypeKind.Object,false,RuntimeType:typeof(string))));
    }
    [Fact]public void UdtColumnsJoinSysTypesOverSqlClient(){
        var adapter=new ProtocolMetadataAdapter();var registry=new DatabaseRegistry();registry.Register("first",adapter);using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();using var connection=Connect();
        using var command=connection.CreateCommand();command.CommandText="SELECT c.name,t.name,c.system_type_id,c.user_type_id,c.max_length,t.is_assembly_type FROM sys.columns AS c JOIN sys.types AS t ON c.user_type_id=t.user_type_id WHERE c.name IN (N'Node',N'Shape',N'Location') ORDER BY c.name";
        using var reader=command.ExecuteReader();var rows=new Dictionary<string,(string,int,int,int,bool)>();while(reader.Read()){rows.Add(reader.GetString(0),(reader.GetString(1),reader.GetByte(2),reader.GetInt32(3),reader.GetInt16(4),reader.GetBoolean(5)));}
        Assert.Equal(3,rows.Count);Assert.Equal(("hierarchyid",240,128,892,true),rows["Node"]);Assert.Equal(("geometry",240,129,-1,true),rows["Shape"]);Assert.Equal(("geography",240,130,-1,true),rows["Location"]);
    }
    [Fact]public void TdsPreflightUsesWireMetadataAndCollationRules(){
        TdsPacketWriter.ValidateCollation(null);TdsPacketWriter.ValidateCollation("Japanese_CI_AS");
        TdsPacketWriter.ValidateMetadata([new("Name",typeof(string),"varchar",false,false,0,20,collation:"Japanese_CI_AS")]);
        Assert.Throws<NotSupportedException>(()=>TdsPacketWriter.ValidateCollation("Unsupported_CI_AS"));
        Assert.Throws<NotSupportedException>(()=>TdsPacketWriter.ValidateMetadata([new("Name",typeof(string),"varchar",false,false,0,20,collation:"Unsupported_CI_AS")]));
        Assert.Throws<NotSupportedException>(()=>TdsPacketWriter.ValidateMetadata([new("Name",typeof(string),"unknown_type",false,false,0)]));
        Assert.Throws<NotSupportedException>(()=>TdsPacketWriter.ValidateMetadata([new("Name",typeof(byte[]),"nvarchar",false,false,0)]));
        Assert.Throws<NotSupportedException>(()=>TdsPacketWriter.ValidateMetadata([new("Name",typeof(string),"varchar(max)",false,false,0)]));
        TdsPacketWriter.ValidateMetadata([new("Name",typeof(string),"varchar",false,false,0,-1)]);
    }
    [Fact]public void RpcWireCollationRejectsInvalidData(){
        var type=typeof(TdsServer).Assembly.GetType("Seamless.Database.Gateway.Tds.TdsRpcRequest",true)!;var parse=type.GetMethod("Parse")!;
        object Invoke(byte[] collation,byte[] value){using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(ushort.MaxValue);writer.Write((ushort)10);writer.Write((ushort)0);writer.Write((byte)0);writer.Write((byte)0);writer.Write((byte)0xA7);writer.Write((ushort)20);writer.Write(collation);writer.Write((ushort)value.Length);writer.Write(value);writer.Flush();return parse.Invoke(null,[stream.ToArray()])!;}
        var request=Invoke([0x11,0x04,0xD0,0,0],[0x93,0xFA,0x96,0x7B,0x8C,0xEA]);var parameters=(System.Collections.IList)type.GetProperty("Parameters")!.GetValue(request)!;Assert.Equal("日本語",parameters[0]!.GetType().GetProperty("Value")!.GetValue(parameters[0]));
        var unknown=Assert.Throws<System.Reflection.TargetInvocationException>(()=>Invoke([0x19,0x04,0xD0,0,0],[0x41]));Assert.IsType<NotSupportedException>(unknown.InnerException);
        var malformed=Assert.Throws<System.Reflection.TargetInvocationException>(()=>Invoke([0x11,0x04,0xD0,0x08,0],[0x41]));Assert.IsType<InvalidDataException>(malformed.InnerException);
        var invalidBytes=Assert.Throws<System.Reflection.TargetInvocationException>(()=>Invoke([0x11,0x04,0xD0,0,0],[0x81]));Assert.IsType<System.Text.DecoderFallbackException>(invalidBytes.InnerException);
    }}

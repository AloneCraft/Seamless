using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class TdsLargeValueTests{
    [Theory,Trait("Category","Integration")]
    [InlineData("nvarchar")]
    [InlineData("varchar")]
    [InlineData("varbinary")]
    public async Task MaxValuesPreserveFollowingColumnsAndRows(string sqlType){
        var adapter=new LargeValueAdapter(sqlType);using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
        await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();
        using var command=connection.CreateCommand();command.CommandText="SELECT payload,marker FROM dbo.LargeValues";command.CommandTimeout=10;
        await using var reader=await command.ExecuteReaderAsync();
        for(var i=0;i<adapter.Values.Length;i++){
            Assert.True(await reader.ReadAsync());var expected=adapter.Values[i];
            if(expected is null){Assert.True(await reader.IsDBNullAsync(0));}
            else if(expected is byte[] bytes){Assert.Equal(bytes,(byte[])reader.GetValue(0));}
            else{Assert.Equal((string)expected,reader.GetString(0));}
            Assert.Equal(100+i,reader.GetInt32(1));
        }
        Assert.False(await reader.ReadAsync());Assert.False(await reader.NextResultAsync());
    }
    [Theory,Trait("Category","Diagnostic")]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public async Task GatewayReadModes(bool asynchronous,bool sequential){
        var adapter=new LargeValueAdapter("nvarchar");using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
        await ReadModes(options.ConnectionString,"SELECT payload,marker FROM dbo.LargeValues",asynchronous,sequential,true);
    }
    [SqlServerPrintFact,Trait("Category","Diagnostic")]
    public async Task SourceReadModes(){
        var options=new SqlConnectionStringBuilder{DataSource="localhost",InitialCatalog="AdventureWorks2016",IntegratedSecurity=true,Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=5};
        foreach(var asynchronous in new[]{false,true}){foreach(var sequential in new[]{false,true}){
            await ReadModes(options.ConnectionString,"SELECT REPLICATE(CAST(N'x' AS nvarchar(max)),20000),100",asynchronous,sequential,false);
        }}
    }
    private static async Task ReadModes(string connectionString,string sql,bool asynchronous,bool sequential,bool gateway){
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText=sql;command.CommandTimeout=10;
        var behavior=sequential?global::System.Data.CommandBehavior.SequentialAccess:global::System.Data.CommandBehavior.Default;
        using var reader=asynchronous?await command.ExecuteReaderAsync(behavior):command.ExecuteReader(behavior);
        var index=0;while(asynchronous?await reader.ReadAsync():reader.Read()){
            if(gateway&&index==0){Assert.Equal("",reader.GetString(0));Assert.Equal(100,reader.GetInt32(1));}
            else if(gateway&&index==1){Assert.True(reader.IsDBNull(0));Assert.Equal(101,reader.GetInt32(1));}
            else if(!gateway||index==2){Assert.Equal(new string('x',20000),reader.GetString(0));Assert.Equal(gateway?102:100,reader.GetInt32(1));}
            else{Assert.Equal("end",reader.GetString(0));Assert.Equal(103,reader.GetInt32(1));}index++;
        }
        Assert.Equal(gateway?4:1,index);
    }
    [Theory,Trait("Category","Integration")]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public async Task UnicodeColumnsPreserveBoundaryValues(bool asynchronous,bool sequential){
        var adapter=new BoundaryAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
        await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="SELECT first,second,marker FROM dbo.UnicodeValues";command.CommandTimeout=10;
        var behavior=sequential?global::System.Data.CommandBehavior.SequentialAccess:global::System.Data.CommandBehavior.Default;using var reader=asynchronous?await command.ExecuteReaderAsync(behavior):command.ExecuteReader(behavior);
        foreach(var expected in adapter.Rows){Assert.True(asynchronous?await reader.ReadAsync():reader.Read());for(var column=0;column<2;column++){if(expected[column] is null){Assert.True(reader.IsDBNull(column));}else{Assert.Equal((string)expected[column]!,reader.GetString(column));}}Assert.Equal((int)expected[2]!,reader.GetInt32(2));}
        Assert.False(asynchronous?await reader.ReadAsync():reader.Read());Assert.False(asynchronous?await reader.NextResultAsync():reader.NextResult());
    }
    [Fact,Trait("Category","Unit")]
    public async Task WireDecoderReconstructsLongUnicode(){
        var adapter=new BoundaryAdapter();var pipe=new global::System.IO.Pipelines.Pipe(new global::System.IO.Pipelines.PipeOptions(pauseWriterThreshold:1000000,resumeWriterThreshold:500000));
        var writer=new TdsPacketWriter(pipe.Writer);await writer.SendQueryResultAsync(new QueryResult(adapter.Table.Columns,adapter.Rows,0));await pipe.Writer.CompleteAsync();
        using var bytes=new MemoryStream();while(true){var result=await pipe.Reader.ReadAsync();foreach(var segment in result.Buffer){bytes.Write(segment.Span);}pipe.Reader.AdvanceTo(result.Buffer.End);if(result.IsCompleted){break;}}await pipe.Reader.CompleteAsync();
        var wire=bytes.ToArray();var offset=0;var packetIndex=0;using var payload=new MemoryStream();var ranges=new List<(int Start,int End)>();
        while(offset<wire.Length){Assert.True(offset+8<=wire.Length);var size=global::System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(wire.AsSpan(offset+2,2));Assert.InRange(size,8,4096);Assert.True(offset+size<=wire.Length);Assert.Equal(offset+size==wire.Length?1:0,wire[offset+1]&1);if(packetIndex>0){var start=checked((int)payload.Length);payload.Write(wire,offset+8,size-8);ranges.Add((start,checked((int)payload.Length)));}offset+=size;packetIndex++;}
        var data=payload.ToArray();var cursor=0;var chunkCount=0;
        foreach(var row in adapter.Rows){Assert.Equal(0xD1,data[cursor++]);for(var column=0;column<2;column++){
            var total=BitConverter.ToInt64(data,cursor);cursor+=8;if(row[column] is null){Assert.Equal(-1,total);continue;}
            var expected=(string)row[column]!;Assert.Equal(global::System.Text.Encoding.Unicode.GetByteCount(expected),total);using var value=new MemoryStream();
            while(true){var start=cursor;var length=BitConverter.ToInt32(data,cursor);cursor+=4;if(length==0){break;}Assert.InRange(length,2,4084);Assert.Equal(0,length%2);Assert.True(cursor+length<=data.Length);Assert.Contains(ranges,r=>start>=r.Start&&cursor+length<=r.End);value.Write(data,cursor,length);cursor+=length;chunkCount++;}
            Assert.Equal(total,value.Length);Assert.Equal(expected,global::System.Text.Encoding.Unicode.GetString(value.ToArray()));
        }Assert.Equal((int)row[2]!,BitConverter.ToInt32(data,cursor));cursor+=4;}
        Assert.True(chunkCount>2);Assert.Equal(0xFD,data[cursor]);Assert.Equal(data.Length,cursor+13);
    }
    private sealed class BoundaryAdapter:IDatabaseAdapter{
        public TableMetadata Table{get;}=new("dbo","UnicodeValues",[new("first",typeof(string),"nvarchar",true,false,0,-1),new("second",typeof(string),"nvarchar",true,false,1,-1),new("marker",typeof(int),"int",false,false,2)]);
        public object?[][] Rows{get;}=[["",null,100],[new string('a',2041),new string('b',2042),101],[new string('c',2043),new string('d',2041)+"😀"+"end",102],[new string('x',20000),"tail😀",103]];
        public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata> GetTables()=>[this.Table];public TableMetadata? FindTable(string name)=>name.Equals("dbo.UnicodeValues",StringComparison.OrdinalIgnoreCase)?this.Table:null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>new(this.Table.Columns,this.Rows,0);
        public int ExecuteInsert(string n,IReadOnlyDictionary<string,object?>v,SessionContext s)=>throw new NotSupportedException();public int ExecuteUpdate(string n,IReadOnlyDictionary<string,object?>v,Func<object,bool>?p,SessionContext s)=>throw new NotSupportedException();public int ExecuteDelete(string n,Func<object,bool>?p,SessionContext s)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext s)=>throw new NotSupportedException();public void CommitTransaction(SessionContext s)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext s)=>throw new NotSupportedException();
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(512)]
    [InlineData(4096)]
    [InlineData(8000)]
    [InlineData(8192)]
    public async Task LoginAcknowledgesServerPacketSize(int requested){
        var adapter=new LargeValueAdapter("nvarchar");using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5,PacketSize=requested};
        await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();Assert.Equal(4096,connection.PacketSize);
        using var command=connection.CreateCommand();command.CommandText="SELECT 7 AS value";Assert.Equal(7,Convert.ToInt32(await command.ExecuteScalarAsync()));
    }
    [Fact,Trait("Category","Unit")]
    public async Task LoginWirePacketSizeEnvChangeUsesBVarChar(){
        var pipe=new global::System.IO.Pipelines.Pipe();var writer=new TdsPacketWriter(pipe.Writer);await writer.SendLoginAckAsync("fixture");await pipe.Writer.CompleteAsync();
        using var capture=new MemoryStream();while(true){var read=await pipe.Reader.ReadAsync();foreach(var segment in read.Buffer){capture.Write(segment.Span);}pipe.Reader.AdvanceTo(read.Buffer.End);if(read.IsCompleted){break;}}await pipe.Reader.CompleteAsync();
        var packet=capture.ToArray();var length=global::System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2,2));Assert.Equal(length,packet.Length);Assert.Equal(1,packet[1]&1);
        var position=8;var found=false;while(position<packet.Length){var token=packet[position++];if(token==0xFD){Assert.Equal(packet.Length,position+12);break;}Assert.True(token is 0xAD or 0xE3);var size=BitConverter.ToUInt16(packet,position);position+=2;var end=position+size;Assert.True(end<=packet.Length);
            if(token==0xE3&&packet[position]==4){Assert.False(found);found=true;position++;var newLength=packet[position++];var newValue=global::System.Text.Encoding.Unicode.GetString(packet,position,newLength*2);position+=newLength*2;var oldLength=packet[position++];var oldValue=global::System.Text.Encoding.Unicode.GetString(packet,position,oldLength*2);position+=oldLength*2;Assert.Equal("4096",newValue);Assert.Equal("4096",oldValue);Assert.Equal(end,position);}
            position=end;
        }Assert.True(found);
    }

    private sealed class LargeValueAdapter:IDatabaseAdapter{
        private readonly TableMetadata table;
        public LargeValueAdapter(string sqlType){
            var binary=sqlType=="varbinary";
            this.Values=binary?[Array.Empty<byte>(),null,Enumerable.Range(0,20000).Select(i=>(byte)i).ToArray(),new byte[]{7}]:["",null,new string('x',20000),"end"];
            this.table=new("dbo","LargeValues",[new("payload",binary?typeof(byte[]):typeof(string),sqlType,true,false,0,-1,collation:binary?null:"SQL_Latin1_General_CP1_CI_AS"),new("marker",typeof(int),"int",false,false,1)]);
        }
        public object?[] Values{get;}
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata> GetTables()=>[this.table];
        public TableMetadata? FindTable(string name)=>name.Equals("dbo.LargeValues",StringComparison.OrdinalIgnoreCase)?this.table:null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>new(this.table.Columns,this.Values.Select((value,index)=>new object?[]{value,100+index}),0);
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();
        public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();
        public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}

using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Hosting;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace Seamless.Database.Gateway.Tests;

[MemoryPackable]public partial record struct LambdaRecord(int Id);
[MemoryPackable]public partial record struct LambdaRelation(int ObjectId,int Other);

public sealed class NativeLambdaDatabase{
    public interface ISurface{
        NativeTable<int,LambdaRecord> ReadOnly{get;}
        NativeTable<int,LambdaRecord> WriteOnly{get;}
    }
    public interface IRelations{
        NativeTable<RuntimeTypedKey,LambdaRelation> A{get;}
        NativeTable<RuntimeTypedKey,LambdaRelation> B{get;}
        NativeTable<RuntimeTypedKey,LambdaRelation> Self{get;}
    }
    private static NativeLambdaNode N(string kind,string? text=null,params NativeLambdaNode[] children)=>new(kind,text,children);
    private static NativeLambdaNode Table(string name)=>N("Member",name,N("Variable","p0"));
    private static NativeLambdaNode Add(string name)=>N("Expression",null,N("Call","Add",Table(name),N("New",typeof(LambdaRecord).FullName,N("Variable","p1"))));
    private static NativeLambdaProgram Program(NativeLambdaNode body)=>new(1,typeof(ISurface).FullName!,NativeLambdaContracts.Surface<ISurface>(),typeof(int).FullName!,typeof(int).FullName!,NativeWireContract.For<int>(),NativeWireContract.For<int>(),body);

    [Fact,Trait("Category","Unit")]
    public void Prepare(){
        var demands=new List<NativeLambdaAccess>();
        var schema=new NativeLambdaSchema<ISurface>(static _=>throw new InvalidOperationException("Prepare must not bind a database."),access=>{
            demands.Add(access);return access is{Table:"ReadOnly",Operation:"Read"}or{Table:"WriteOnly",Operation:"Insert"};
        });
        using var runtime=new NativeLambdaDatabase<ISurface>(schema);
        var program=Program(N("Block",null,Add("WriteOnly"),N("Return",null,N("Linq","Count",Table("ReadOnly")))));
        var handle=runtime.Prepare(program,"v1");
        Assert.Equal(new[]{new NativeLambdaAccess("ReadOnly","Read"),new NativeLambdaAccess("WriteOnly","Insert")},demands);
        Assert.Equal(0,runtime.ExecutionCount);Assert.Equal(1,runtime.CompilationCount);
        Assert.Equal(handle,runtime.Prepare(program,"v1"));Assert.Equal(1,runtime.CompilationCount);

        foreach(var invalid in new[]{
            program with{DatabaseContract="changed"},
            program with{ArgumentsContract="changed"},
            program with{Body=N("Block",null,N("Return",null,N("Literal","1);System.Environment.Exit(0);return 1")))},
            program with{Body=N("Block",null,N("Return",null,N("New","System.Diagnostics.Process")))},
            program with{Body=N("Unknown")}
        }){var error=Assert.Throws<NativeLambdaException>(()=>runtime.Prepare(invalid,"v1"));Assert.True(error.ExecutionNotStarted);}
        Assert.Equal(1,runtime.CompilationCount);
    }

    [Fact,Trait("Category","Integration")]
    public async Task ExecuteAsync(){
        using var database=new Fixture();
        var allowed=true;
        using var runtime=new NativeLambdaDatabase<ISurface>(new(static db=>new Surface((Fixture)db),_=>allowed));
        var generation="v1";var transport=runtime.Local(database,()=>generation);
        var program=Program(N("Block",null,Add("WriteOnly"),N("Return",null,N("Linq","Count",Table("ReadOnly")))));
        var handle=await transport.PrepareAsync(program,default);Assert.Empty(database.Rows);
        Assert.Equal(1,MemoryPackSerializer.Deserialize<int>(await transport.ExecuteAsync(handle,MemoryPackSerializer.Serialize(1),default)));
        var failure=program with{Body=N("Block",null,Add("WriteOnly"),N("Throw",null,N("Literal","\"rollback\"")))};
        var failing=await transport.PrepareAsync(failure,default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>transport.ExecuteAsync(failing,MemoryPackSerializer.Serialize(2),default));
        Assert.False(database.Rows.ContainsKey(2));
        allowed=false;
        var denied=await Assert.ThrowsAsync<NativeLambdaException>(()=>transport.ExecuteAsync(handle,MemoryPackSerializer.Serialize(3),default));Assert.Equal("Permission",denied.Code);Assert.False(database.Rows.ContainsKey(3));
        allowed=true;generation="v2";
        var stale=await Assert.ThrowsAsync<NativeLambdaException>(()=>transport.ExecuteAsync(handle,MemoryPackSerializer.Serialize(3),default));Assert.Equal("Stale",stale.Code);Assert.True(stale.ExecutionNotStarted);Assert.False(database.Rows.ContainsKey(3));
    }

    [Fact,Trait("Category","Unit")]
    public async Task LeastRecentlyUsedPreparedQueriesAreReclaimed(){
        using var database=new Fixture();
        using var runtime=new NativeLambdaDatabase<ISurface>(new(static db=>new Surface((Fixture)db),static _=>true)){MaximumPreparedQueries=2};
        var transport=runtime.Local(database,static ()=>"v1");
        NativeLambdaProgram constant(int value)=>Program(N("Block",null,N("Return",null,N("Literal",value.ToString(global::System.Globalization.CultureInfo.InvariantCulture)))));
        var first=runtime.Prepare(constant(1),"v1");var second=runtime.Prepare(constant(2),"v1");
        Assert.Equal(first,runtime.Prepare(constant(1),"v1"));
        var third=runtime.Prepare(constant(3),"v1");Assert.Equal(3,runtime.CompilationCount);
        Assert.Equal(1,MemoryPackSerializer.Deserialize<int>(await transport.ExecuteAsync(first,MemoryPackSerializer.Serialize(0),default)));
        Assert.Equal(3,MemoryPackSerializer.Deserialize<int>(await transport.ExecuteAsync(third,MemoryPackSerializer.Serialize(0),default)));
        var stale=await Assert.ThrowsAsync<NativeLambdaException>(()=>transport.ExecuteAsync(second,MemoryPackSerializer.Serialize(0),default));Assert.Equal("Stale",stale.Code);Assert.True(stale.ExecutionNotStarted);
        var count=runtime.CompilationCount;
        Assert.Throws<NativeLambdaException>(()=>runtime.Prepare(constant(4)with{DatabaseContract="bad"},"v1"));Assert.Equal(count,runtime.CompilationCount);
        Assert.Equal(first,runtime.Prepare(constant(1),"v1"));
    }

    [Fact,Trait("Category","Unit")]
    public async Task InvalidationAndDisposalDrainRunningCode(){
        foreach(var dispose in new[]{false,true}){foreach(var cancel in new[]{false,true}){
            using var database=new Fixture();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var cancellation=new CancellationTokenSource();
            var schema=new NativeLambdaSchema<ISurface>(db=>{entered.Set();release.Wait();return new Surface((Fixture)db);},static _=>true);
            using var runtime=new NativeLambdaDatabase<ISurface>(schema){MaximumPreparedQueries=1};var transport=runtime.Local(database,static ()=>"v1");
            var program=Program(N("Block",null,N("Return",null,N("Literal","1"))));var handle=runtime.Prepare(program,"v1");
            var running=Task.Run(()=>transport.ExecuteAsync(handle,MemoryPackSerializer.Serialize(0),cancellation.Token));
            try{
                Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));Assert.Equal(1,runtime.CacheStatistics.ExecutionLeases);
                var full=Assert.Throws<NativeLambdaException>(()=>runtime.Prepare(program with{Body=N("Block",null,N("Return",null,N("Literal","2")))},"v1"));Assert.Equal("Capacity",full.Code);Assert.Equal(1,runtime.CompilationCount);
                runtime.InvalidatePreparedQueries("other");Assert.Equal(1,runtime.CacheStatistics.PreparedQueries);
                runtime.InvalidatePreparedQueries("v1");Assert.Equal(new NativeLambdaCacheStatistics(0,1,1,1,0),runtime.CacheStatistics);
                full=Assert.Throws<NativeLambdaException>(()=>runtime.Prepare(program,"v1"));Assert.Equal("Capacity",full.Code);
                if(dispose){runtime.Dispose();Assert.Throws<ObjectDisposedException>(()=>runtime.Prepare(program,"v1"));}
                if(cancel){cancellation.Cancel();}
            }finally{release.Set();try{await running.WaitAsync(TimeSpan.FromSeconds(5));}catch(OperationCanceledException)when(cancellation.IsCancellationRequested){}}
            if(cancel){await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>running);}else{Assert.Equal(1,MemoryPackSerializer.Deserialize<int>(await running));}
            Assert.Equal(0,runtime.CacheStatistics.ExecutionLeases);Assert.Equal(0,runtime.CacheStatistics.LiveQueries);
            if(!dispose){var renewed=runtime.Prepare(program,"v1");Assert.NotEqual(handle,renewed);Assert.Equal(1,MemoryPackSerializer.Deserialize<int>(await transport.ExecuteAsync(renewed,MemoryPackSerializer.Serialize(0),default)));}
        }}
    }

    [Fact,Trait("Category","Integration")]
    public async Task NetworkRepreparesReclaimedHandleWithoutDuplicateExecution(){
        using var database=new Fixture();var bindings=0;
        var schema=new NativeLambdaSchema<ISurface>(db=>{Interlocked.Increment(ref bindings);return new Surface((Fixture)db);},static _=>true);
        var registry=new DatabaseRegistry();registry.Register("cache",new InProcessDatabaseAdapter(database),schemaFingerprint:"v1");
        using var server=new NativeQueryServer(registry,0);server.RegisterLambda("cache",schema,maximumPreparedQueries:2);server.Start();
        await using var connection=new NativeQueryClient("127.0.0.1",server.Port);var client=connection.Lambda<ISurface>("cache");
        var insert=client.CreateGeneratedQuery<int,int>(Program(N("Block",null,Add("WriteOnly"),N("Return",null,N("Linq","Count",Table("ReadOnly"))))).Body);
        Assert.Equal(1,await insert.ExecuteAsync(7));
        var second=client.CreateGeneratedQuery<int,int>(N("Block",null,N("Return",null,N("Literal","2"))));await second.PrepareAsync();
        var third=client.CreateGeneratedQuery<int,int>(N("Block",null,N("Return",null,N("Literal","3"))));await third.PrepareAsync();
        Assert.Equal(1,bindings);Assert.Equal(2,await insert.ExecuteAsync(8));Assert.Equal(2,bindings);Assert.Equal(2,database.Rows.Count);
        await server.StopAsync();
    }

    [Fact,Trait("Category","Unit")]
    public async Task MalformedArgumentsNeverBindOrExecute(){
        using var database=new Fixture();var bindings=0;
        using var runtime=new NativeLambdaDatabase<ISurface>(new(db=>{bindings++;return new Surface((Fixture)db);},static _=>true));
        var program=Program(N("Block",null,Add("WriteOnly"),N("Return",null,N("Linq","Count",Table("ReadOnly")))));
        var handle=runtime.Prepare(program,"v1");var transport=runtime.Local(database,static ()=>"v1");
        foreach(var bytes in new[]{Array.Empty<byte>(),new byte[3],new byte[5]}){
            var error=await Assert.ThrowsAsync<NativeLambdaException>(()=>transport.ExecuteAsync(handle,bytes,default));Assert.Equal("Arguments",error.Code);Assert.True(error.ExecutionNotStarted);
        }
        Assert.Equal(0,bindings);Assert.Equal(0,runtime.ExecutionCount);Assert.Empty(database.Rows);Assert.Equal(0,runtime.CacheStatistics.ExecutionLeases);
        Assert.Equal(1,MemoryPackSerializer.Deserialize<int>(await transport.ExecuteAsync(handle,MemoryPackSerializer.Serialize(1),default)));
    }

    [Fact,Trait("Category","Integration")]
    public async Task NetworkRejectsMalformedArgumentsAndOversizedPrepare(){
        using var database=new Fixture();var bindings=0;
        var schema=new NativeLambdaSchema<ISurface>(db=>{bindings++;return new Surface((Fixture)db);},static _=>true).Expose<int[]>();
        var registry=new DatabaseRegistry();registry.Register("wire",new InProcessDatabaseAdapter(database),schemaFingerprint:"v1");
        using var server=new NativeQueryServer(registry,0);server.RegisterLambda("wire",schema);server.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using(var socket=new TcpClient()){
            await socket.ConnectAsync("127.0.0.1",server.Port,timeout.Token);var stream=socket.GetStream();
            var program=Program(N("Block",null,N("Return",null,N("Literal","1"))))with{ArgumentsType=typeof(int[]).FullName!,ArgumentsContract=NativeWireContract.For<int[]>()};
            await NativeFrame.WriteAsync(stream,NativeLambdaWire.Prepare,NativeLambdaWire.PrepareRequest("wire",program),timeout.Token);
            var prepared=await NativeFrame.ReadAsync(stream,timeout.Token)??throw new EndOfStreamException();Assert.Equal(NativeLambdaWire.Prepared,prepared.Kind);var handle=NativeLambdaWire.ReadHandle(prepared.Payload);
            Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
            await NativeFrame.WriteAsync(stream,NativeLambdaWire.Execute,NativeLambdaWire.ExecuteRequest("wire",handle,default,new byte[]{255,255,255,127}),timeout.Token);
            var rejected=await NativeFrame.ReadAsync(stream,timeout.Token)??throw new EndOfStreamException();Assert.Equal(NativeLambdaWire.Error,rejected.Kind);var error=NativeLambdaWire.ReadFailure(rejected.Payload);Assert.Equal("Arguments",error.Code);Assert.True(error.ExecutionNotStarted);Assert.Equal(0,bindings);
            await NativeFrame.WriteAsync(stream,NativeLambdaWire.Execute,NativeLambdaWire.ExecuteRequest("wire",handle,default,MemoryPackSerializer.Serialize(new[]{1,2})),timeout.Token);
            var row=await NativeFrame.ReadAsync(stream,timeout.Token)??throw new EndOfStreamException();Assert.Equal(NativeFrame.Row,row.Kind);Assert.Equal(1,MemoryPackSerializer.Deserialize<int>(row.Payload));Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);Assert.Equal(1,bindings);
        }
        using(var oversized=new TcpClient()){
            await oversized.ConnectAsync("127.0.0.1",server.Port,timeout.Token);var header=new byte[5];BinaryPrimitives.WriteInt32LittleEndian(header,1024*1024);header[4]=NativeLambdaWire.Prepare;await oversized.GetStream().WriteAsync(header,timeout.Token);
            Assert.Equal(0,await oversized.GetStream().ReadAsync(new byte[1],timeout.Token));
        }
        await using var client=new NativeQueryClient("127.0.0.1",server.Port);var query=client.Lambda<ISurface>("wire").CreateGeneratedQuery<int,int>(N("Block",null,N("Return",null,N("Literal","2"))));Assert.Equal(2,await query.ExecuteAsync(0,timeout.Token));
        await server.StopAsync();
    }

    [Fact,Trait("Category","Integration")]
    public async Task ResultLimitsRollBackWritesBeforeCommit(){
        using var database=new Fixture();
        using var runtime=new NativeLambdaDatabase<ISurface>(new(static db=>new Surface((Fixture)db),static _=>true){WireLimits=new(){MaximumStringCharacters=3}});
        var client=new NativeLambdaClient<ISurface>(runtime.Local(database,static ()=>"v1"));
        var query=client.CreateGeneratedQuery<int,string>(N("Block",null,Add("WriteOnly"),N("Return",null,N("Literal","\"too long\""))));
        await Assert.ThrowsAsync<InvalidDataException>(()=>query.ExecuteAsync(1));Assert.Empty(database.Rows);Assert.Equal(0,runtime.CacheStatistics.ExecutionLeases);
    }

    [Fact,Trait("Category","Unit")]
    public void ReclaimedCodeUnloadsEvenIfAnOldModuleReferenceIsHeld(){
        var observed=ReclaimedCode();
        for(var attempt=0;attempt<10&&observed.Lifetime.IsAlive;attempt++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
        Assert.False(observed.Lifetime.IsAlive);GC.KeepAlive(observed.Module);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Lifetime,INativeQueryModule Module) ReclaimedCode(){
        using var runtime=new NativeLambdaDatabase<ISurface>(new(static _=>throw new InvalidOperationException("Not executed."),static _=>true)){MaximumPreparedQueries=1};
        var program=Program(N("Block",null,N("Return",null,N("Literal","101535"))));var handle=runtime.Prepare(program,"v1");
        var name="Seamless.Lambda_"+Convert.ToHexString(SHA256.HashData(program.Encode()))[..24];
        var context=AssemblyLoadContext.All.Single(item=>item.IsCollectible&&item.Assemblies.Any(assembly=>assembly.GetName().Name==name));
        var lifetime=new WeakReference(context);var module=runtime.Resolve(handle,"fixture","v1").Module;
        runtime.Prepare(program with{Body=N("Block",null,N("Return",null,N("Literal","101536")))},"v1");
        return(lifetime,module);
    }

    [Fact,Trait("Category","Unit")]
    public async Task RegisteredCatalogStaysImmutableDuringConcurrentPreparation(){
        var schema=new NativeLambdaSchema<ISurface>(static _=>throw new InvalidOperationException("Not executed."),static _=>true);
        using var first=new NativeLambdaDatabase<ISurface>(schema);using var second=new NativeLambdaDatabase<ISurface>(schema);
        Assert.Throws<InvalidOperationException>(()=>schema.Expose<WirePayload>());
        var program=Program(N("Block",null,N("Return",null,N("Convert","int[]",N("Variable","p1")))))with{ArgumentsType=typeof(int[]).FullName!,ResultType=typeof(int[]).FullName!,ArgumentsContract=NativeWireContract.For<int[]>(),ResultContract=NativeWireContract.For<int[]>()};
        await Task.WhenAll(Task.Run(()=>first.Prepare(program,"v1")),Task.Run(()=>second.Prepare(program,"v1")));
        Assert.Equal(1,first.CompilationCount);Assert.Equal(1,second.CompilationCount);
        Assert.Throws<NativeLambdaException>(()=>first.Prepare(program with{ArgumentsType="System.Int32"+string.Concat(Enumerable.Repeat("[]",40))},"v1"));
    }

    [Fact,Trait("Category","Unit")]
    public void StructuralBounds(){
        var body=N("Literal","1");for(var index=0;index<NativeLambdaProgram.MaximumDepth+1;index++){body=N("Unary","+",body);}
        Assert.Throws<InvalidDataException>(()=>Program(body).Encode());
        Assert.Throws<InvalidDataException>(()=>NativeLambdaProgram.Decode(new byte[NativeLambdaProgram.MaximumBytes+1]));
        Assert.Throws<InvalidDataException>(()=>NativeLambdaWire.ReadRequest([255,255]));
        Assert.Throws<InvalidDataException>(()=>NativeLambdaWire.ReadExecution(new byte[39]));
        var handle=new NativePreparedLambda(Guid.NewGuid(),1);Assert.Equal(handle,NativeLambdaWire.ReadHandle(NativeLambdaWire.Handle(handle)));
    }

    [Fact,Trait("Category","Integration")]
    public async Task CyclesCommitOnlyFinalState(){
        foreach(var network in new[]{false,true}){
            using var database=new RelationFixture();
            var schema=new NativeLambdaSchema<IRelations>(static db=>new RelationSurface((RelationFixture)db),static _=>true);
            using var local=new NativeLambdaDatabase<IRelations>(schema);
            var registry=new DatabaseRegistry();registry.Register("relations",new InProcessDatabaseAdapter(database),schemaFingerprint:"v1");
            using var server=new NativeQueryServer(registry,0);server.RegisterLambda("relations",schema);server.Start();
            await using var connection=new NativeQueryClient("127.0.0.1",server.Port);
            var client=new NativeLambdaClient<IRelations>(local.Local(database,static ()=>"v1"));
            if(network){client=connection.Lambda<IRelations>("relations");}
            NativeLambdaNode add(string table,int id,int other)=>N("Expression",null,N("Call","Add",Table(table),N("New",typeof(LambdaRelation).FullName,N("Literal",id.ToString(global::System.Globalization.CultureInfo.InvariantCulture)),N("Literal",other.ToString(global::System.Globalization.CultureInfo.InvariantCulture)))));
            var body=N("Block",null,add("A",1,2),add("B",2,1),add("Self",7,8),add("Self",8,7),N("Return",null,N("Linq","Count",Table("A"))));
            var query=client.CreateGeneratedQuery<int,int>(body);await query.PrepareAsync();Assert.Empty(database.A);
            Assert.Equal(1,await query.ExecuteAsync(0));Assert.Single(database.A);Assert.Single(database.B);Assert.Equal(2,database.Self.Count);
            var broken=client.CreateGeneratedQuery<int,int>(N("Block",null,add("A",3,99),N("Return",null,N("Literal","1"))));
            if(network){await Assert.ThrowsAsync<NativeLambdaException>(()=>broken.ExecuteAsync(0));}
            else{await Assert.ThrowsAsync<ForeignKeyViolationException>(()=>broken.ExecuteAsync(0));}
            Assert.False(database.A.ContainsKey(new(3)));Assert.Single(database.A);Assert.Single(database.B);
            NativeLambdaNode number(int value)=>N("Literal",value.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
            NativeLambdaNode key(int value)=>N("New",typeof(RuntimeTypedKey).FullName,number(value));
            NativeLambdaNode replace(int id)=>N("Expression",null,N("Call","Replace",Table("Self"),key(id),N("New",typeof(LambdaRelation).FullName,number(id),number(id))));
            var change=client.CreateGeneratedQuery<int,int>(N("Block",null,replace(7),replace(8),N("Return",null,N("Linq","Count",Table("Self")))));
            Assert.Equal(2,await change.ExecuteAsync(0));Assert.Equal(7,database.Self[new(7)].Other);Assert.Equal(8,database.Self[new(8)].Other);
            NativeLambdaNode remove(string table,int id)=>N("Expression",null,N("Call","Remove",Table(table),key(id)));
            var delete=client.CreateGeneratedQuery<int,int>(N("Block",null,remove("A",1),remove("B",2),remove("Self",7),remove("Self",8),N("Return",null,N("Linq","Count",Table("A")))));
            Assert.Equal(0,await delete.ExecuteAsync(0));Assert.Empty(database.A);Assert.Empty(database.B);Assert.Empty(database.Self);
            await server.StopAsync();
        }
    }

    private sealed class Fixture:DatabaseContainer{
        public Fixture():base(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536}){_=this.Rows;}
        public Table<int,LambdaRecord> Rows=>this.Table<int,LambdaRecord>(static row=>row.Id,"Rows");
        protected override void EnsureTransactionSchema(){_=this.Rows;}
    }
    private sealed class Surface(Fixture database):ISurface{
        public NativeTable<int,LambdaRecord> ReadOnly{get;}=new NativeTableAdapter<int,LambdaRecord>(database.Rows);
        public NativeTable<int,LambdaRecord> WriteOnly{get;}=new NativeTableAdapter<int,LambdaRecord>(database.Rows);
    }
    private sealed class RelationFixture:DatabaseContainer{
        public RelationFixture():base(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536}){_=this.A;_=this.B;_=this.Self;}
        [ForeignKey("AB","B",new[]{"Other"},new[]{"ObjectId"})]
        public Table<RuntimeTypedKey,LambdaRelation> A=>this.Table<RuntimeTypedKey,LambdaRelation>(static row=>new(row.ObjectId),"A");
        [ForeignKey("BA","A",new[]{"Other"},new[]{"ObjectId"})]
        public Table<RuntimeTypedKey,LambdaRelation> B=>this.Table<RuntimeTypedKey,LambdaRelation>(static row=>new(row.ObjectId),"B");
        [ForeignKey("Self","Self",new[]{"Other"},new[]{"ObjectId"})]
        public Table<RuntimeTypedKey,LambdaRelation> Self=>this.Table<RuntimeTypedKey,LambdaRelation>(static row=>new(row.ObjectId),"Self");
        protected override void EnsureTransactionSchema(){_=this.A;_=this.B;_=this.Self;}
    }
    private sealed class RelationSurface(RelationFixture database):IRelations{
        public NativeTable<RuntimeTypedKey,LambdaRelation> A{get;}=new NativeTableAdapter<RuntimeTypedKey,LambdaRelation>(database.A);
        public NativeTable<RuntimeTypedKey,LambdaRelation> B{get;}=new NativeTableAdapter<RuntimeTypedKey,LambdaRelation>(database.B);
        public NativeTable<RuntimeTypedKey,LambdaRelation> Self{get;}=new NativeTableAdapter<RuntimeTypedKey,LambdaRelation>(database.Self);
    }
}

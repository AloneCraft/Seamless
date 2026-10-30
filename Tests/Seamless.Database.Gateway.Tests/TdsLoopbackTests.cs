using System.Net;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class TdsLoopbackTests{
    [Fact,Trait("Category","Integration")]
    public async Task LoopbackAddressesAndRestart(){
        var adapter=new EmptyAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));
        for(var pass=0;pass<2;pass++){
            server.Start();Assert.InRange(server.Port,1,65535);
            await Check("127.0.0.1",server.Port);
            if(Socket.OSSupportsIPv6){await Check("[::1]",server.Port);}
            var settings=Options("127.0.0.1",server.Port);await using var held=new SqlConnection(settings.ConnectionString);await held.OpenAsync();
            await server.StopAsync();
        }
    }
    private static SqlConnectionStringBuilder Options(string host,int port)=>new(){DataSource=$"tcp:{host},{port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=5};
    private static async Task Check(string host,int port){
        var options=Options(host,port);await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();
        using var command=connection.CreateCommand();command.CommandText="SELECT DB_NAME()";
        Assert.Equal("fixture",await command.ExecuteScalarAsync());
    }
    [Fact,Trait("Category","Integration")]
    public async Task IPv6BindFailureDoesNotLeaveIPv4Listener(){
        if(!Socket.OSSupportsIPv6){return;}
        var occupied=new TcpListener(IPAddress.IPv6Loopback,0);occupied.Server.DualMode=false;occupied.ExclusiveAddressUse=true;occupied.Start();
        var port=((IPEndPoint)occupied.LocalEndpoint).Port;var adapter=new EmptyAdapter();using var server=new TdsServer(port,adapter,new SqlExecutionEngine(adapter));
        try{
            Assert.Throws<SocketException>(()=>server.Start());
            var probe=new TcpListener(IPAddress.Loopback,port);try{probe.Start();}finally{probe.Stop();}
        }finally{occupied.Stop();}
        server.Start();await server.StopAsync();
    }
    private sealed class EmptyAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata>GetTables()=>[];public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}

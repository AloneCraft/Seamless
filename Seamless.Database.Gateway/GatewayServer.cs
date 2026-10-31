using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway;

public sealed class GatewayServerOptions{
    public int TdsPort{get;set;}=TdsConstants.DefaultPort;
    public int HttpPort{get;set;}=8088;
    public int NativePort{get;set;}=14335;
}

public sealed class GatewayServer:IDisposable{
    private readonly IDatabaseAdapter adapter;
    private readonly GatewayServerOptions options;
    private readonly SqlExecutionEngine sqlEngine;
    private readonly TdsServer tdsServer;
    private readonly HttpServer httpServer;
    private readonly NativeQueryServer nativeServer;
    private bool isStarted;

    public GatewayServer(IDatabaseAdapter adapter,GatewayServerOptions? options=null):this(CreateRegistry(adapter),options){}
    private static DatabaseRegistry CreateRegistry(IDatabaseAdapter adapter){var registry=new DatabaseRegistry();registry.Register(adapter.DatabaseName,adapter);return registry;}
    public GatewayServer(DatabaseRegistry databases,GatewayServerOptions? options=null){
        var adapter=databases.Resolve(databases.DefaultDatabaseName).Adapter;
        ArgumentNullException.ThrowIfNull(adapter);
        this.adapter=adapter;
        this.options=options??new GatewayServerOptions();

        this.sqlEngine=new SqlExecutionEngine(databases);
        this.tdsServer=new TdsServer(this.options.TdsPort,adapter,this.sqlEngine);
        this.httpServer=new HttpServer(this.options.HttpPort,adapter,this.sqlEngine);
        this.nativeServer=new NativeQueryServer(databases,this.options.NativePort);
    }

    public int TdsPort=>this.options.TdsPort;
    public int HttpPort=>this.options.HttpPort;
    public int NativePort=>this.nativeServer.Port;
    public NativeQueryServer Native=>this.nativeServer;

    public void Start(){
        if(this.isStarted){
            return;
        }else{
        }

        this.isStarted=true;
        try{this.tdsServer.Start();this.httpServer.Start();this.nativeServer.Start();}
        catch{this.StopAsync().GetAwaiter().GetResult();throw;}
    }

    public async Task StopAsync(){
        if(!this.isStarted){
            return;
        }else{
        }

        await this.nativeServer.StopAsync();
        await this.tdsServer.StopAsync();
        await this.httpServer.StopAsync();
        this.isStarted=false;
    }

    public void Dispose(){
        this.StopAsync().GetAwaiter().GetResult();
    }
}

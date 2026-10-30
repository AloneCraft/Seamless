using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway;

public sealed class GatewayServerOptions{
    public int TdsPort{get;set;}=TdsConstants.DefaultPort;
    public int HttpPort{get;set;}=8088;
}

public sealed class GatewayServer:IDisposable{
    private readonly IDatabaseAdapter adapter;
    private readonly GatewayServerOptions options;
    private readonly SqlExecutionEngine sqlEngine;
    private readonly TdsServer tdsServer;
    private readonly HttpServer httpServer;
    private bool isStarted;

    public GatewayServer(IDatabaseAdapter adapter,GatewayServerOptions? options=null){
        ArgumentNullException.ThrowIfNull(adapter);
        this.adapter=adapter;
        this.options=options??new GatewayServerOptions();

        this.sqlEngine=new SqlExecutionEngine(adapter);
        this.tdsServer=new TdsServer(this.options.TdsPort,adapter,this.sqlEngine);
        this.httpServer=new HttpServer(this.options.HttpPort,adapter,this.sqlEngine);
    }

    public int TdsPort=>this.options.TdsPort;
    public int HttpPort=>this.options.HttpPort;

    public void Start(){
        if(this.isStarted){
            return;
        }else{
        }

        this.tdsServer.Start();
        this.httpServer.Start();
        this.isStarted=true;
    }

    public async Task StopAsync(){
        if(!this.isStarted){
            return;
        }else{
        }

        await this.tdsServer.StopAsync();
        await this.httpServer.StopAsync();
        this.isStarted=false;
    }

    public void Dispose(){
        this.StopAsync().GetAwaiter().GetResult();
    }
}

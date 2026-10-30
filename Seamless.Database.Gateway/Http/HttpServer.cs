using global::System.Net;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;

namespace Seamless.Database.Gateway.Http;

public sealed class HttpServer:IDisposable{
    private readonly int port;
    private readonly IDatabaseAdapter adapter;
    private readonly SqlExecutionEngine sqlEngine;
    private HttpListener? listener;
    private CancellationTokenSource? cts;
    private Task? listenTask;

    public HttpServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine){
        this.port=port;
        this.adapter=adapter;
        this.sqlEngine=sqlEngine;
    }

    public int Port=>this.port;

    public void Start(){
        if(this.listener is not null){
            return;
        }else{
        }

        this.cts=new CancellationTokenSource();
        this.listener=new HttpListener();
        this.listener.Prefixes.Add($"http://*:{this.port}/");
        this.listener.Prefixes.Add($"http://localhost:{this.port}/");
        this.listener.Prefixes.Add($"http://127.0.0.1:{this.port}/");

        try{
            this.listener.Start();
        }catch(HttpListenerException){
            // 権限等でワイルドカードプレフィックスに失敗した場合は localhost のみにフォールバック
            this.listener=new HttpListener();
            this.listener.Prefixes.Add($"http://localhost:{this.port}/");
            this.listener.Prefixes.Add($"http://127.0.0.1:{this.port}/");
            this.listener.Start();
        }

        this.listenTask=Task.Run(()=>this.AcceptLoopAsync(this.cts.Token));
    }

    public async Task StopAsync(){
        if(this.listener is null){
            return;
        }else{
        }

        this.cts?.Cancel();
        this.listener.Stop();
        if(this.listenTask is not null){
            try{
                await this.listenTask;
            }catch(OperationCanceledException){
            }catch(ObjectDisposedException){
            }catch(HttpListenerException){
            }
        }else{
        }

        this.listener=null;
        this.cts?.Dispose();
        this.cts=null;
    }

    public void Dispose(){
        this.StopAsync().GetAwaiter().GetResult();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken){
        while(!cancellationToken.IsCancellationRequested){
            try{
                var context=await this.listener!.GetContextAsync();
                _ = Task.Run(()=>HttpEndpoints.HandleRequestAsync(context,this.adapter,this.sqlEngine,cancellationToken),cancellationToken);
            }catch(OperationCanceledException){
                break;
            }catch(HttpListenerException){
                break;
            }catch(ObjectDisposedException){
                break;
            }catch(Exception){
                // 一時的エラーは無視してループ継続
            }
        }
    }
}

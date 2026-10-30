using System.IO.Pipelines;
using global::System.Net;
using global::System.Net.Sockets;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;

namespace Seamless.Database.Gateway.Tds;

public sealed class TdsServer:IDisposable{
    private readonly int port;
    private readonly IDatabaseAdapter adapter;
    private readonly SqlExecutionEngine sqlEngine;
    private TcpListener? listener;
    private CancellationTokenSource? cts;
    private Task? listenTask;
    private long nextSessionId=1;

    public TdsServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine){
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
        this.listener=new TcpListener(IPAddress.Any,this.port);
        this.listener.Server.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);
        this.listener.Start(1024);
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
            }catch(SocketException){
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
                var socket=await this.listener!.AcceptSocketAsync(cancellationToken);
                socket.NoDelay=true;
                var sessionId=Interlocked.Increment(ref this.nextSessionId);
                _ = Task.Run(()=>this.HandleSocketAsync(socket,sessionId,cancellationToken),cancellationToken);
            }catch(OperationCanceledException){
                break;
            }catch(SocketException){
                break;
            }catch(Exception){
                // 接続受付時の一時的エラーは無視してループ継続
            }
        }
    }

    private async Task HandleSocketAsync(Socket socket,long sessionId,CancellationToken cancellationToken){
        using var networkStream=new NetworkStream(socket,ownsSocket:true);
        var pipeReader=PipeReader.Create(networkStream);
        var pipeWriter=PipeWriter.Create(networkStream);

        var session=new TdsSession(sessionId,this.adapter,this.sqlEngine);
        try{
            await session.ProcessConnectionAsync(pipeReader,pipeWriter,cancellationToken);
        }catch(Exception){
            // 切断時やエラー時はソケットを終了
        }finally{
            await pipeReader.CompleteAsync();
            await pipeWriter.CompleteAsync();
        }
    }
}

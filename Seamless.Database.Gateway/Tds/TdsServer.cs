using System.Collections.Concurrent;
using System.IO.Pipelines;
using global::System.Net;
using global::System.Net.Sockets;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;

namespace Seamless.Database.Gateway.Tds;

public sealed class TdsServer:IDisposable{
    private readonly int port;private readonly IDatabaseAdapter adapter;private readonly SqlExecutionEngine sqlEngine;private TcpListener? listener;private TcpListener? ipv6Listener;private CancellationTokenSource? cts;private Task[]? listenTasks;private long nextSessionId;
    private readonly ConcurrentDictionary<long,(Socket Socket,Task Task)> sessions=new();
    public TdsServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine){this.port=port;this.adapter=adapter;this.sqlEngine=sqlEngine;}
    public int Port=>((IPEndPoint?)this.listener?.LocalEndpoint)?.Port??this.port;
    public void Start(){
        if(this.listener is not null){return;}
        var cancellation=new CancellationTokenSource();var ipv4=new TcpListener(IPAddress.Loopback,this.port);TcpListener? ipv6=null;
        try{
            ipv4.Start(1024);
            if(Socket.OSSupportsIPv6){
                var actualPort=((IPEndPoint)ipv4.LocalEndpoint).Port;
                ipv6=new TcpListener(IPAddress.IPv6Loopback,actualPort);ipv6.Server.DualMode=false;ipv6.Start(1024);
            }
            this.cts=cancellation;this.listener=ipv4;this.ipv6Listener=ipv6;
            var tasks=new List<Task>{this.AcceptAsync(ipv4,cancellation.Token)};
            if(ipv6 is not null){tasks.Add(this.AcceptAsync(ipv6,cancellation.Token));}
            this.listenTasks=tasks.ToArray();
        }catch{
            cancellation.Cancel();ipv6?.Stop();ipv4.Stop();cancellation.Dispose();this.listener=null;this.ipv6Listener=null;this.cts=null;this.listenTasks=null;throw;
        }
    }
    public async Task StopAsync(){
        if(this.listener is null){return;}
        var cancellation=this.cts!;cancellation.Cancel();this.listener.Stop();this.ipv6Listener?.Stop();
        Exception? acceptError=null;
        try{if(this.listenTasks is not null){await Task.WhenAll(this.listenTasks);}}
        catch(OperationCanceledException){}catch(SocketException){}catch(ObjectDisposedException){}catch(Exception error){acceptError=error;}
        var active=this.sessions.Values.ToArray();
        try{
            foreach(var session in active){session.Socket.Dispose();}
            await Task.WhenAll(active.Select(session=>session.Task));
        }finally{
            this.listener=null;this.ipv6Listener=null;this.listenTasks=null;cancellation.Dispose();this.cts=null;
        }
        if(acceptError is not null){global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(acceptError).Throw();}
    }
    public void Dispose()=>this.StopAsync().GetAwaiter().GetResult();
    private async Task AcceptAsync(TcpListener listener,CancellationToken token){
        while(!token.IsCancellationRequested){Socket socket;try{socket=await listener.AcceptSocketAsync(token);}catch(OperationCanceledException){break;}catch(SocketException) when(token.IsCancellationRequested){break;}catch(ObjectDisposedException) when(token.IsCancellationRequested){break;}socket.NoDelay=true;var id=Interlocked.Increment(ref this.nextSessionId);var task=this.HandleAsync(socket,id,token);this.sessions[id]=(socket,task);_ = task.ContinueWith(_=>{this.sessions.TryRemove(id,out var removed);},CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);}
    }
    private async Task HandleAsync(Socket socket,long id,CancellationToken token){
        using var stream=new NetworkStream(socket,true);var reader=PipeReader.Create(stream);var writer=PipeWriter.Create(stream);try{await new TdsSession(id,this.adapter,this.sqlEngine).ProcessConnectionAsync(reader,writer,token);}catch(OperationCanceledException) when(token.IsCancellationRequested){}catch(IOException){}catch(SocketException){}finally{await reader.CompleteAsync();await writer.CompleteAsync();}
    }
}

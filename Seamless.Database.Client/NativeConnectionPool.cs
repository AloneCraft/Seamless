using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Seamless.Execution;
using Seamless.Execution.Codecs;

namespace Seamless.Database.Client;

public sealed record NativeConnectionPoolOptions{
    public int MaximumConnections{get;init;}=16;
    public int MaximumWaiters{get;init;}=128;
    public TimeSpan WaitTimeout{get;init;}=TimeSpan.FromSeconds(5);
    public NativeConnectionPolicy? ConnectionPolicy{get;init;}
    public bool UseServerConnectionPolicy{get;init;}
    public string? ServerConnectionPolicyProfile{get;init;}
    public ResourceBudget RecordBudget{get;init;}=NativeRecordResources.DefaultBudget;
    public RecordGraphValidationLimits RecordLimits{get;init;}=RecordGraphValidationLimits.Default;
}

/// <summary>一つの接続先・TLS設定・資格情報の範囲で物理接続を再利用する。</summary>
public sealed class NativeConnectionPool:IDisposable{
    private readonly object sync=new();
    private readonly List<Transport> idle=[];
    private readonly SemaphoreSlim available;
    private readonly SemaphoreSlim waiting;
    private readonly CancellationTokenSource closed=new();
    private readonly Timer idleTimer;
    private readonly NativeConnectionPoolOptions options;
    private readonly string host;
    private readonly int port;
    private readonly bool tls;
    private readonly X509Certificate2? trustedRoot;
    private readonly NativeAuthenticatedPrincipal? principal;
    private readonly Func<CancellationToken,ValueTask<string>>? tokenProvider;
    private bool disposed;

    public NativeConnectionPool(string host,int port,NativeConnectionPoolOptions options):this(host,port,options,false,null,null,null){}

    public static NativeConnectionPool ForTls(string host,int port,NativeConnectionPoolOptions options,X509Certificate2? trustedRoot=null)=>
        new(host,port,options,true,trustedRoot,null,null);

    /// <summary>呼び出し側はプールの全利用に同じ認証主体と資格情報の範囲を使う。</summary>
    public static NativeConnectionPool ForAuthenticatedTls(string host,int port,NativeConnectionPoolOptions options,NativeAuthenticatedPrincipal principal,
        Func<CancellationToken,ValueTask<string>> tokenProvider,X509Certificate2? trustedRoot=null){
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentOutOfRangeException.ThrowIfEqual(principal.AuthorityId,Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(principal.Subject);
        return new(host,port,options,true,trustedRoot,principal,tokenProvider);
    }

    private NativeConnectionPool(string host,int port,NativeConnectionPoolOptions options,bool tls,X509Certificate2? trustedRoot,
        NativeAuthenticatedPrincipal? principal,Func<CancellationToken,ValueTask<string>>? tokenProvider){
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port,1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port,65535);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.RecordBudget);ArgumentNullException.ThrowIfNull(options.RecordLimits);options.RecordLimits.CheckForNative();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumConnections);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaximumWaiters);
        if(options.ServerConnectionPolicyProfile is{} profile){NativeConnectionPolicyWire.ValidateProfile(profile);}
        if(options.WaitTimeout<TimeSpan.FromMilliseconds(1)||options.WaitTimeout.TotalMilliseconds>int.MaxValue){throw new ArgumentOutOfRangeException(nameof(options.WaitTimeout));}
        this.host=host;this.port=port;this.options=options;this.tls=tls;this.trustedRoot=trustedRoot;this.principal=principal;this.tokenProvider=tokenProvider;
        this.available=new(options.MaximumConnections,options.MaximumConnections);
        this.waiting=new(options.MaximumWaiters);
        this.idleTimer=new(static state=>((NativeConnectionPool)state!).ExpireIdle(),this,Timeout.Infinite,Timeout.Infinite);
    }

    /// <summary>接続枠を借りる。取引と結果列挙を終えた後にクライアントを破棄して枠を返す。</summary>
    public async ValueTask<NativeQueryClient> RentAsync(CancellationToken cancellationToken=default){
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(this.closed.IsCancellationRequested,this);
        if(!this.available.Wait(0)){
            if(!this.waiting.Wait(0)){throw new InvalidOperationException("The native connection pool wait queue is full.");}
            try{
                using var wait=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.closed.Token);
                if(!await this.available.WaitAsync(this.options.WaitTimeout,wait.Token).ConfigureAwait(false)){
                    throw new TimeoutException("Timed out waiting for a native connection.");
                }
            }catch(OperationCanceledException)when(this.closed.IsCancellationRequested&&!cancellationToken.IsCancellationRequested){
                throw new ObjectDisposedException(nameof(NativeConnectionPool));
            }finally{this.waiting.Release();}
        }
        try{
            lock(this.sync){
                cancellationToken.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(this.disposed,this);
                while(this.idle.Count!=0){
                    var transport=this.idle[^1];
                    this.idle.RemoveAt(this.idle.Count-1);
                    if(transport.IsReusable){
                        try{return this.CreateClient(transport);}
                        catch{transport.Dispose();throw;}
                    }
                    transport.Dispose();
                }
                return this.CreateClient(null);
            }
        }catch{this.available.Release();throw;}
    }

    private NativeQueryClient CreateClient(Transport? transport)=>
        NativeQueryClient.CreatePooled(this,this.host,this.port,this.options,this.tls,this.trustedRoot,this.principal,this.tokenProvider,transport);

    internal void Return(Transport? transport){
        try{
            if(transport is null){return;}
            lock(this.sync){
                if(!this.disposed&&transport.IsReusable){
                    this.idle.Add(transport);
                    if(transport.IdleTimeout is not null){this.ScheduleExpiry();}
                    return;
                }
            }
            transport.Dispose();
        }finally{this.available.Release();}
    }

    private void ExpireIdle(){
        lock(this.sync){
            if(this.disposed){return;}
            for(var index=this.idle.Count-1;index>=0;index--){
                var transport=this.idle[index];
                if(transport.Remaining is not{} time||time>TimeSpan.Zero){continue;}
                this.idle.RemoveAt(index);
                transport.Dispose();
            }
            this.ScheduleExpiry();
        }
    }

    private void ScheduleExpiry(){
        var next=Timeout.InfiniteTimeSpan;
        foreach(var transport in this.idle){
            if(transport.Remaining is{} time&&(next==Timeout.InfiniteTimeSpan||time<next)){next=time;}
        }
        if(next!=Timeout.InfiniteTimeSpan&&next<TimeSpan.FromMilliseconds(1)){next=TimeSpan.FromMilliseconds(1);}
        this.idleTimer.Change(next,Timeout.InfiniteTimeSpan);
    }

    public void Dispose(){
        lock(this.sync){
            if(this.disposed){return;}
            this.disposed=true;
            this.idleTimer.Dispose();
        }
        this.closed.Cancel();
        foreach(var transport in this.idle){transport.Dispose();}
        this.idle.Clear();
    }

    internal sealed record Transport(TcpClient Socket,Stream Stream,NativeAuthenticatedPrincipal? Principal,NativeConnectionPolicy? Recommendation,
        bool Discovered,TimeSpan? IdleTimeout,long IdleSince):IDisposable{
        internal TimeSpan? Remaining=>this.IdleTimeout-Stopwatch.GetElapsedTime(this.IdleSince);
        internal bool IsReusable{
            get{
                if(this.Remaining<=TimeSpan.Zero){return false;}
                // アイドル中の読み取り可能状態はFIN・エラー・未読データのいずれでも廃棄する。
                try{return !this.Socket.Client.Poll(0,SelectMode.SelectRead);}
                catch(SocketException){return false;}
                catch(ObjectDisposedException){return false;}
            }
        }
        public void Dispose(){
            try{this.Stream.Dispose();}
            finally{this.Socket.Dispose();}
        }
    }
}

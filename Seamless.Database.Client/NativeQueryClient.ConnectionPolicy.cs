using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Seamless.Database.Client;

public enum NativeConnectionLifetime{KeepAlive,ReleaseWhenIdle,ReleaseAfterOperation}

public sealed record NativeConnectionPolicy{
    public NativeConnectionLifetime Lifetime{get;}
    public TimeSpan? IdleTimeout{get;}
    public NativeConnectionPolicy(NativeConnectionLifetime lifetime,TimeSpan? idleTimeout=null){
        if(!Enum.IsDefined(lifetime)){throw new ArgumentOutOfRangeException(nameof(lifetime));}
        if(lifetime==NativeConnectionLifetime.ReleaseWhenIdle){
            if(idleTimeout is not{} timeout||timeout<TimeSpan.FromMilliseconds(1)||timeout.TotalMilliseconds>uint.MaxValue-1){
                throw new ArgumentOutOfRangeException(nameof(idleTimeout));
            }
        }else if(idleTimeout.HasValue){throw new ArgumentException("An idle timeout requires the idle release policy.",nameof(idleTimeout));}
        this.Lifetime=lifetime;
        this.IdleTimeout=idleTimeout;
    }
}

public sealed partial class NativeQueryClient{
    private static readonly NativeConnectionPolicy DefaultConnectionPolicy=new(NativeConnectionLifetime.KeepAlive);
    private Timer? idleTimer;
    private long idleSince;
    private NativeAuthenticatedPrincipal? reconnectPrincipal;
    private NativeConnectionPolicy? configuredConnectionPolicy;
    private string? serverConnectionPolicyProfile;
    private bool connectionPolicyDiscovered;
    /// <summary>初回の接続時にサーバー推奨値を取得する。明示設定がある場合は取得を省略する。</summary>
    public bool UseServerConnectionPolicy{get;init;}
    public string? ServerConnectionPolicyProfile{
        get=>this.serverConnectionPolicyProfile;
        init{
            if(value is not null){NativeConnectionPolicyWire.ValidateProfile(value);}
            this.serverConnectionPolicyProfile=value;
        }
    }
    public NativeConnectionPolicy? ServerRecommendedConnectionPolicy{get;private set;}
    public NativeConnectionPolicy ConnectionPolicy=>this.configuredConnectionPolicy??this.ServerRecommendedConnectionPolicy??DefaultConnectionPolicy;

    public NativeQueryClient(string host,int port,NativeConnectionPolicy connectionPolicy):this(host,port){
        this.configuredConnectionPolicy=connectionPolicy??throw new ArgumentNullException(nameof(connectionPolicy));
    }

    public static NativeQueryClient ForTls(string host,int port,X509Certificate2? trustedRoot,NativeConnectionPolicy connectionPolicy)=>
        new(host,port,trustedRoot){configuredConnectionPolicy=connectionPolicy??throw new ArgumentNullException(nameof(connectionPolicy))};

    public static NativeQueryClient ForAuthenticatedTls(Func<CancellationToken,ValueTask<string>> accessTokenProvider,string host,int port,X509Certificate2? trustedRoot,NativeConnectionPolicy connectionPolicy)=>
        new(host,port,trustedRoot,accessTokenProvider){configuredConnectionPolicy=connectionPolicy??throw new ArgumentNullException(nameof(connectionPolicy))};

    public static NativeQueryClient ForTlsWithServerConnectionPolicy(string host="localhost",int port=14335,X509Certificate2? trustedRoot=null)=>
        new(host,port,trustedRoot){UseServerConnectionPolicy=true};

    public static NativeQueryClient ForAuthenticatedTlsWithServerConnectionPolicy(Func<CancellationToken,ValueTask<string>> accessTokenProvider,string host="localhost",int port=14335,X509Certificate2? trustedRoot=null)=>
        new(host,port,trustedRoot,accessTokenProvider){UseServerConnectionPolicy=true};

    public static NativeQueryClient ForAuthenticatedTlsWithServerConnectionPolicy(string profile,Func<CancellationToken,ValueTask<string>> accessTokenProvider,string host="localhost",int port=14335,X509Certificate2? trustedRoot=null)=>
        new(host,port,trustedRoot,accessTokenProvider){UseServerConnectionPolicy=true,ServerConnectionPolicyProfile=profile??throw new ArgumentNullException(nameof(profile))};

    private async Task DiscoverConnectionPolicyAsync(Stream transport,CancellationToken cancellationToken){
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        await NativeFrame.WriteAsync(transport,NativeConnectionPolicyWire.Discover,NativeConnectionPolicyWire.WriteDiscovery(this.ServerConnectionPolicyProfile),deadline.Token).ConfigureAwait(false);
        var response=await NativeFrame.ReadAsync(transport,deadline.Token,static kind=>kind switch{
            NativeConnectionPolicyWire.Recommendation=>NativeConnectionPolicyWire.RecommendationBytes,
            NativeFrame.Error or NativeFrame.RecoverableError=>32768,
            _=>throw new InvalidDataException("Unexpected native connection policy response.")
        }).ConfigureAwait(false)??throw new EndOfStreamException("The server disconnected before returning its native connection policy.");
        if(response.Kind!=NativeConnectionPolicyWire.Recommendation){
            throw new NativeQueryException(System.Text.Encoding.UTF8.GetString(response.Payload),response.Kind==NativeFrame.RecoverableError);
        }
        this.ServerRecommendedConnectionPolicy=NativeConnectionPolicyWire.ReadRecommendation(response.Payload);
        this.connectionPolicyDiscovered=true;
    }

    // 取引と結果列挙が終わるまで、呼び出し元が直列化ゲートを持つ。
    private void ReleaseOperation(bool preservePreconnected=false){
        if(this.ConnectionPolicy.IdleTimeout is{} idleTimeout){
            // 短い期限でも、タイマーはゲート解放前に期限切れを処理しない。
            lock(this.gate){
                try{
                    if(this.stream is not null&&!this.disposed){
                        this.idleSince=Stopwatch.GetTimestamp();
                        this.ScheduleIdleExpiry(idleTimeout);
                    }
                }finally{this.gate.Release();}
            }
            return;
        }
        try{
            if(!preservePreconnected&&this.ConnectionPolicy.Lifetime==NativeConnectionLifetime.ReleaseAfterOperation){this.CloseConnection();}
        }finally{this.gate.Release();}
    }

    private void ScheduleIdleExpiry(TimeSpan remaining){
        this.idleTimer??=new(static state=>((NativeQueryClient)state!).ExpireIdleConnection(),this,Timeout.InfiniteTimeSpan,Timeout.InfiniteTimeSpan);
        if(remaining<TimeSpan.FromMilliseconds(1)){remaining=TimeSpan.FromMilliseconds(1);}
        this.idleTimer.Change(remaining,Timeout.InfiniteTimeSpan);
    }

    private void ExpireIdleConnection(){
        lock(this.gate){
            if(this.disposed||!this.gate.Wait(0)){return;}
            try{
                if(this.stream is null){return;}
                var remaining=this.ConnectionPolicy.IdleTimeout!.Value-Stopwatch.GetElapsedTime(this.idleSince);
                if(remaining>TimeSpan.Zero){
                    this.ScheduleIdleExpiry(remaining);
                    return;
                }
                this.CloseConnection();
            }finally{this.gate.Release();}
        }
    }
}

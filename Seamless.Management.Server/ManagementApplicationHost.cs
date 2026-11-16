using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Seamless.Management.Server;

public sealed record ManagementHostOptions(string Root,ManagementExecutionMode Mode){
    public string? Certificate{get;init;}
    public string? CertificatePassword{get;init;}
    public ManagementServerSettings? Defaults{get;init;}
    public int EmbeddedQueueCapacity{get;init;}=64;
    public int ConcurrentCapacity{get;init;}=32;
}

/// <summary>DBを所有するアプリに、共通Manager・設定・管理受付の寿命を組み込む。</summary>
public sealed class ManagementApplicationHost:IAsyncDisposable{
    private ManagementStore? store;
    private bool ownsStore;
    private ManagementService? service;
    private ConcurrentManagementOperations? concurrent;
    private ManagementServer? server;
    private X509Certificate2? certificate;
    private LocalServerConnection? local;
    private LocalManagerConnection? localManager;
    private readonly CancellationTokenSource stopping=new();
    private readonly object disposalGate=new();
    private Task? disposal;
    private Task audit=Task.CompletedTask;
    public ManagementExecutionMode Mode{get;private set;}
    public IManagementOperations Operations{get;private set;}=null!;
    public InProcessManagementHost? Embedded{get;private set;}
    public ManagerWebHost Manager{get;private set;}=null!;
    public ManagerWebHost? ExternalManager{get;private set;}
    public ManagementServerSettings RunningSettings{get;private set;}=null!;
    public int ProcessId=>Environment.ProcessId;
    public int TcpPort=>this.server?.Port??0;
    public string? PipeName=>this.server?.PipeName;
    private ManagementApplicationHost(){}

    public static Task<ManagementApplicationHost> CreateAsync(ManagementHostOptions options,CancellationToken cancellationToken=default)=>StartAsync(options,true,cancellationToken);
    public static Task<ManagementApplicationHost> OpenAsync(ManagementHostOptions options,CancellationToken cancellationToken=default)=>StartAsync(options,false,cancellationToken);
    private static async Task<ManagementApplicationHost> StartAsync(ManagementHostOptions options,bool create,CancellationToken cancellation){
        ArgumentNullException.ThrowIfNull(options);ArgumentException.ThrowIfNullOrWhiteSpace(options.Root);cancellation.ThrowIfCancellationRequested();
        if(!Enum.IsDefined(options.Mode)||options.EmbeddedQueueCapacity is <1 or >65536||options.ConcurrentCapacity is <1 or >1024){throw new ArgumentException("実行形態と有効な受付容量を指定してください。",nameof(options));}
        if(options.Mode==ManagementExecutionMode.Server){ArgumentException.ThrowIfNullOrWhiteSpace(options.Certificate);}
        var host=new ManagementApplicationHost{Mode=options.Mode};
        try{
            if(options.Mode==ManagementExecutionMode.Embedded){
                host.Embedded=InProcessManagementHost.OpenConfigured(options.Root,create,options.Defaults,options.EmbeddedQueueCapacity);
                host.Operations=host.Embedded;host.RunningSettings=host.Embedded.RunningSettings!;
                if(host.RunningSettings.ExternalManagerOrigin is not null){throw new ManagementException("ExecutionMode","外部Managerの受付にはサーバー型を指定してください。");}
                host.Manager=await ManagerWebHost.StartAsync(host.Embedded,host.RunningSettings.ManagerPort,sessionCapacity:1,cancellationToken:cancellation).ConfigureAwait(false);
                await host.Embedded.InvokeAsync((service,_)=>{
                    service.AttachServerSettings(new(host.Embedded.SettingsDefaults!,host.RunningSettings,[],"",null,0,host.Manager.Origin.AbsoluteUri){Mode=ManagementExecutionMode.Embedded});return true;
                },cancellation).ConfigureAwait(false);
                host.audit=host.RunEmbeddedAuditAsync();
            }else{
                if(create){host.store=ManagementStore.Initialize(options.Root,"local-owner",Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));}
                else{host.store=new(options.Root);}
                host.ownsStore=true;
                var defaults=(options.Defaults??new(){ReadAuditEnabled=host.store.Format>=5}).Validate();
                var running=(ManagementService.SavedServerSettings(host.store)?.Settings??defaults).Validate();
                await host.StartServerAsync(host.store,defaults,running,[],options.Certificate!,options.CertificatePassword,null,options.ConcurrentCapacity,cancellation).ConfigureAwait(false);
            }
            return host;
        }catch(Exception startup){
            try{await host.DisposeAsync().ConfigureAwait(false);}catch(Exception cleanup){throw new AggregateException("管理ホストの起動と後処理に失敗しました。",startup,cleanup);}throw;
        }
    }
    internal static async Task<ManagementApplicationHost> StartServerAsync(ManagementStore store,ManagementServerSettings defaults,ManagementServerSettings running,string[] overrides,string certificatePath,string? password,string? configuration,CancellationToken cancellation=default){
        var host=new ManagementApplicationHost{Mode=ManagementExecutionMode.Server,store=store};
        try{await host.StartServerAsync(store,defaults,running,overrides,certificatePath,password,configuration,32,cancellation).ConfigureAwait(false);return host;}
        catch(Exception startup){
            try{await host.DisposeAsync().ConfigureAwait(false);}catch(Exception cleanup){throw new AggregateException("管理ホストの起動と後処理に失敗しました。",startup,cleanup);}throw;
        }
    }
    private async Task StartServerAsync(ManagementStore store,ManagementServerSettings defaults,ManagementServerSettings running,string[] overrides,string certificatePath,string? password,string? configuration,int capacity,CancellationToken cancellation){
        this.RunningSettings=running.Validate();this.service=new(store,ManagementService.AuditOptions(store,running)){UseSdkBuilds=true};
        this.certificate=X509CertificateLoader.LoadPkcs12FromFile(certificatePath,password);
        this.concurrent=new(this.service,capacity);this.Operations=this.concurrent;
        this.Manager=await ManagerWebHost.StartAsync(this.concurrent,running.ManagerPort,cancellationToken:cancellation).ConfigureAwait(false);
        if(running.ExternalManagerOrigin is{} origin){
            var external=new Uri(origin);this.ExternalManager=await ManagerWebHost.StartExternalAsync(this.service,this.concurrent,this.certificate,IPAddress.Parse(running.ExternalManagerAddress),external.Port,external,cancellationToken:cancellation).ConfigureAwait(false);
        }
        this.server=ManagementServer.ForLocalOwner(this.service,this.certificate,new(IPAddress.Parse(running.Address),running.Port),this.Manager.Sessions,running.MaximumBufferedRequestBytes,running.MaximumPendingWriteBytes,running.ResponseWriteTimeout);
        this.server.ShareExecutionSlots(this.concurrent);
        cancellation.ThrowIfCancellationRequested();this.server.Start();
        this.service.AttachServerSettings(new(defaults,running,overrides,Path.GetFullPath(certificatePath),configuration,this.server.Port,this.Manager.Origin.AbsoluteUri));
        var address=IPAddress.Parse(running.Address);
        if(IPAddress.IsLoopback(address)){this.local=LocalServerConnection.Publish(store.Root,store.InstanceId,this.server.Port,this.certificate,this.server.PipeName,address,this.server.UnixPipePath);}
        this.localManager=LocalManagerConnection.Publish(store.Root,store.InstanceId,this.server.PipeName!,this.server.UnixPipePath);
    }
    private async Task RunEmbeddedAuditAsync(){
        var readInterval=TimeSpan.FromSeconds(this.RunningSettings.ReadAuditSaveSeconds);var transferInterval=TimeSpan.FromMilliseconds(250);
        var period=transferInterval;if(this.RunningSettings.ReadAuditEnabled&&readInterval<period){period=readInterval;}
        using var timer=new PeriodicTimer(period);var readAt=System.Diagnostics.Stopwatch.GetTimestamp();var transferAt=readAt;
        try{
            while(await timer.WaitForNextTickAsync(this.stopping.Token).ConfigureAwait(false)){
                var read=this.RunningSettings.ReadAuditEnabled&&System.Diagnostics.Stopwatch.GetElapsedTime(readAt)>=readInterval;
                var transfer=System.Diagnostics.Stopwatch.GetElapsedTime(transferAt)>=transferInterval;
                if(!read&&!transfer){continue;}var now=System.Diagnostics.Stopwatch.GetTimestamp();if(read){readAt=now;}if(transfer){transferAt=now;}
                await this.Embedded!.InvokeAsync((service,_)=>{if(read){service.FlushReadAuditAsync().GetAwaiter().GetResult();}if(transfer){service.FlushAuditTransfersAsync().GetAwaiter().GetResult();}return true;},this.stopping.Token).ConfigureAwait(false);
            }
        }catch(OperationCanceledException)when(this.stopping.IsCancellationRequested){}
    }
    public ValueTask DisposeAsync(){lock(this.disposalGate){this.disposal??=this.StopAsync();return new(this.disposal);}}
    private async Task StopAsync(){
        this.concurrent?.StopAccepting();this.stopping.Cancel();var failures=new List<Exception>();
        async Task Close(IAsyncDisposable? value){if(value is null){return;}try{await value.DisposeAsync().ConfigureAwait(false);}catch(Exception error){failures.Add(error);}}
        void Release(Action action){try{action();}catch(Exception error){failures.Add(error);}}
        await Close(this.server).ConfigureAwait(false);await Close(this.ExternalManager).ConfigureAwait(false);await Close(this.Manager).ConfigureAwait(false);
        try{await this.audit.ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        await Close(this.concurrent).ConfigureAwait(false);
        Release(()=>this.Embedded?.Dispose());Release(()=>this.service?.Dispose());
        if(this.store is{} store){Release(()=>this.localManager?.Remove(store.Root));Release(()=>this.local?.Remove(store.Root));if(this.ownsStore){Release(store.Dispose);}}
        Release(()=>this.certificate?.Dispose());Release(this.stopping.Dispose);
        if(failures.Count>0){throw new AggregateException("管理ホストの停止中にエラーが発生しました。",failures);}
    }
}

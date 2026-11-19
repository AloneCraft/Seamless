using System.Security.Cryptography;
using System.Threading.Channels;

namespace Seamless.Management.Server;

/// <summary>組み込み試作で管理領域と管理操作を、同じプロセスの専用DB実行スレッドで所有する。</summary>
public sealed partial class InProcessManagementHost:IDisposable,IManagementOperations,IManagedDatabaseSourceOperations{
    bool IManagedDatabaseSourceOperations.CanOpenManagedDatabaseSource=>true;
    Task<string> IManagedDatabaseSourceOperations.OpenManagedDatabaseSourceAsync(string id,CancellationToken cancellationToken)=>
        this.InvokeAsync((service,user)=>service.OpenManagedDatabaseSource(user,id),cancellationToken).AsTask();
    private ManagementStore? store;
    private ManagementService? service;
    private readonly ExecutionThread execution;
    private readonly UserAccount owner=LocalOwnerIdentity.Capture().Account();
    public int ProcessId=>Environment.ProcessId;
    public int ExecutionThreadId=>this.execution.ThreadId;
    public UserInfo Owner=>new(this.owner.Name,this.owner.Grants.ToArray(),false);
    internal ManagementServerSettings? SettingsDefaults{get;private set;}
    internal ManagementServerSettings? RunningSettings{get;private set;}

    private InProcessManagementHost(string root,bool create,int queueCapacity,ManagementServerSettings? defaults=null,bool configured=false){
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        this.execution=new(queueCapacity,()=>{
            if(create){this.store=ManagementStore.Initialize(root,"local-owner",Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));}
            else{this.store=new(root);}
            if(configured){
                this.SettingsDefaults=(defaults??new(){ReadAuditEnabled=this.store.Format>=5}).Validate();
                this.RunningSettings=(ManagementService.SavedServerSettings(this.store)?.Settings??this.SettingsDefaults).Validate();
                this.service=new(this.store,ManagementService.AuditOptions(this.store,this.RunningSettings),startAuditWorker:false,startReadAuditWorker:false);
                this.service.AuditExternallyScheduled=true;
            }else{this.service=new(this.store,null,startAuditWorker:false);}
        },()=>{
            try{
                try{if(this.RunningSettings is not null){this.service?.FlushReadAuditAsync().GetAwaiter().GetResult();this.service?.FlushAuditTransfersAsync().GetAwaiter().GetResult();}}
                finally{try{this.CloseResults();}finally{this.service?.Dispose();}}
            }
            finally{this.store?.Dispose();}
        });
    }

    public static InProcessManagementHost Create(string root,int queueCapacity=64)=>new(root,true,queueCapacity);
    public static InProcessManagementHost Open(string root,int queueCapacity=64)=>new(root,false,queueCapacity);
    internal static InProcessManagementHost OpenConfigured(string root,bool create,ManagementServerSettings? defaults,int queueCapacity)=>new(root,create,queueCapacity,defaults,true);
    public ServerInfo Info=>this.Invoke(static(service,_)=>OwnerInfo(service.Info));
    public Task<ServerInfo> InfoAsync(CancellationToken cancellationToken=default)=>
        this.InvokeAsync(static(service,_)=>OwnerInfo(service.Info),cancellationToken).AsTask();
    private static ServerInfo OwnerInfo(ServerInfo info)=>info with{Capabilities=[..info.Capabilities,"OwnerSourceProjectsV1","OwnerSourceDirectoryV1","OwnerSourceDraftsV1","OwnerSourceBuildsV1","OwnerSourceSdkBuildV1","OwnerSourceBuildImportsV1","OwnerSourceBuildPublicationV1","OwnerSourceConflictsV1","OwnerSourceConfigurationsV1"]};
    public CatalogPage<CatalogDatabase> Databases(long cursor=0,string versionToken="")=>this.Invoke((service,user)=>service.Databases(user,cursor,versionToken));
    public Task<CatalogPage<CatalogDatabase>> DatabasesAsync(long cursor=0,string versionToken="",CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.Databases(user,cursor,versionToken),cancellationToken).AsTask();

    internal T Invoke<T>(Func<ManagementService,UserAccount,T> operation,CancellationToken cancellationToken=default)=>
        this.execution.InvokeAsync(()=>operation(this.service!,this.owner),cancellationToken).AsTask().GetAwaiter().GetResult();
    internal ValueTask<T> InvokeAsync<T>(Func<ManagementService,UserAccount,T> operation,CancellationToken cancellationToken=default)=>
        this.execution.InvokeAsync(()=>operation(this.service!,this.owner),cancellationToken);
    public void Dispose(){
        if(Environment.CurrentManagedThreadId==this.ExecutionThreadId){throw new InvalidOperationException("DB実行スレッド内からホストを破棄できません。");}
        this.sourceProjects.Dispose();this.execution.Dispose();
    }

    private sealed class ExecutionThread:IDisposable{
        private readonly Channel<IWork> queue;
        private readonly Thread thread;
        private readonly TaskCompletionSource stopped=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int stopping;
        public int ThreadId=>this.thread.ManagedThreadId;

        public ExecutionThread(int capacity,Action initialize,Action cleanup){
            if(capacity<=0){throw new ArgumentOutOfRangeException(nameof(capacity));}
            this.queue=Channel.CreateBounded<IWork>(new BoundedChannelOptions(capacity){SingleReader=true,FullMode=BoundedChannelFullMode.Wait,AllowSynchronousContinuations=false});
            var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this.thread=new Thread(()=>{
                Exception? failure=null;
                try{
                    initialize();ready.TrySetResult();
                    while(this.queue.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult()){
                        while(this.queue.Reader.TryRead(out var work)){work.Execute();}
                    }
                }catch(Exception error){
                    failure=error;
                    ready.TrySetException(error);
                    this.queue.Writer.TryComplete(error);
                    while(this.queue.Reader.TryRead(out var work)){work.Fail(error);}
                }finally{
                    try{cleanup();}
                    catch(Exception error){
                        if(failure is null){failure=error;}
                        else{failure=new AggregateException(failure,error);}
                    }
                    if(failure is null){this.stopped.TrySetResult();}
                    else{this.stopped.TrySetException(failure);}
                }
            }){IsBackground=true,Name="Seamless embedded DB"};
            this.thread.Start();
            try{ready.Task.GetAwaiter().GetResult();}
            catch{this.thread.Join();this.stopped.Task.GetAwaiter().GetResult();throw;}
        }

        public T Invoke<T>(Func<T> operation)=>this.InvokeAsync(operation).AsTask().GetAwaiter().GetResult();
        public async ValueTask<T> InvokeAsync<T>(Func<T> operation,CancellationToken cancellationToken=default){
            ArgumentNullException.ThrowIfNull(operation);
            // 停止中も受入済み処理の内部呼び出しは完了させ、新規の外部要求だけを拒否する。
            if(Environment.CurrentManagedThreadId==this.ThreadId){cancellationToken.ThrowIfCancellationRequested();return operation();}
            ObjectDisposedException.ThrowIf(Volatile.Read(ref this.stopping)!=0,this);
            cancellationToken.ThrowIfCancellationRequested();
            using var work=new Work<T>(operation,cancellationToken);
            try{await this.queue.Writer.WriteAsync(work,cancellationToken).ConfigureAwait(false);}
            catch(ChannelClosedException){throw new ObjectDisposedException(nameof(InProcessManagementHost));}
            return await work.Result.ConfigureAwait(false);
        }
        public void Dispose(){
            if(Environment.CurrentManagedThreadId==this.ThreadId){throw new InvalidOperationException("DB実行スレッド内からホストを破棄できません。");}
            if(Interlocked.Exchange(ref this.stopping,1)==0){this.queue.Writer.TryComplete();}
            this.thread.Join();
            this.stopped.Task.GetAwaiter().GetResult();
        }
        private interface IWork{
            void Execute();
            void Fail(Exception error);
        }
        private sealed class Work<T>:IWork,IDisposable{
            private readonly TaskCompletionSource<T> completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly Func<T> operation;
            private readonly CancellationToken cancellationToken;
            private readonly CancellationTokenRegistration registration;
            private int state;
            public Work(Func<T> operation,CancellationToken cancellationToken){
                this.operation=operation;this.cancellationToken=cancellationToken;
                this.registration=cancellationToken.UnsafeRegister(static value=>((Work<T>)value!).CancelQueued(),this);
            }
            public Task<T> Result=>this.completion.Task;
            private void CancelQueued(){if(Interlocked.CompareExchange(ref this.state,2,0)==0){this.completion.TrySetCanceled(this.cancellationToken);}}
            public void Execute(){
                // 取消が実行開始より先なら本体を呼ばない。開始後は本体の確定・取消結果を待つ。
                if(Interlocked.CompareExchange(ref this.state,1,0)!=0){return;}
                if(this.cancellationToken.IsCancellationRequested){this.completion.TrySetCanceled(this.cancellationToken);return;}
                try{this.completion.TrySetResult(this.operation());}
                catch(Exception error){this.completion.TrySetException(error);}
            }
            public void Fail(Exception error)=>this.completion.TrySetException(error);
            public void Dispose()=>this.registration.Dispose();
        }
    }
}

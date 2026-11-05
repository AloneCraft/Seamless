using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Seamless.Management.Server;

public sealed class ManagementServer:IAsyncDisposable{
    private readonly ManagementService service;
    private readonly X509Certificate2 certificate;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource stopping=new();
    private readonly ConcurrentDictionary<long,Task> connections=new();
    private long sequence;
    private Task? accept;
    private Task? pipeAccept;
    private readonly string? localUser;
    public string? PipeName{get;}
    public event Action<Exception>? ConnectionFailed;
    public ManagementServer(ManagementService service,X509Certificate2 certificate,IPEndPoint endpoint,string? localUser=null){if(!certificate.HasPrivateKey){throw new ArgumentException("秘密鍵を持つサーバー証明書が必要です。",nameof(certificate));}this.service=service;this.certificate=certificate;this.listener=new(endpoint);this.localUser=localUser;if(localUser is not null){ManagementStore.ValidateAccountName(localUser);this.PipeName=LocalServerConnection.PipeNameFor(service.Info.InstanceId);}}
    public int Port=>((IPEndPoint)this.listener.LocalEndpoint).Port;
    public void Start(){if(this.accept is not null){throw new InvalidOperationException("起動済みです。");}this.listener.Start();this.accept=this.AcceptAsync();if(this.PipeName is not null){this.pipeAccept=this.AcceptPipeAsync();}}
    private async Task AcceptPipeAsync(){
        try{while(!this.stopping.IsCancellationRequested){
            var pipe=new NamedPipeServerStream(this.PipeName!,PipeDirection.InOut,32,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            try{await pipe.WaitForConnectionAsync(this.stopping.Token);}catch{pipe.Dispose();throw;}
            if(this.connections.Count>=32){pipe.Dispose();continue;}
            var id=Interlocked.Increment(ref this.sequence);var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);this.connections[id]=completion.Task;
            _=Task.Run(async()=>{try{await this.ServeRequestAsync(pipe,this.localUser);}finally{pipe.Dispose();this.connections.TryRemove(id,out _);completion.TrySetResult();}});
        }}catch(OperationCanceledException)when(this.stopping.IsCancellationRequested){}
    }
    private async Task AcceptAsync(){
        try{while(!this.stopping.IsCancellationRequested){var socket=await this.listener.AcceptTcpClientAsync(this.stopping.Token);socket.NoDelay=true;if(this.connections.Count>=32){socket.Dispose();continue;}
            var id=Interlocked.Increment(ref this.sequence);var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);this.connections[id]=completion.Task;
            _=Task.Run(async()=>{try{await this.ServeAsync(socket);}finally{socket.Dispose();this.connections.TryRemove(id,out _);completion.TrySetResult();}});
        }}catch(OperationCanceledException)when(this.stopping.IsCancellationRequested){}catch(SocketException)when(this.stopping.IsCancellationRequested){}
    }
    private async Task ServeAsync(TcpClient client){
        await using var stream=new SslStream(client.GetStream(),leaveInnerStreamOpen:false);
        try{using var handshake=CancellationTokenSource.CreateLinkedTokenSource(this.stopping.Token);handshake.CancelAfter(TimeSpan.FromSeconds(15));await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=this.certificate},handshake.Token);await this.ServeRequestAsync(stream,null);}
        catch(Exception error){this.ConnectionFailed?.Invoke(error);}
    }
    private async Task ServeRequestAsync(Stream stream,string? localUser){
        RpcRequest? request=null;
        try{
            using var handshake=CancellationTokenSource.CreateLinkedTokenSource(this.stopping.Token);handshake.CancelAfter(TimeSpan.FromSeconds(15));
            request=await ManagementWire.ReadAsync<RpcRequest>(stream,handshake.Token);
            if(request.Operation=="info"){var info=this.service.Info;if(this.PipeName is not null){info=info with{Capabilities=[..info.Capabilities,"SameUserPipeV1"]};}await this.Send(stream,request.Id,"result",info);return;}
            if(request.Operation=="login"){await this.Send(stream,request.Id,"result",this.service.Login(ManagementJson.Read<LoginRequest>(request.Payload)));return;}
            UserAccount user;if(localUser is not null){user=this.service.AuthenticateLocal(localUser);}else{user=this.service.Authenticate(request.Token);}
            if(request.Operation=="localIdentity"){if(localUser is null){throw new ManagementException("Unsupported","この接続はローカルIPCではありません。");}await this.Send(stream,request.Id,"result",new UserInfo(user.Name,user.Grants,user.Disabled));return;}
            if(request.Operation=="execute"){
                foreach(var row in this.service.Execute(user,ManagementJson.Read<ExecuteRequest>(request.Payload),this.stopping.Token)){await this.Send(stream,request.Id,"row",row);}
                await this.Send(stream,request.Id,"complete",new{Complete=true});return;
            }
            object result=request.Operation switch{
                "catalogDatabases"=>this.service.Databases(user,Cursor(request.Payload),request.Payload.TryGetProperty("versionToken",out var catalogVersion)?catalogVersion.GetString()??"":""),
                "catalogSchemas"=>this.service.Schemas(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "catalogObjects"=>this.service.Objects(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "catalogDetails"=>this.service.ObjectDetails(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "catalogSource"=>this.service.ObjectSource(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "definitions"=>this.service.Definitions(user,Cursor(request.Payload)),
                "definition"=>this.service.GetDefinition(user,Id(request.Payload)),
                "draft"=>this.service.GetDraft(user,Id(request.Payload)),
                "drafts"=>this.service.Drafts(user,Cursor(request.Payload)),
                "saveDraft"=>this.service.SaveDraft(user,ManagementJson.Read<SaveDraftRequest>(request.Payload)),
                "build"=>this.service.Build(user,ManagementJson.Read<DraftKey>(request.Payload)),
                "analyze"=>this.service.Analyze(user,ManagementJson.Read<Definition>(request.Payload)),
                "version"=>this.service.Version(user,Id(request.Payload),request.Payload.GetProperty("version").GetInt64()),
                "preview"=>this.service.Preview(user,ManagementJson.Read<DraftKey>(request.Payload)),
                "apply"=>this.service.Apply(user,ManagementJson.Read<ApplyRequest>(request.Payload)),
                "operation"=>this.service.GetOperation(user,ManagementJson.Read<Guid>(request.Payload)),
                "validation"=>this.Validation(user,request.Payload),
                "validations"=>this.service.Validations(user,Cursor(request.Payload)),
                "dropValidation"=>this.DropValidation(user,request.Payload),
                "plans"=>this.service.Plans(user,Id(request.Payload)),
                "publishedAnalysis"=>this.service.AnalyzePublished(user,Id(request.Payload)),
                "history"=>this.service.History(user,Cursor(request.Payload)),
                "users"=>this.service.Users(user,Cursor(request.Payload)),
                "cancel"=>this.Cancel(user,request.Payload),
                "seed"=>this.Seed(user,request.Payload),
                "changeUser"=>this.ChangeUser(user,request.Payload),
                "changePassword"=>this.ChangePassword(user,request.Payload),
                _=>throw new ManagementException("Unsupported","未対応の管理操作です。")
            };
            await this.Send(stream,request.Id,"result",result);
        }catch(Exception error){
            if(request is null){this.ConnectionFailed?.Invoke(error);return;}var actual=error;if(error is System.Reflection.TargetInvocationException{InnerException:{} inner}){actual=inner;}
            var code="OperationFailed";if(actual is ManagementException management){code=management.Code;}else if(actual is OperationCanceledException){code="Cancelled";}
            try{await ManagementWire.WriteAsync(stream,new RpcResponse(request.Id,"error",ManagementJson.Element(new{}),code,actual.Message),this.stopping.Token);}catch(IOException){}catch(OperationCanceledException){}
        }
    }
    private object Cancel(UserAccount user,JsonElement payload){this.service.Cancel(user,ManagementJson.Read<Guid>(payload));return new{Accepted=true};}
    private object Seed(UserAccount user,JsonElement payload){this.service.Seed(user,ManagementJson.Read<SeedRequest>(payload));return new{Completed=true};}
    private object DropValidation(UserAccount user,JsonElement payload){this.service.DropValidation(user,Id(payload));return new{Completed=true};}
    private object ChangeUser(UserAccount user,JsonElement payload){this.service.ChangeUser(user,ManagementJson.Read<ChangeUserRequest>(payload));return new{Completed=true};}
    private object ChangePassword(UserAccount user,JsonElement payload){this.service.ChangePassword(user,ManagementJson.Read<ChangePasswordRequest>(payload));return new{Completed=true};}
    private object Validation(UserAccount user,JsonElement payload){if(payload.TryGetProperty("draftRevision",out var revision)&&revision.ValueKind==JsonValueKind.Number){return this.service.CreateValidationDraft(user,new(Id(payload),revision.GetInt64()));}return this.service.CreateValidation(user,Id(payload));}
    private static string Id(JsonElement payload)=>payload.GetProperty("id").GetString()??throw new ManagementException("InvalidRequest","IDが必要です。");
    private static long Cursor(JsonElement payload){if(payload.TryGetProperty("cursor",out var cursor)){return cursor.GetInt64();}return 0;}
    private Task Send(Stream stream,Guid id,string kind,object value)=>ManagementWire.WriteAsync(stream,new RpcResponse(id,kind,ManagementJson.Element(value)),this.stopping.Token);
    public async ValueTask DisposeAsync(){this.stopping.Cancel();this.service.RequestStop();this.listener.Stop();if(this.accept is not null){await this.accept;}if(this.pipeAccept is not null){await this.pipeAccept;}await Task.WhenAll(this.connections.Values);this.stopping.Dispose();}
}

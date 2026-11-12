using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Query;
using Seamless.Management;
using Seamless.Security;

namespace Seamless.Database.Server;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GatewayNativeAuthorizationConfiguration(IReadOnlyList<GatewayNativeQueryResource> Queries){
    public NativeServerLimits Limits{get;init;}=new();
    public int MaximumConcurrentAuthentications{get;init;}=32;
    public TimeSpan AuthenticationTimeout{get;init;}=TimeSpan.FromSeconds(15);
    public string? TransactionOutcomeKeySha256{get;init;}
    public string? ApprovalFile{get;init;}
    public bool ManageApprovalFile{get;init;}
}
public sealed record GatewayNativeQueryResource(int DatabaseId,string ModuleId,string QueryId,Guid QueryGenerationId,string ManifestSha256,string AssemblySha256,Guid ResourceId,IReadOnlyList<GatewayNativeDatabaseApproval> Databases,IReadOnlyList<GatewayNativeAccess> Access){
    public GatewayNativeApprovalRegistration? Registration{get;init;}
}
public sealed record GatewayNativeApprovalRegistration(Guid Id,SecuritySubjectId Subject,long PolicyVersion,DateTimeOffset RegisteredAt);
public sealed record GatewayNativeDatabaseApproval(int DatabaseId,Guid GenerationId,string SchemaFingerprint,string AssemblySha256);
public sealed record GatewayNativeAccess(Guid ResourceId,SecurityPermission Permissions,bool Delegated=false);

internal sealed partial class GatewayProtection{
    private GatewayNativeApprovalFile? nativeApprovalFile;
    private Func<GatewayPublicationRequest,AuthorizationLease,GatewayPublicationIdentity>? nativePublication;
    private Func<GatewayPublicationListRequest,AuthorizationLease,GatewayPublicationList>? nativePublications;
    private Func<GatewayPublicationApprovalRequest,AuthorizationLease,bool,GatewayPublicationApprovalResult>? nativeApproval;
    internal Task WatchNativeApprovalsAsync(CancellationToken cancellationToken)=>this.nativeApprovalFile?.WatchAsync(cancellationToken)??Task.CompletedTask;

    private static byte[]? ReadNativeTransactionOutcomeKey(GatewayNativeAuthorizationConfiguration? native){
        if(native?.TransactionOutcomeKeySha256 is not{} expected){return null;}
        if(!Hash(expected)){throw new InvalidDataException("Native.TransactionOutcomeKeySha256 must be a SHA-256 digest.");}
        var encoded=Environment.GetEnvironmentVariable("SEAMLESS_GATEWAY_NATIVE_OUTCOME_KEY");
        Span<byte> key=stackalloc byte[32];
        try{
            if(encoded?.Length!=44||!Convert.TryFromBase64String(encoded,key,out var length)||length!=key.Length||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(key),Convert.FromHexString(expected))){
                throw new InvalidDataException("The native transaction outcome key is missing or does not match its configured digest.");
            }
            return key.ToArray();
        }finally{CryptographicOperations.ZeroMemory(key);}
    }
    internal NativeGatewayAuthorization? BindNative(GatewayDatabaseHost host,string configurationDirectory){
        if(settings.Native is not{} native){return null;}
        if(host.Transactions.WalId!=settings.GatewayWalId){throw new InvalidDataException("The native authorization mappings belong to a different Gateway WAL.");}
        if(native.Queries is null||native.Queries.Count>100000||native.Limits is null){throw new InvalidDataException("Invalid native authorization bounds.");}
        if(native.ManageApprovalFile&&string.IsNullOrWhiteSpace(native.ApprovalFile)){throw new InvalidDataException("Native.ManageApprovalFile requires Native.ApprovalFile.");}
        var registrations=host.Databases.Databases.Where(static entry=>entry.Adapter is InProcessDatabaseAdapter).ToDictionary(static entry=>((InProcessDatabaseAdapter)entry.Adapter).Database.Options.GatewayProfile!.DatabaseId);
        var databases=new Dictionary<int,(string Name,Guid Resource)>();var names=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var snapshot=policy.Policy;
        foreach(var mapping in settings.Databases){
            if(mapping is null||!registrations.TryGetValue(mapping.DatabaseId,out var registration)||snapshot.Resource(mapping.ResourceId) is not{Kind:SecurityResourceKind.Database} resource
                ||!string.Equals(resource.Name,registration.Name,StringComparison.OrdinalIgnoreCase)||!databases.TryAdd(mapping.DatabaseId,(registration.Name,mapping.ResourceId))||!names.TryAdd(registration.Name,mapping.DatabaseId)){
                throw new InvalidDataException("A protected native database mapping is missing, invalid or duplicated.");
            }
        }
        var publicationDatabases=databases.ToDictionary(static item=>item.Value.Resource,static item=>item.Key);
        var queries=new Dictionary<(int Database,string Module,string Query,Guid Generation),GatewayNativeQueryResource>();
        var resources=new Dictionary<Guid,(int Database,string Module,string Query)>();
        foreach(var query in native.Queries){
            Add(query,queries,resources,snapshot);
        }
        var active=new ApprovalSnapshot(queries,resources);
        var fileKeys=new HashSet<(int Database,string Module,string Query,Guid Generation)>();
        if(native.ApprovalFile is{} path){
            if(string.IsNullOrWhiteSpace(path)){throw new InvalidDataException("Native.ApprovalFile must name a local JSON file.");}
            this.nativeApprovalFile=new(Path.GetFullPath(path,configurationDirectory),PrepareFile,native.ManageApprovalFile);
            this.nativeApprovalFile.LoadInitial();
        }
        this.nativePublication=ReadPublication;
        this.nativePublications=ListPublications;
        this.nativeApproval=ApprovePublication;
        return NativeGatewayAuthorization.ForPublishedQueries(tokens,(identity,_)=>ValueTask.FromResult<IAuthorizationSession>(policy.CreateSession(identity)),DemandRequest,DemandExecution,native.MaximumConcurrentAuthentications,native.AuthenticationTimeout,nativeTransactionOutcomeKey);

        GatewayPublicationIdentity ReadPublication(GatewayPublicationRequest request,AuthorizationLease operation){
            if(!publicationDatabases.TryGetValue(request.DatabaseResourceId,out var databaseId)){
                throw new ManagementException("NotFound","指定したDBの公開構成がありません。");
            }
            var target=databases[databaseId];
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(operation.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            NativeQueryExecution execution;
            try{execution=host.Transactions.InspectNativePublication(target.Name,request.ModuleId,request.QueryId,deadline.Token);}
            catch(Exception error)when(error is NativeQueryException or SqlQueryException or ObjectDisposedException){
                throw new ManagementException("NotFound","現在公開されている該当クエリを取得できません。");
            }
            if(execution.Databases.Count>1024){throw new ManagementException("Limit","依存DBの数が応答上限を超えています。");}
            var dependencies=new List<GatewayPublicationDatabase>(execution.Databases.Count);
            foreach(var binding in execution.Databases){
                if(!databases.TryGetValue(binding.DatabaseId,out var mapped)||
                    operation.Snapshot.Resource(mapped.Resource) is not{Kind:SecurityResourceKind.Database} resource||
                    !string.Equals(binding.Name,mapped.Name,StringComparison.OrdinalIgnoreCase)||
                    !string.Equals(mapped.Name,resource.Name,StringComparison.OrdinalIgnoreCase)){throw Denied();}
                operation.Demand(mapped.Resource,SecurityPermission.ManagePermissions);
                dependencies.Add(new(mapped.Resource,binding.DatabaseId,binding.Name,binding.GenerationId,binding.SchemaFingerprint,binding.AssemblySha256));
            }
            operation.ThrowIfCancellationRequested();
            if(request.ExpectedGenerationId is{} expected&&execution.QueryGenerationId!=expected){
                throw new ManagementException("CatalogChanged","選択した公開版が変わりました。一覧を更新してください。");
            }
            return new(settings.ManagementInstanceId,execution.WalId,databaseId,execution.ModuleId,execution.Definition.Id,
                execution.QueryGenerationId,execution.ManifestSha256,execution.AssemblySha256,dependencies);
        }

        GatewayPublicationList ListPublications(GatewayPublicationListRequest request,AuthorizationLease operation){
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(operation.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            var snapshot=operation.Snapshot;
            var scope=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                new{snapshot.Subject,Roles=snapshot.Roles.Order()})));
            var cursor=request.After;
            if(cursor is not null&&(cursor.PolicyVersion!=snapshot.PolicyVersion||cursor.Scope!=scope)){
                throw new ManagementException("CatalogChanged","認可の状態が変わりました。最初から取得してください。");
            }
            var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var mapping in databases.Values){
                deadline.Token.ThrowIfCancellationRequested();
                if(snapshot.Resource(mapping.Resource) is{Kind:SecurityResourceKind.Database} resource&&
                    string.Equals(mapping.Name,resource.Name,StringComparison.OrdinalIgnoreCase)&&snapshot.Evaluate(mapping.Resource,SecurityPermission.ManagePermissions).Allowed){
                    allowed.Add(mapping.Name);
                }
            }
            var page=host.Transactions.ListNativePublications(allowed,cursor?.CatalogVersion??Guid.Empty,cursor?.ModuleId,cursor?.QueryId,request.PageSize,deadline.Token);
            if(cursor is not null&&page.Version!=cursor.CatalogVersion){
                throw new ManagementException("CatalogChanged","公開一覧の版が変わりました。最初から取得してください。");
            }
            var items=page.Items.Select(item=>new GatewayPublicationItem(databases[names[item.Database]].Resource,item.Database,
                item.ModuleId,item.QueryId,item.GenerationId,item.IsReadOnly)).ToArray();
            GatewayPublicationCursor? next=null;
            if(page.HasMore){var last=items[^1];next=new(page.Version,snapshot.PolicyVersion,scope,last.ModuleId,last.QueryId);}
            operation.ThrowIfCancellationRequested();
            return new(items,next);
        }

        GatewayPublicationApprovalResult ApprovePublication(GatewayPublicationApprovalRequest request,AuthorizationLease operation,bool register){
            var publication=ReadPublication(request.Publication,operation);
            var access=request.Access.Select(static item=>new GatewayNativeAccess(item.ResourceId,item.Permissions,item.Delegated)).OrderBy(static item=>item.ResourceId).ToArray();
            var bindings=publication.Databases.Select(static item=>new GatewayNativeDatabaseApproval(item.DatabaseId,item.GenerationId,item.SchemaFingerprint,item.AssemblySha256)).OrderBy(static item=>item.DatabaseId).ToArray();
            var approval=new GatewayNativeQueryResource(publication.DatabaseId,publication.ModuleId,publication.QueryId,publication.QueryGenerationId,
                publication.ManifestSha256,publication.AssemblySha256,request.QueryResourceId,bindings,access);
            ValidateMetadata(operation.Snapshot.Resource,approval,bindings.Select(static item=>item.DatabaseId).ToHashSet());
            var prior=Volatile.Read(ref active);
            var target=(approval.DatabaseId,approval.ModuleId,approval.QueryId);
            if(prior.Resources.TryGetValue(approval.ResourceId,out var bound)&&bound!=target){
                throw new ManagementException("Conflict","実行資源は別の公開クエリに対応しています。");
            }
            if(!register){
                return new(publication.QueryGenerationId,GatewayNativeApprovalFile.Export(new(settings.ManagementInstanceId,settings.GatewayWalId,[approval])),false,false);
            }
            if(prior.Queries.TryGetValue(Key(approval),out var existing)){return Already(existing,approval);}
            var registration=new GatewayNativeApprovalRegistration(operation.Audit.OperationId,operation.Snapshot.Subject,operation.Snapshot.PolicyVersion,DateTimeOffset.UtcNow);
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(operation.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            GatewayNativeQueryResource? duplicate;
            try{duplicate=this.nativeApprovalFile!.Append(approval with{Registration=registration},deadline.Token);}
            catch(UnauthorizedAccessException){throw new ManagementException("Storage","サーバーが承認ファイルを更新できません。");}
            catch(IOException){throw new ManagementException("Storage","承認ファイルへの書き込み結果を確認できません。サーバーで状態を確認してください。");}
            if(duplicate is not null){return Already(duplicate,approval);}
            return new(publication.QueryGenerationId,null,true,false,registration.Id);
        }
        static GatewayPublicationApprovalResult Already(GatewayNativeQueryResource existing,GatewayNativeQueryResource requested){
            var normalized=existing with{Registration=null,Databases=existing.Databases.OrderBy(static item=>item.DatabaseId).ToArray(),
                Access=existing.Access.OrderBy(static item=>item.ResourceId).ToArray()};
            if(!SameApproval(normalized,requested)){throw new ManagementException("Conflict","同じ公開世代には別の承認が登録されています。");}
            return new(requested.QueryGenerationId,null,true,true,existing.Registration?.Id);
        }

        Action PrepareFile(GatewayNativeApprovalDocument document,bool initial,CancellationToken cancellationToken){
            if(document.ManagementInstanceId!=settings.ManagementInstanceId||document.GatewayWalId!=settings.GatewayWalId||
                document.Queries is null||document.Queries.Count>100000){throw new InvalidDataException("The native approval document has an invalid instance, WAL or count.");}
            var previous=Volatile.Read(ref active);
            var nextQueries=new Dictionary<(int Database,string Module,string Query,Guid Generation),GatewayNativeQueryResource>(previous.Queries);
            var nextResources=new Dictionary<Guid,(int Database,string Module,string Query)>(previous.Resources);
            var keys=new HashSet<(int Database,string Module,string Query,Guid Generation)>();
            var currentPolicy=policy.Policy;
            foreach(var query in document.Queries){
                cancellationToken.ThrowIfCancellationRequested();
                if(query is null||query.Databases is null||query.Access is null||!keys.Add(Key(query))){throw new InvalidDataException("A native approval file entry is missing or duplicated.");}
                if(previous.Queries.TryGetValue(Key(query),out var old)){
                    if(!SameApproval(old,query)){throw new InvalidDataException("The native approval file cannot change an existing approval.");}
                    continue;
                }
                if(nextQueries.Count>=100000){throw new InvalidDataException("The native approval limit was reached.");}
                Add(query,nextQueries,nextResources,currentPolicy);
                if(!initial){
                    var execution=host.Transactions.InspectNativePublication(databases[query.DatabaseId].Name,query.ModuleId,query.QueryId,cancellationToken);
                    if(!MatchesArtifact(query,execution)){throw new InvalidDataException("A new approval does not match the current native publication.");}
                }
            }
            if(!fileKeys.IsSubsetOf(keys)){throw new InvalidDataException("The native approval file cannot remove an existing approval.");}
            cancellationToken.ThrowIfCancellationRequested();
            var next=new ApprovalSnapshot(nextQueries,nextResources);
            return ()=>{fileKeys=keys;Volatile.Write(ref active,next);};
        }
        void Add(GatewayNativeQueryResource query,Dictionary<(int Database,string Module,string Query,Guid Generation),GatewayNativeQueryResource> targetQueries,
            Dictionary<Guid,(int Database,string Module,string Query)> targetResources,AuthorizationPolicySnapshot currentPolicy){
            if(query is null||query.QueryGenerationId==Guid.Empty||string.IsNullOrWhiteSpace(query.ModuleId)||string.IsNullOrWhiteSpace(query.QueryId)||query.ModuleId.StartsWith('$')
                ||query.ModuleId.Length>1024||query.QueryId.Length>1024||!Hash(query.ManifestSha256)||!Hash(query.AssemblySha256)||!databases.ContainsKey(query.DatabaseId)
                ||query.Databases is null||query.Databases.Count==0||query.Databases.Count>databases.Count||query.Access is null||query.Access.Count>100000
                ||!targetQueries.TryAdd(Key(query),query)){
                throw new InvalidDataException("A native query approval is invalid or duplicated.");
            }
            var scope=(query.DatabaseId,query.ModuleId,query.QueryId);
            if(targetResources.TryGetValue(query.ResourceId,out var previous)&&previous!=scope){throw new InvalidDataException("A native resource cannot identify different public queries.");}targetResources[query.ResourceId]=scope;
            var ids=new HashSet<int>();
            foreach(var binding in query.Databases){if(binding is null||!databases.ContainsKey(binding.DatabaseId)||!ids.Add(binding.DatabaseId)||binding.GenerationId==Guid.Empty||string.IsNullOrWhiteSpace(binding.SchemaFingerprint)||binding.SchemaFingerprint.Length>1024||!Hash(binding.AssemblySha256)){throw new InvalidDataException("A native database approval is invalid or duplicated.");}}
            if(!ids.Contains(query.DatabaseId)){throw new InvalidDataException("A native approval must include its target database.");}
            ValidateMetadata(currentPolicy.Resource,query,ids);
        }

        void DemandRequest(AuthorizationLease lease,NativeQueryRequest request){
            if(!names.TryGetValue(request.Database,out var id)){throw Denied();}
            var database=databases[id];if(lease.Snapshot.Resource(database.Resource) is not{Kind:SecurityResourceKind.Database} resource||!string.Equals(resource.Name,database.Name,StringComparison.OrdinalIgnoreCase)){throw Denied();}
            lease.Demand(database.Resource,SecurityPermission.Discover);
        }
        void DemandExecution(AuthorizationLease lease,NativeQueryRequest request,NativeQueryExecution execution){
            if(execution.WalId!=settings.GatewayWalId||execution.ModuleId!=request.ModuleId||execution.Definition.Id!=request.QueryId||!string.Equals(execution.Definition.Database,request.Database,StringComparison.OrdinalIgnoreCase)
                ||!names.TryGetValue(request.Database,out var databaseId)||!Volatile.Read(ref active).Queries.TryGetValue((databaseId,request.ModuleId,request.QueryId,execution.QueryGenerationId),out var query)
                ||!MatchesArtifact(query,execution)){throw Denied();}
            ValidateMetadata(lease.Snapshot.Resource,query,query.Databases.Select(static item=>item.DatabaseId).ToHashSet());
            lease.Demand(query.ResourceId,SecurityPermission.Execute);
            foreach(var access in query.Access){if(!access.Delegated){lease.Demand(access.ResourceId,access.Permissions);}}
        }
        bool MatchesArtifact(GatewayNativeQueryResource query,NativeQueryExecution execution){
            if(execution.WalId!=settings.GatewayWalId||execution.ModuleId!=query.ModuleId||execution.Definition.Id!=query.QueryId||
                execution.QueryGenerationId!=query.QueryGenerationId||!SameHash(query.ManifestSha256,execution.ManifestSha256)||
                !SameHash(query.AssemblySha256,execution.AssemblySha256)||query.Databases.Count!=execution.Databases.Count){return false;}
            foreach(var binding in execution.Databases){
                var approved=query.Databases.SingleOrDefault(item=>item.DatabaseId==binding.DatabaseId);
                if(approved is null||approved.GenerationId!=binding.GenerationId||approved.SchemaFingerprint!=binding.SchemaFingerprint||!SameHash(approved.AssemblySha256,binding.AssemblySha256)
                    ||!databases.TryGetValue(binding.DatabaseId,out var registration)||!string.Equals(registration.Name,binding.Name,StringComparison.OrdinalIgnoreCase)){return false;}
            }
            return true;
        }
        void ValidateMetadata(Func<Guid,SecurityResource?> resource,GatewayNativeQueryResource query,HashSet<int> bindings){
            var target=resource(query.ResourceId);
            if(target is null||target.Kind is not(SecurityResourceKind.Query or SecurityResourceKind.Procedure or SecurityResourceKind.ScalarFunction or SecurityResourceKind.TableFunction or SecurityResourceKind.View)
                ||target.Name!=query.QueryId||!Within(resource,target,databases[query.DatabaseId].Resource)){throw Denied();}
            var seen=new HashSet<Guid>();
            foreach(var access in query.Access){
                if(access is null||!seen.Add(access.ResourceId)||access.Permissions==SecurityPermission.None||(access.Permissions&~(SecurityPermission.Discover|SecurityPermission.Read|SecurityPermission.Insert|SecurityPermission.Update|SecurityPermission.Delete|SecurityPermission.Execute))!=0
                    ||resource(access.ResourceId) is not{} item||!bindings.Any(id=>Within(resource,item,databases[id].Resource))){throw Denied();}
            }
        }
    }
    private sealed record ApprovalSnapshot(Dictionary<(int Database,string Module,string Query,Guid Generation),GatewayNativeQueryResource> Queries,
        Dictionary<Guid,(int Database,string Module,string Query)> Resources);
    private static (int Database,string Module,string Query,Guid Generation) Key(GatewayNativeQueryResource query)=>
        (query.DatabaseId,query.ModuleId,query.QueryId,query.QueryGenerationId);
    private static bool SameApproval(GatewayNativeQueryResource old,GatewayNativeQueryResource next)=>
        old with{Databases=next.Databases,Access=next.Access}==next&&old.Databases.SequenceEqual(next.Databases)&&old.Access.SequenceEqual(next.Access);
    private static bool Within(Func<Guid,SecurityResource?> resource,SecurityResource target,Guid database){
        for(var depth=0;depth<4;depth++){if(target.Id==database){return true;}if(target.ParentId is not{} parent||resource(parent) is not{} next){return false;}target=next;}return false;
    }
    private static bool Hash(string? hash)=>hash?.Length==64&&hash.All(Uri.IsHexDigit);
    private static bool SameHash(string left,string right)=>string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
    private static AuthorizationException Denied()=>new("UnapprovedExecution","The native query execution or its resource bindings are not approved.");
}

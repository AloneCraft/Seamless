using Seamless.Authentication;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Query;
using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Security;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Seamless.Database.Server;

public record GatewayAuthorizationConfiguration(string ManagementRoot,Guid ManagementInstanceId,Guid GatewayWalId,IReadOnlyList<GatewayHttpDatabaseResource> Databases){
    public int MaximumConcurrentIdentityVerifications{get;init;}=32;
    public TimeSpan? MaximumTransactionDuration{get;init;}
    public GatewayAuthorizationEndpoint? ManagementEndpoint{get;init;}
    public bool ProtectHttp{get;init;}=true;
    public bool ProtectTds{get;init;}
    public GatewayNativeAuthorizationConfiguration? Native{get;init;}
}
public sealed record GatewayHttpAuthorizationConfiguration(string ManagementRoot,Guid ManagementInstanceId,Guid GatewayWalId,IReadOnlyList<GatewayHttpDatabaseResource> Databases)
    :GatewayAuthorizationConfiguration(ManagementRoot,ManagementInstanceId,GatewayWalId,Databases);
public sealed record GatewayAuthorizationEndpoint(string ListenAddress,int Port);
public sealed record GatewayHttpDatabaseResource(int DatabaseId,Guid ResourceId,IReadOnlyList<GatewayHttpTableResource>? Tables=null){
    public IReadOnlyList<GatewayHttpSqlObjectResource>? SqlObjects{get;init;}
}
public sealed record GatewayHttpTableResource(int TableId,Guid ResourceId);
public sealed record GatewayHttpSqlObjectResource(Guid GenerationId,SecurityResourceKind Kind,string Schema,string Name,Guid ResourceId){
    public IReadOnlyList<Guid>? DelegatedReadResources{get;init;}
}

internal sealed partial class GatewayProtection(ManagementStore store,StoredAuthorization policy,OidcTokenVerifier tokens,GatewayAuthorizationConfiguration settings,byte[]? nativeTransactionOutcomeKey):IDisposable{
    internal static GatewayProtection? Open(GatewayStorageConfiguration configuration,string configurationDirectory){
        if(configuration.Authorization is not null&&configuration.HttpAuthorization is not null){throw new InvalidDataException("Specify Authorization or HttpAuthorization, not both.");}
        if((configuration.Authorization??configuration.HttpAuthorization) is not{} settings){return null;}
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MaximumConcurrentIdentityVerifications,nameof(settings.MaximumConcurrentIdentityVerifications));
        if(settings.ProtectHttp&&configuration.Endpoints.Http.Certificate is null){throw new InvalidDataException("Protected HTTP requires HttpCertificatePath.");}
        if(settings.Native is not null&&configuration.Endpoints.NativeCertificate is null){throw new InvalidDataException("Protected native RPC requires NativeCertificatePath.");}
        if(settings.Native is{} native&&(native.MaximumConcurrentAuthentications<=0||native.AuthenticationTimeout<=TimeSpan.Zero||native.AuthenticationTimeout.TotalMilliseconds>uint.MaxValue-1)){
            throw new InvalidDataException("Native authentication requires a positive capacity and finite timeout.");
        }
        if(!settings.ProtectHttp&&!settings.ProtectTds&&settings.Native is null){throw new InvalidDataException("Authorization must protect an explicitly configured endpoint.");}
        if(string.IsNullOrWhiteSpace(settings.ManagementRoot)||settings.ManagementInstanceId==Guid.Empty||settings.GatewayWalId==Guid.Empty||settings.Databases is null){
            throw new InvalidDataException("Protected Gateway endpoints require a management root, its instance ID, the Gateway WAL ID, and database mappings.");
        }
        if(settings.ManagementEndpoint is{} endpoint&&(!IPAddress.TryParse(endpoint.ListenAddress,out _)||endpoint.Port is <0 or >65535)){
            throw new InvalidDataException("Authorization management requires an explicit address and valid port.");
        }
        var outcomeKey=ReadNativeTransactionOutcomeKey(settings.Native);
        ManagementStore? management=null;
        StoredAuthorization? authorization=null;
        try{
            management=new ManagementStore(Path.GetFullPath(settings.ManagementRoot,configurationDirectory));
            if(management.InstanceId!=settings.ManagementInstanceId){throw new InvalidDataException("The authorization policy belongs to a different management instance.");}
            var limits=settings.MaximumTransactionDuration is{} duration?new AuthorizationLimits{MaximumTransactionDuration=duration}:null;
            authorization=StoredAuthorization.Open(management,limits:limits);
            var provider=management.Read<OidcProviderSettings>("authorization","oidcProvider")??throw new InvalidDataException("Protected Gateway endpoints require an OIDC provider registered with the initial authorization policy.");
            return new(management,authorization,OidcTokenVerifier.ForServer(provider,settings.MaximumConcurrentIdentityVerifications),settings,outcomeKey);
        }catch{
            if(outcomeKey is not null){CryptographicOperations.ZeroMemory(outcomeKey);}
            try{authorization?.Dispose();}finally{management?.Dispose();}
            throw;
        }
    }

    internal TdsGatewayAuthorization BindTds(X509Certificate2 certificate,HttpGatewayAuthorization sqlAuthorization)
        =>new(certificate,sqlAuthorization,(identity,_)=>ValueTask.FromResult<IAuthorizationSession>(policy.CreateSession(identity)));

    internal HttpGatewayAuthorization Bind(GatewayDatabaseHost host){
        if(host.Transactions.WalId!=settings.GatewayWalId){throw new InvalidDataException("The configured authorization mappings belong to a different Gateway WAL.");}
        var registrations=host.Databases.Databases.Where(static item=>item.Adapter is InProcessDatabaseAdapter)
            .ToDictionary(static item=>((InProcessDatabaseAdapter)item.Adapter).Database.Options.GatewayProfile!.DatabaseId);
        var databaseResources=new Dictionary<int,Guid>();
        var tableResources=new Dictionary<(int DatabaseId,int TableId),Guid>();
        var sqlObjectResources=new Dictionary<(int DatabaseId,Guid GenerationId,SecurityResourceKind Kind,string Schema,string Name),HttpSqlObjectBinding>();
        var resourceIds=new HashSet<Guid>();
        var snapshot=policy.Policy;
        foreach(var mapping in settings.Databases){
            if(mapping is null||!registrations.TryGetValue(mapping.DatabaseId,out var registration)
                ||snapshot.Resource(mapping.ResourceId) is not{Kind:SecurityResourceKind.Database} resource
                ||!string.Equals(resource.Name,registration.Name,StringComparison.OrdinalIgnoreCase)
                ||!resourceIds.Add(mapping.ResourceId)||!databaseResources.TryAdd(mapping.DatabaseId,mapping.ResourceId)){
                throw new InvalidDataException("A protected HTTP database mapping is missing, invalid or duplicated.");
            }
            foreach(var table in mapping.Tables??[]){
                if(table is null||table.TableId<=0||snapshot.Resource(table.ResourceId) is not{Kind:SecurityResourceKind.Table}
                    ||!resourceIds.Add(table.ResourceId)||!tableResources.TryAdd((mapping.DatabaseId,table.TableId),table.ResourceId)){
                    throw new InvalidDataException("A protected HTTP table mapping is invalid or duplicated.");
                }
            }
            foreach(var item in mapping.SqlObjects??[]){
                if(item is null||item.GenerationId==Guid.Empty||item.Kind is not(SecurityResourceKind.View or SecurityResourceKind.ScalarFunction or SecurityResourceKind.Procedure)
                    ||string.IsNullOrWhiteSpace(item.Schema)||string.IsNullOrWhiteSpace(item.Name)
                    ||snapshot.Resource(item.ResourceId) is not{ParentId:{} parent} target||target.Kind!=item.Kind||target.Name!=item.Name
                    ||snapshot.Resource(parent) is not{Kind:SecurityResourceKind.Schema} schema||schema.ParentId!=mapping.ResourceId||schema.Name!=item.Schema
                    ||!resourceIds.Add(item.ResourceId)){
                    throw new InvalidDataException("A protected SQL object mapping is invalid or duplicated.");
                }
                var delegated=item.DelegatedReadResources??[];
                if(delegated.Count!=delegated.Distinct().Count()||delegated.Any(id=>!IsDelegatedTable(mapping,id))){
                    throw new InvalidDataException("A delegated SQL read must identify a mapped table in the same database.");
                }
                var binding=new HttpSqlObjectBinding(item.ResourceId,Array.AsReadOnly(delegated.ToArray()));
                if(!sqlObjectResources.TryAdd((mapping.DatabaseId,item.GenerationId,item.Kind,item.Schema,item.Name),binding)){
                    throw new InvalidDataException("A protected SQL object mapping is duplicated.");
                }
            }
        }
        return new(tokens,BeginOperation,DatabaseResource,TableResource,SqlObjectResource,BeginTransaction);

        Guid? DatabaseResource(string name){
            try{
                var registration=host.Databases.Resolve(name);
                if(registration.Adapter is InProcessDatabaseAdapter adapter&&adapter.Database.Options.GatewayProfile is{} profile
                    &&profile.WalId==settings.GatewayWalId&&databaseResources.TryGetValue(profile.DatabaseId,out var id)){return id;}
            }catch(SqlQueryException error)when(error.Number==4060){}
            return null;
        }
        Guid? TableResource(IDatabaseAdapter adapter,int tableId){
            if(adapter is InProcessDatabaseAdapter current&&current.Database.Options.GatewayProfile is{} profile
                &&profile.WalId==settings.GatewayWalId&&tableResources.TryGetValue((profile.DatabaseId,tableId),out var id)){return id;}
            return null;
        }
        HttpSqlObjectBinding? SqlObjectResource(IDatabaseAdapter adapter,SecurityResourceKind kind,string schema,string name){
            if(adapter is InProcessDatabaseAdapter current&&current.Database.Options.GatewayProfile is{} profile
                &&profile.WalId==settings.GatewayWalId&&sqlObjectResources.TryGetValue((profile.DatabaseId,profile.GenerationId,kind,schema,name),out var id)){return id;}
            return null;
        }
        bool IsDelegatedTable(GatewayHttpDatabaseResource mapping,Guid id){
            if(mapping.Tables?.Any(table=>table.ResourceId==id)!=true||snapshot.Resource(id) is not{Kind:SecurityResourceKind.Table,ParentId:{} parent}){return false;}
            return parent==mapping.ResourceId||snapshot.Resource(parent) is{Kind:SecurityResourceKind.Schema} schema&&schema.ParentId==mapping.ResourceId;
        }
    }

    private ValueTask<AuthorizationLease?> BeginOperation(AuthenticatedIdentity identity,CancellationToken cancellation){
        try{using var session=policy.CreateSession(identity);return new(session.BeginOperation(cancellation));}
        catch(Exception error)when(error is ManagementException or InvalidDataException or IOException){return new((AuthorizationLease?)null);}
    }
    internal ManagementServer? CreateManagementServer(X509Certificate2? certificate){
        if(settings.ManagementEndpoint is not{} endpoint){return null;}
        if(!IPAddress.TryParse(endpoint.ListenAddress,out var address)||endpoint.Port is <0 or >65535||certificate is null){throw new InvalidDataException("Authorization management requires an explicit address, valid port and TLS certificate.");}
        return ManagementServer.ForAuthorization(policy,tokens,certificate,new(address,endpoint.Port),this.nativePublication,this.nativePublications,this.nativeApproval,settings.Native?.ManageApprovalFile==true);
    }
    private ValueTask<AuthorizationLease?> BeginTransaction(AuthenticatedIdentity identity,CancellationToken cancellation){
        cancellation.ThrowIfCancellationRequested();
        try{using var session=policy.CreateSession(identity);return new(session.BeginTransaction());}
        catch(Exception error)when(error is ManagementException or InvalidDataException or IOException){return new((AuthorizationLease?)null);}
    }

    public void Dispose(){
        if(nativeTransactionOutcomeKey is not null){CryptographicOperations.ZeroMemory(nativeTransactionOutcomeKey);}
        try{tokens.Dispose();}
        finally{try{policy.Dispose();}finally{store.Dispose();}}
    }
}

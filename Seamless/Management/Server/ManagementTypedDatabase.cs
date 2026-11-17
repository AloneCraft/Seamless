using MemoryPack;
using Seamless.Database;

namespace Seamless.Management.Server;

[MemoryPackable]internal readonly partial record struct ManagementTextValue(string? Inline,int Length,int Parts,string Hash);
[MemoryPackable]internal readonly partial record struct ManagementUserRow(Guid Id,string Subject,string Name,ManagementTextValue PasswordHash,int? GrantCount,bool Disabled);
[MemoryPackable]internal readonly partial record struct ManagementGrantRow(Guid Id,string Subject,int Ordinal,ManagementTextValue Database,ManagementPermission Permissions,bool IsNull);
[MemoryPackable]internal readonly partial record struct ManagementTextPartRow(Guid Id,string OwnerKind,string Subject,int Ordinal,int Part,char[] Text);
[MemoryPackable]internal readonly partial record struct ManagementSettingsRow(Guid Id,string Subject,long Version,string Address,int Port,int ManagerPort,string? ExternalManagerOrigin,string ExternalManagerAddress,long? MaximumBufferedRequestBytes,long? MaximumPendingWriteBytes,TimeSpan? ResponseWriteTimeout,bool ReadAuditEnabled,int ReadAuditWindowSeconds,double ReadAuditSaveSeconds){
    internal static ManagementSettingsRow From(string subject,StoredServerSettings value){
        var settings=value.Settings;
        return new(ManagementStore.Key("serverSettings",subject),subject,value.Version,settings.Address,settings.Port,settings.ManagerPort,settings.ExternalManagerOrigin,settings.ExternalManagerAddress,settings.MaximumBufferedRequestBytes,settings.MaximumPendingWriteBytes,settings.ResponseWriteTimeout,settings.ReadAuditEnabled,settings.ReadAuditWindowSeconds,settings.ReadAuditSaveSeconds);
    }
    internal StoredServerSettings Settings()=>new(this.Version,new(){Address=this.Address,Port=this.Port,ManagerPort=this.ManagerPort,ExternalManagerOrigin=this.ExternalManagerOrigin,ExternalManagerAddress=this.ExternalManagerAddress,MaximumBufferedRequestBytes=this.MaximumBufferedRequestBytes,MaximumPendingWriteBytes=this.MaximumPendingWriteBytes,ResponseWriteTimeout=this.ResponseWriteTimeout,ReadAuditEnabled=this.ReadAuditEnabled,ReadAuditWindowSeconds=this.ReadAuditWindowSeconds,ReadAuditSaveSeconds=this.ReadAuditSaveSeconds});
}

internal sealed class ManagementTypedDatabase(DatabaseContainerOptions options):ManagementAuditDatabase(options){
    internal Table<ManagementKey,ManagementUserRow> Users=>this.Table<ManagementKey,ManagementUserRow>(static row=>new(row.Id));
    internal Table<ManagementKey,ManagementGrantRow> UserGrants=>this.Table<ManagementKey,ManagementGrantRow>(static row=>new(row.Id));
    internal Table<ManagementKey,ManagementTextPartRow> ManagedTextParts=>this.Table<ManagementKey,ManagementTextPartRow>(static row=>new(row.Id));
    internal Table<ManagementKey,ManagementSettingsRow> ServerSettings=>this.Table<ManagementKey,ManagementSettingsRow>(static row=>new(row.Id));
    protected override void EnsureTransactionSchema(){base.EnsureTransactionSchema();_=this.Users;_=this.UserGrants;_=this.ManagedTextParts;_=this.ServerSettings;}
    protected override string TransactionCatalogIdentity=>base.TransactionCatalogIdentity+"|ManagedTypesV1|"+this.TransactionCodecIdentity<ManagementKey,ManagementUserRow>("Users")+"|"+this.TransactionCodecIdentity<ManagementKey,ManagementGrantRow>("UserGrants")+"|"+this.TransactionCodecIdentity<ManagementKey,ManagementTextPartRow>("ManagedTextParts")+"|"+this.TransactionCodecIdentity<ManagementKey,ManagementSettingsRow>("ServerSettings");
}

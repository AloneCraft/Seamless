using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

/// <summary>共通domainへ参加する前の、初期snapshotだけを構築できる私有能力。</summary>
internal sealed class GatewayVersionedSeed{
    private readonly object gate=new();
    private readonly GatewayDatabaseProfile profile;
    private readonly GatewayTransactionOwner owner;
    private DatabaseContainer? database;
    internal GatewayVersionedSeed(GatewayDatabaseProfile profile,GatewayTransactionOwner owner){
        ArgumentNullException.ThrowIfNull(profile);if(profile.WalId==Guid.Empty||profile.GenerationId==Guid.Empty||profile.DatabaseId<=0||owner.TransactionId==Guid.Empty){throw new ArgumentException("A private seed requires its WAL, database, generation and owner identities.");}this.profile=profile;this.owner=owner;
    }
    internal bool Sealed{get;private set;}
    internal void Validate(DatabaseContainerOptions options){
        if(options.GatewayProfile!=this.profile||options.TransactionHistory is null||options.GatewayVersionedBinding is not null||options.RecordSerialization!=DatabaseRecordSerialization.CanonicalV1||options.StorageMode!=DatabaseStorageMode.Storage||options.ParentBudget is null){throw new ArgumentException("The private seed requires a finite canonical versioned Gateway profile.",nameof(options));}
    }
    internal void Bind(DatabaseContainer value){lock(this.gate){if(this.database is not null&&!ReferenceEquals(this.database,value)){throw new InvalidOperationException("A seed capability belongs to one private database instance.");}this.database=value;}}
    internal void CheckOwner(DatabaseContainer value,GatewayTransactionOwner owner){lock(this.gate){if(!ReferenceEquals(this.database,value)||owner!=this.owner||this.Sealed){throw new InvalidOperationException("Only the constructing owner may seal an unfinished private seed.");}}}
    internal void Complete(){lock(this.gate){this.Sealed=true;}}
}

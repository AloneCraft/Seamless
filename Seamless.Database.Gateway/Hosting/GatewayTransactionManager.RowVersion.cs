using System.Buffers.Binary;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    private static ulong InitialRowVersion(GatewaySchemaCatalog schema)=>schema.Definition.Schemas.SelectMany(static owner=>owner.Tables).SelectMany(static table=>table.Columns).Select(static column=>column.InitialRowVersionValue??0).DefaultIfEmpty().Max();
    private void PreserveImportedRowVersion(SessionContext session,string database,GatewaySchemaCatalog schema){
        var initial=InitialRowVersion(schema);if(initial==0){return;}var id=((InProcessDatabaseAdapter)this.generationRegistry!.Resolve(database).Adapter).Database.Options.GatewayProfile!.DatabaseId;var key=new ValueKey(id,"rowversion",0,0);
        lock(this.valueGate){if(!this.values.TryGetValue(key,out var current)||current.Last<initial){this.PersistValues([new(key,initial,1)],session.CancellationToken);}}
    }
    internal byte[] NextRowVersion(SessionContext session,string database){
        var adapter=(InProcessDatabaseAdapter)this.generationRegistry!.Resolve(database).Adapter;var id=adapter.Database.Options.GatewayProfile!.DatabaseId;var initial=InitialRowVersion(this.TransactionSchema(session,database));var key=new ValueKey(id,"rowversion",0,0);
        lock(this.valueGate){var previous=this.values.GetValueOrDefault(key)?.Last??initial;if(previous>=ulong.MaxValue){throw new SqlQueryException("The database rowversion counter is exhausted.",8115);}var next=checked((ulong)(previous+1));this.PersistValues([new(key,next,1)],session.CancellationToken);var bytes=new byte[8];BinaryPrimitives.WriteUInt64BigEndian(bytes,next);return bytes;}
    }
    internal byte[] CurrentRowVersion(SessionContext session,string database){
        if(!this.TypedDdlEnabled){throw new SqlQueryException("@@DBTS requires the typed Gateway profile.",40515);}
        var adapter=(InProcessDatabaseAdapter)this.generationRegistry!.Resolve(database).Adapter;using var read=this.AcquireRead(session,adapter);var id=adapter.Database.Options.GatewayProfile!.DatabaseId;GatewaySchemaCatalog schema;if(this.versioned is not null){schema=this.TransactionSchema(session,database);}else{lock(this.gate){schema=session.Transaction?.Participants.GetValueOrDefault(id)?.Candidate?.Schema??this.published[id].Prepared.Schema;}}var initial=InitialRowVersion(schema);
        lock(this.valueGate){var value=this.values.GetValueOrDefault(new(id,"rowversion",0,0))?.Last??initial;var bytes=new byte[8];BinaryPrimitives.WriteUInt64BigEndian(bytes,checked((ulong)value));return bytes;}
    }
    internal byte[] NextRowVersion(SessionContext session,InProcessDatabaseAdapter adapter)=>this.NextRowVersion(session,this.databases[adapter.Database.Options.GatewayProfile!.DatabaseId].Name);
}

using System.Text.Json;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
using Seamless.Execution;
using Seamless.Transactions;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    private readonly Dictionary<ValueKey,ValueReservation> values=[];
    private readonly List<ResourceReservation> valueMemory=[];
    private readonly object valueGate=new();
    private const string ValueFormat="seamless-gateway-values-v1";
    private sealed record ValueKey(int DatabaseId,string Kind,long ObjectId,long ColumnId);
    private sealed record ValueReservation(ValueKey Key,decimal Last,decimal Increment);
    private sealed record ValueCommit(string Format,ValueReservation[] Values);
    private void ApplyValues(IEnumerable<ValueReservation> changes,bool reserveMemory=true){
        lock(this.valueGate){this.ApplyOwnedValues(changes,reserveMemory);}
    }
    private void ApplyOwnedValues(IEnumerable<ValueReservation> changes,bool reserveMemory){
        foreach(var change in changes){if(change.Key.DatabaseId<=0||change.Key.Kind is not ("identity" or "table-id" or "column-id" or "rowversion")||change.Increment==0){throw new InvalidDataException("Invalid durable value reservation.");}if(reserveMemory&&!this.values.ContainsKey(change.Key)){this.valueMemory.Add(this.budget.ReserveMemory(256));}this.values[change.Key]=change;}
    }
    private void PersistValues(ValueReservation[] changes,CancellationToken cancellationToken){
        if(!Monitor.IsEntered(this.valueGate)){throw new InvalidOperationException("Persisted value allocation requires the exclusive value owner.");}
        var payload=JsonSerializer.SerializeToUtf8Bytes(new ValueCommit(ValueFormat,changes));using var memory=this.budget.ReserveMemory(checked(payload.Length*3L));var reservations=new List<ResourceReservation>();try{foreach(var key in changes.Select(static change=>change.Key).Distinct().Where(key=>!this.values.ContainsKey(key))){reservations.Add(this.budget.ReserveMemory(256));}this.values.EnsureCapacity(this.values.Count+reservations.Count);this.valueMemory.EnsureCapacity(this.valueMemory.Count+reservations.Count);
            // ID/rowversionの予約は業務rollback後も残す独立取引。耐久化後の反映順だけを共通化する。
            // 呼出し側がvalueGateを保持して結果を同期的に待つ間だけ、反映をcontinuationへ委譲する。
            // WAL待ちの後は別threadになり得るため、callbackからthread所有のMonitorへ再入しない。
            void Applied(){this.ApplyOwnedValues(changes,false);this.valueMemory.AddRange(reservations);reservations.Clear();}
            if(this.versioned is not null){var parent=DatabaseRegistry.CurrentSqlSession?.SnapshotTransaction?.Scope??throw new InvalidOperationException("A versioned value reservation requires its owning writable transaction.");this.commitCoordinator.CommitIndependentMetadataAsync(parent,Guid.NewGuid(),payload,cancellationToken,_=>Applied()).AsTask().GetAwaiter().GetResult();}
            else{using var commit=new CommonCommitChanges(static()=>[],_=>Applied());this.commitCoordinator.CommitExternalAsync(Guid.NewGuid(),commit,payload,cancellationToken).AsTask().GetAwaiter().GetResult();}}
        catch(TransactionCommitException error)when(error.Outcome is TransactionOutcomeKind.Unknown or TransactionOutcomeKind.Committed){foreach(var id in changes.Select(static change=>change.Key.DatabaseId).Distinct()){if(this.databases.TryGetValue(id,out var database)){database.Adapter.Database.FaultGateway();}}throw;}
        finally{foreach(var reservation in reservations){reservation.Dispose();}}
    }
    internal GatewaySchemaCatalog DdlBaseSchema(SessionContext session,string database){
        var schema=this.TransactionSchema(session,database);var id=((InProcessDatabaseAdapter)this.generationRegistry!.Resolve(database).Adapter).Database.Options.GatewayProfile!.DatabaseId;
        lock(this.valueGate){var table=this.values.GetValueOrDefault(new(id,"table-id",0,0));var column=this.values.GetValueOrDefault(new(id,"column-id",0,0));return schema with{NextTableId=Math.Max(schema.NextTableId,table is null?schema.NextTableId:checked((int)table.Last+1)),NextColumnId=Math.Max(schema.NextColumnId,column is null?schema.NextColumnId:checked((long)column.Last+1))};}
    }
    internal void ReserveDdlIdentifiers(SessionContext session,string database,GatewaySchemaCatalog before,GatewaySchemaCatalog after){
        this.PreserveImportedRowVersion(session,database,before);
        var id=((InProcessDatabaseAdapter)this.generationRegistry!.Resolve(database).Adapter).Database.Options.GatewayProfile!.DatabaseId;var changes=new List<ValueReservation>();if(after.NextTableId>before.NextTableId){changes.Add(new(new(id,"table-id",0,0),after.NextTableId-1,1));}if(after.NextColumnId>before.NextColumnId){changes.Add(new(new(id,"column-id",0,0),after.NextColumnId-1,1));}if(changes.Count>0){lock(this.valueGate){this.PersistValues(changes.ToArray(),session.CancellationToken);}}
    }
    internal object NextIdentity(SessionContext session,string database,GatewayTableIdentity table,GatewayColumnIdentity column,ColumnCatalog definition,object? explicitValue=null,bool explicitInsert=false){
        var id=((InProcessDatabaseAdapter)this.generationRegistry!.Resolve(database).Adapter).Database.Options.GatewayProfile!.DatabaseId;var key=new ValueKey(id,"identity",table.Id,column.Id);lock(this.valueGate){
            var current=this.values.GetValueOrDefault(key);decimal next;try{next=explicitInsert?Convert.ToDecimal(explicitValue,global::System.Globalization.CultureInfo.InvariantCulture):current is null?definition.InitialIdentityValue is{} initial?checked(initial+definition.IdentityIncrement):definition.IdentitySeed:checked(current.Last+definition.IdentityIncrement);}catch(OverflowException){throw new SqlQueryException("Arithmetic overflow generating an IDENTITY value.",8115);}var converted=SqlScalarConversion.Assign(next,ComparisonHost.ToSqlType(definition.Type))!;
            if(!explicitInsert||current is null||definition.IdentityIncrement>0&&next>current.Last||definition.IdentityIncrement<0&&next<current.Last){this.PersistValues([new(key,next,definition.IdentityIncrement)],session.CancellationToken);}session.LastIdentity=next;session.CurrentIdentityScope.Value=next;return converted;
        }
    }
    internal decimal? IdentityCurrent(SessionContext session,string database,string schemaName,string tableName,bool useSeed=true){
        var registration=this.generationRegistry!.Resolve(database);if(registration.Adapter is not InProcessDatabaseAdapter adapter){return null;}
        using var read=this.AcquireRead(session,adapter);var id=adapter.Database.Options.GatewayProfile!.DatabaseId;GatewaySchemaCatalog schema;
        if(this.versioned is not null){schema=this.TransactionSchema(session,database);}else{lock(this.gate){schema=session.Transaction?.Participants.GetValueOrDefault(id)?.Candidate?.Schema??this.published[id].Prepared.Schema;}}
        var identity=schema.Tables.SingleOrDefault(table=>table.Schema.Equals(schemaName,StringComparison.OrdinalIgnoreCase)&&table.Name.Equals(tableName,StringComparison.OrdinalIgnoreCase));if(identity is null){return null;}
        var definition=schema.Definition.Schemas.Single(owner=>owner.Name==identity.Schema).Tables.Single(table=>table.Name==identity.Name).Columns.SingleOrDefault(static column=>column.IsIdentity);if(definition is null){return null;}
        var column=identity.Columns.Single(column=>column.Name==definition.Name);lock(this.valueGate){var value=this.values.GetValueOrDefault(new(id,"identity",identity.Id,column.Id))?.Last??definition.InitialIdentityValue;if(value is not null||!useSeed){return value;}return definition.IdentitySeed;}
    }
    internal void RecordInsertedIdentity(SessionContext session,InProcessDatabaseAdapter adapter,TableMetadata table,ColumnCatalog definition,object value){
        var database=this.databases[adapter.Database.Options.GatewayProfile!.DatabaseId].Name;var schema=this.TransactionSchema(session,database);var identity=schema.Tables.Single(identity=>identity.Id==table.StableObjectId);var column=identity.Columns.Single(column=>column.Name==definition.Name);
        _=this.NextIdentity(session,database,identity,column,definition,value,true);
    }
    private bool RestoreValueCommit(ReadOnlyMemory<byte> metadata){
        using var json=JsonDocument.Parse(metadata);if(!json.RootElement.TryGetProperty("Format",out var format)||format.GetString()!=ValueFormat){return false;}var commit=JsonSerializer.Deserialize<ValueCommit>(metadata.Span)??throw new InvalidDataException("Missing value reservation.");if(commit.Values.Length==0||commit.Values.Length>4096){throw new InvalidDataException("Invalid value reservation count.");}this.ApplyValues(commit.Values);return true;
    }
}

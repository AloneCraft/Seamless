using MemoryPack;
using Seamless.Database;

namespace Seamless.Management.Server;

[MemoryPackable]internal readonly partial record struct AuditSequenceKey(long Sequence);
[MemoryPackable]internal readonly partial record struct AuditDayKey(int Day);
[MemoryPackable]internal partial record struct AuditEventRow(long Sequence,AuditEvent Value,string SearchText);
[MemoryPackable]internal partial record struct AuditEventIdRow(Guid Id,long Sequence,string Hash);
[MemoryPackable]internal partial record struct AuditDayRow(int Day,long FirstSequence,long LastSequence);
[MemoryPackable]internal partial record struct AuditSummaryRow(long Sequence,AuditReadKey Key,long Count,DateTimeOffset SavedAt);
[MemoryPackable]internal partial record struct AuditSummaryIdRow(Guid Id,long Sequence);
[MemoryPackable]internal partial record struct AuditBatchRow(Guid Id,string Hash);
[MemoryPackable]internal partial record struct AuditStateRow(long Id,long EventSequence,long SummarySequence);

internal class ManagementAuditDatabase(DatabaseContainerOptions options):ManagementDatabase(options){
    internal static readonly string[] TableNames=["AuditEvents","AuditEventIds","AuditDays","ReadSummaries","ReadSummaryIds","AuditBatches","AuditState"];
    internal Table<AuditSequenceKey,AuditEventRow> AuditEvents=>this.Table<AuditSequenceKey,AuditEventRow>(static row=>new(row.Sequence));
    internal Table<ManagementKey,AuditEventIdRow> AuditEventIds=>this.Table<ManagementKey,AuditEventIdRow>(static row=>new(row.Id));
    internal Table<AuditDayKey,AuditDayRow> AuditDays=>this.Table<AuditDayKey,AuditDayRow>(static row=>new(row.Day));
    internal Table<AuditSequenceKey,AuditSummaryRow> ReadSummaries=>this.Table<AuditSequenceKey,AuditSummaryRow>(static row=>new(row.Sequence));
    internal Table<ManagementKey,AuditSummaryIdRow> ReadSummaryIds=>this.Table<ManagementKey,AuditSummaryIdRow>(static row=>new(row.Id));
    internal Table<ManagementKey,AuditBatchRow> AuditBatches=>this.Table<ManagementKey,AuditBatchRow>(static row=>new(row.Id));
    internal Table<AuditSequenceKey,AuditStateRow> AuditState=>this.Table<AuditSequenceKey,AuditStateRow>(static row=>new(row.Id));
    protected override void EnsureTransactionSchema(){base.EnsureTransactionSchema();_=this.AuditEvents;_=this.AuditEventIds;_=this.AuditDays;_=this.ReadSummaries;_=this.ReadSummaryIds;_=this.AuditBatches;_=this.AuditState;}
    protected override string TransactionCatalogIdentity=>base.TransactionCatalogIdentity+"|AuditV1|"+this.TransactionCodecIdentity<AuditSequenceKey,AuditEventRow>("AuditEvents")+"|"+this.TransactionCodecIdentity<ManagementKey,AuditEventIdRow>("AuditEventIds")+"|"+this.TransactionCodecIdentity<AuditDayKey,AuditDayRow>("AuditDays")+"|"+this.TransactionCodecIdentity<AuditSequenceKey,AuditSummaryRow>("ReadSummaries")+"|"+this.TransactionCodecIdentity<ManagementKey,AuditSummaryIdRow>("ReadSummaryIds")+"|"+this.TransactionCodecIdentity<ManagementKey,AuditBatchRow>("AuditBatches")+"|"+this.TransactionCodecIdentity<AuditSequenceKey,AuditStateRow>("AuditState");
}

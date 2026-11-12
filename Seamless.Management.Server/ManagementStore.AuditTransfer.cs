using Seamless.Database;

namespace Seamless.Management.Server;

public sealed partial class ManagementStore{
    internal void ImportAudit(ManagementStore source,CancellationToken cancellationToken=default){
        if((source.Format!=4||this.Format!=5)&&(source.Format!=5||this.Format!=6)&&(source.Format!=6||this.Format!=7)){throw new InvalidOperationException("集計索引の更新元・更新先の形式が不正です。");}
        lock(source.gate){using var read=source.database.BeginReadSession();var from=source.AuditDatabase;var to=this.AuditDatabase;
            void Copy<TKey,TRow>(Table<TKey,TRow> input,Table<TKey,TRow> output,Func<TRow,TKey> key,Action<TRow>? custom=null) where TKey:struct where TRow:struct{
                var rows=new List<TRow>(64);void Flush(){this.Write(()=>{foreach(var row in rows){if(custom is not null){custom(row);}else{output[key(row)]=row;}}});rows.Clear();}
                foreach(var row in input){cancellationToken.ThrowIfCancellationRequested();rows.Add(row);if(rows.Count==64){Flush();}}if(rows.Count>0){Flush();}
            }
            Copy(from.AuditEvents,to.AuditEvents,static row=>new AuditSequenceKey(row.Sequence));Copy(from.AuditEventIds,to.AuditEventIds,static row=>new ManagementKey(row.Id));Copy(from.AuditDays,to.AuditDays,static row=>new AuditDayKey(row.Day));Copy(from.AuditBatches,to.AuditBatches,static row=>new ManagementKey(row.Id));Copy(from.AuditState,to.AuditState,static row=>new AuditSequenceKey(row.Id));
            Copy(from.ReadSummaries,to.ReadSummaries,static row=>new AuditSequenceKey(row.Sequence),row=>{
                Validate(row.Key);if(row.Sequence<=0||row.Count<=0){throw new InvalidDataException("旧集計行が不正です。");}var id=AuditReadIdentity.Id(row.Key);
                if(to.ReadSummaryIds.TryGetValue(new(id),out var location)){
                    if(!to.ReadSummaries.TryGetValue(new(location.Sequence),out var existing)||existing.Key!=row.Key){throw new InvalidDataException("更新後の集計キーが衝突しています。");}
                    var selected=existing;if(row.Sequence<existing.Sequence){selected=row;to.ReadSummaries.Remove(new AuditSequenceKey(existing.Sequence));}var saved=existing.SavedAt;if(row.SavedAt>saved){saved=row.SavedAt;}var merged=selected with{Count=checked(existing.Count+row.Count),SavedAt=saved};to.ReadSummaries[new(merged.Sequence)]=merged;to.ReadSummaryIds[new(id)]=new(id,merged.Sequence);
                }else{to.ReadSummaries[new(row.Sequence)]=row;to.ReadSummaryIds[new(id)]=new(id,row.Sequence);}
            });
            from.AuditState.TryGetValue(new(0),out var state);using var check=this.database.BeginReadSession();foreach(var row in to.ReadSummaries){if(row.Sequence>state.SummarySequence){throw new InvalidDataException("旧集計の連番上限が一致しません。");}}
        }
    }
}

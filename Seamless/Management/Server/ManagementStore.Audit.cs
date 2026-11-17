using System.Security.Cryptography;
using System.Text;
using MemoryPack;

namespace Seamless.Management.Server;

public sealed partial class ManagementStore{
    private ManagementAuditDatabase AuditDatabase=>this.database as ManagementAuditDatabase??throw new ManagementException("MigrationRequired","監査用テーブルはCLIのupgrade-auditで明示導入してください。");
    private static void TextBound(string? value,int maximum,bool required=false){if(value is null){if(required){throw new ManagementException("Validation","監査の識別情報が必要です。");}return;}if(value.Length>maximum||value.Contains('\0')||required&&string.IsNullOrWhiteSpace(value)){throw new ManagementException("Validation","監査の文字列が不正です。");}}
    private static void Validate(AuditEvent value){
        if(value.EventId==Guid.Empty||value.AuthorityId==Guid.Empty||!Enum.IsDefined(value.Action)||!Enum.IsDefined(value.Outcome)||value.PolicyVersion<0){throw new ManagementException("Validation","監査イベントの識別情報が不正です。");}
        TextBound(value.SubjectId,1024,true);TextBound(value.Database,128,true);TextBound(value.ObjectId,1024);TextBound(value.CodeVersion,256);TextBound(value.ResultCode,128,true);TextBound(value.Message,4096);TextBound(value.Namespace,1024);TextBound(value.TypeName,1024);TextBound(value.MethodName,512);TextBound(value.MethodSignature,2048);TextBound(value.QueryTemplate,8192);
    }
    internal static void ValidateAuditEvent(AuditEvent value)=>Validate(value);
    private static void Validate(AuditReadKey key){if(key.AuthorityId==Guid.Empty||key.WindowEnd<=key.WindowStart||key.PolicyVersion<0){throw new ManagementException("Validation","集計キーが不正です。");}TextBound(key.SubjectId,1024,true);TextBound(key.Database,128,true);TextBound(key.QueryId,1024,true);TextBound(key.CodeVersion,256);}
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(MemoryPackSerializer.Serialize(value)));
    private static bool SameAuditValues(AuditEvent left,AuditEvent right)=>left==right&&left.StartedAt.EqualsExact(right.StartedAt)&&left.CompletedAt.EqualsExact(right.CompletedAt);
    private Guid SummaryId(AuditReadKey key){if(this.Format>=5){return AuditReadIdentity.Id(key);}return new(SHA256.HashData(MemoryPackSerializer.Serialize(key)).AsSpan(0,16));}
    private static string SearchText(AuditEvent value)=>string.Join('\n',value.SubjectId,value.Database,value.ObjectId,value.ResultCode,value.Message,value.Namespace,value.TypeName,value.MethodName,value.MethodSignature,string.Join('.',new[]{value.Namespace,value.TypeName,value.MethodName}.Where(static item=>!string.IsNullOrEmpty(item)))).ToUpperInvariant();
    public long AppendAudit(AuditEvent value){_=this.AuditDatabase;Validate(value);long result=0;this.Write(()=>result=this.AppendAuditInside(value));return result;}
    internal long AppendAuditInside(AuditEvent value){
        var data=this.AuditDatabase;Validate(value);var hash=Hash(value);
        if(data.AuditEventIds.TryGetValue(new(value.EventId),out var existing)){
            // unmanaged値の未使用バイトは値の同一性ではない。既存の保存形式・ハッシュも維持する。
            if(existing.Hash!=hash&&(!data.AuditEvents.TryGetValue(new(existing.Sequence),out var saved)||!SameAuditValues(saved.Value,value))){throw new ManagementException("Conflict","監査イベントIDが異なる内容で再利用されました。");}return existing.Sequence;
        }
        data.AuditState.TryGetValue(new(0),out var state);var sequence=checked(state.EventSequence+1);data.AuditEvents[new(sequence)]=new(sequence,value,SearchText(value));data.AuditEventIds[new(value.EventId)]=new(value.EventId,sequence,hash);
        var day=DateOnly.FromDateTime(value.CompletedAt.UtcDateTime).DayNumber;if(data.AuditDays.TryGetValue(new(day),out var range)){range=range with{FirstSequence=Math.Min(range.FirstSequence,sequence),LastSequence=Math.Max(range.LastSequence,sequence)};}else{range=new(day,sequence,sequence);}data.AuditDays[new(day)]=range;data.AuditState[new(0)]=state with{Id=0,EventSequence=sequence};return sequence;
    }
    private static string[] Validate(AuditQuery query){
        ArgumentNullException.ThrowIfNull(query);if(query.Limit is <1 or >100||query.Before<0||query.Ceiling<0||query.FromUtc>query.ToUtc||query.Action.HasValue&&!Enum.IsDefined(query.Action.Value)||query.Outcome.HasValue&&!Enum.IsDefined(query.Outcome.Value)){throw new ManagementException("InvalidRequest","監査の検索範囲が不正です。");}
        TextBound(query.ObjectId,1024);
        TextBound(query.Database,128);TextBound(query.SubjectId,1024);TextBound(query.Text,1024);var terms=(query.Text??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);if(terms.Length>16||terms.Any(static term=>term.Length>128)){throw new ManagementException("Limit","検索語は16個まで、各128文字までです。");}return terms.Select(static term=>term.ToUpperInvariant()).ToArray();
    }
    private static bool TextMatches(string text,string[] terms,string? additional=null){foreach(var term in terms){if(!text.Contains(term,StringComparison.Ordinal)&&(additional is null||!additional.Contains(term,StringComparison.Ordinal))){return false;}}return true;}
    private static long Ceiling(long current,AuditQuery query){if(query.Ceiling==0){return current;}if(query.Ceiling>current){throw new ManagementException("InvalidRequest","監査ページの上限が不正です。");}return query.Ceiling;}
    public AuditPage AuditEvents(AuditQuery query)=>this.AuditEvents(query,null);
    internal AuditPage AuditEvents(AuditQuery query,Func<AuditEvent,bool>? canReadQueryTemplate){
        var terms=Validate(query);lock(this.gate){var data=this.AuditDatabase;using var read=this.database.BeginReadSession();data.AuditState.TryGetValue(new(0),out var state);var ceiling=Ceiling(state.EventSequence,query);var end=ceiling;var minimum=1L;
            if(query.FromUtc.HasValue&&query.ToUtc.HasValue){
                var first=DateOnly.FromDateTime(query.FromUtc.Value.UtcDateTime).DayNumber;var last=DateOnly.FromDateTime(query.ToUtc.Value.UtcDateTime).DayNumber;
                if(last-first<=1024){var found=false;long low=long.MaxValue,high=0;for(var day=first;day<=last;day++){if(data.AuditDays.TryGetValue(new(day),out var range)){found=true;low=Math.Min(low,range.FirstSequence);high=Math.Max(high,range.LastSequence);}}if(!found){return new([],0,ceiling,false,0);}minimum=low;end=Math.Min(end,high);}
            }
            if(query.Before>0){end=Math.Min(end,query.Before);}var rows=new List<AuditEvent>();var sequence=end;var examined=0;var characters=0;
            while(sequence>=minimum&&rows.Count<query.Limit&&examined<4096&&characters<1<<20){
                var key=sequence--;examined++;if(!data.AuditEvents.TryGetValue(new(key),out var row)){continue;}var value=row.Value;
                if(query.ObjectId is{} objectId&&!string.Equals(value.ObjectId,objectId,StringComparison.Ordinal)){continue;}
                if(query.FromUtc.HasValue&&value.CompletedAt<query.FromUtc.Value||query.ToUtc.HasValue&&value.CompletedAt>query.ToUtc.Value||query.Database is not null&&value.Database!=query.Database||query.SubjectId is not null&&value.SubjectId!=query.SubjectId||query.Action.HasValue&&value.Action!=query.Action.Value||query.Outcome.HasValue&&value.Outcome!=query.Outcome.Value){continue;}
                var allowed=canReadQueryTemplate is null||canReadQueryTemplate(value);
                if(terms.Length>0){string? template=null;if(allowed){template=value.QueryTemplate?.ToUpperInvariant();}characters+=row.SearchText.Length+(template?.Length??0);if(!TextMatches(row.SearchText,terms,template)){continue;}}
                if(!allowed){value=value with{QueryTemplate=null};}rows.Add(value);
            }
            return new(rows.ToArray(),sequence,ceiling,sequence>=minimum,examined);
        }
    }
    public bool ApplyReadSummaryBatch(Guid batchId,AuditReadDelta[] deltas){
        _=this.AuditDatabase;if(batchId==Guid.Empty||deltas is null||deltas.Length is 0 or >1024){throw new ManagementException("Validation","集計バッチは1〜1024件です。");}var frozen=deltas.ToArray();foreach(var item in frozen){Validate(item.Key);if(item.Count<=0){throw new ManagementException("Validation","集計件数は正数です。");}}string hash;if(this.Format>=5){hash=AuditReadIdentity.BatchHash(frozen);}else{hash=Hash(frozen);}var applied=false;
        this.Write(()=>{
            var data=this.AuditDatabase;if(data.AuditBatches.TryGetValue(new(batchId),out var prior)){if(prior.Hash!=hash){if(this.Format>=5&&!prior.Hash.StartsWith("v2:",StringComparison.Ordinal)){if(prior.Hash==Hash(frozen)){return;}throw new ManagementException("LegacyReplayUnverifiable","旧形式の集計バッチは再送内容を照合できません。別IDで再送せず、保存済みの集計を確認してください。");}throw new ManagementException("Conflict","集計バッチIDが異なる内容で再利用されました。");}return;}
            data.AuditState.TryGetValue(new(0),out var state);
            foreach(var delta in frozen){var id=this.SummaryId(delta.Key);AuditSummaryRow row;
                if(data.ReadSummaryIds.TryGetValue(new(id),out var location)){if(!data.ReadSummaries.TryGetValue(new(location.Sequence),out row)||row.Key!=delta.Key){throw new InvalidDataException("集計キーが衝突しています。");}row=row with{Count=checked(row.Count+delta.Count),SavedAt=DateTimeOffset.UtcNow};}
                else{state=state with{SummarySequence=checked(state.SummarySequence+1)};row=new(state.SummarySequence,delta.Key,delta.Count,DateTimeOffset.UtcNow);data.ReadSummaryIds[new(id)]=new(id,row.Sequence);}data.ReadSummaries[new(row.Sequence)]=row;
            }
            data.AuditState[new(0)]=state;data.AuditBatches[new(batchId)]=new(batchId,hash);applied=true;
        });return applied;
    }
    public AuditReadEntry? ReadSummary(AuditReadKey key){Validate(key);lock(this.gate){var data=this.AuditDatabase;using var read=this.database.BeginReadSession();if(!data.ReadSummaryIds.TryGetValue(new(this.SummaryId(key)),out var location)){return null;}if(!data.ReadSummaries.TryGetValue(new(location.Sequence),out var row)||row.Key!=key){throw new InvalidDataException("集計キーが一致しません。");}return ReadAuditEntry(row,this.ReadAuditMethod(key.CodeVersion),true);}}
    public AuditReadPage ReadSummaries(AuditQuery query)=>this.ReadSummaries(query,null);
    internal AuditReadPage ReadSummaries(AuditQuery query,Func<AuditReadKey,bool>? canReadQueryTemplate){
        ArgumentNullException.ThrowIfNull(query);
        if(query.ObjectId is not null){throw new ManagementException("Unsupported","参照集計には対象テーブル別の識別情報がありません。");}
        var terms=Validate(query);lock(this.gate){var data=this.AuditDatabase;using var read=this.database.BeginReadSession();data.AuditState.TryGetValue(new(0),out var state);var ceiling=Ceiling(state.SummarySequence,query);var sequence=ceiling;if(query.Before>0){sequence=Math.Min(sequence,query.Before);}var rows=new List<AuditReadEntry>();var examined=0;var characters=0;var methods=new Dictionary<string,AuditQueryInfo?>(StringComparer.Ordinal);
            while(sequence>0&&rows.Count<query.Limit&&examined<4096&&characters<1<<20){var id=sequence--;examined++;if(!data.ReadSummaries.TryGetValue(new(id),out var row)){continue;}var key=row.Key;
                if(query.FromUtc.HasValue&&key.WindowEnd<query.FromUtc.Value||query.ToUtc.HasValue&&key.WindowStart>query.ToUtc.Value||query.Database is not null&&key.Database!=query.Database||query.SubjectId is not null&&key.SubjectId!=query.SubjectId||query.Action.HasValue&&query.Action!=AuditAction.Read||query.Outcome.HasValue&&query.Outcome!=AuditOutcome.Succeeded){continue;}
                AuditQueryInfo? method=null;if(key.CodeVersion is{} artifact){if(!methods.TryGetValue(artifact,out method)){method=this.ReadAuditMethod(artifact);if(methods.Count<64){methods.Add(artifact,method);}}}var allow=canReadQueryTemplate is null||canReadQueryTemplate(key);var entry=ReadAuditEntry(row,method,allow);
                if(terms.Length>0){var text=ReadAuditSearchText(key,method);var template=entry.Method?.QueryTemplate?.ToUpperInvariant();characters+=text.Length+(template?.Length??0);if(!TextMatches(text,terms,template)){continue;}}rows.Add(entry);
            }
            return new(rows.ToArray(),sequence,ceiling,sequence>0,examined);
        }
    }
}

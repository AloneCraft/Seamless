using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using MemoryPack;
using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Execution.Growth;
using ExecutionStore=Seamless.Execution.Store;

namespace Seamless.Management.Server;

[MemoryPackable]internal readonly partial record struct ExecutionHistoryRow(long Key,Guid SessionId,byte[] Payload,byte[] Integrity):IRecord<long>;
[MemoryPackable]internal sealed partial record ExecutionHistoryPeriodSeal(int Format,Guid SessionId,long First,long Last,int Count,Guid Checkpoint,ExecutionHistoryPeriodFile[] Files);
[MemoryPackable]internal sealed partial record ExecutionHistoryPeriodFile(string Path,long Length,byte[] Hash);
[MemoryPackable]internal sealed partial record ExecutionPeriodSeal(int Format,string PayloadKind,Guid SessionId,long First,long Last,int Count,Guid Checkpoint,ExecutionHistoryPeriodFile[] Files);
internal sealed class IncompleteExecutionHistoryPeriodException():IOException("未完了の実績期間が残っているため、新しい記録を停止しました。");

/// <summary>完了済みの独立期間だけを公開する。既存期間は必須情報を検査してから開く。</summary>
internal sealed class ExecutionHistoryPeriods(string directory,ResourceBudget budget,string payloadKind="History",int maximumRows=64,TsavoriteTuning? tuning=null,Action? beforeOpen=null){
    private const int MaximumPayloadBytes=32768,MaximumSealBytes=1<<20,MaximumFiles=512;
    private const string SealName="period.seal";
    private static readonly TsavoriteTuning Tuning=new(){IndexSize=4096,PageSize=4096,SegmentSize=16384,LogMemorySize=8192,Growth=new Policy<Fixed>()};
    private static readonly StoreLimits Limits=new(){MemoryBytes=32L<<20,DiskBytes=16L<<20};
    private readonly int rowLimit=CheckRowLimit(maximumRows);
    private readonly TsavoriteTuning effectiveTuning=tuning??Tuning;
    private static int CheckRowLimit(int value){ArgumentOutOfRangeException.ThrowIfLessThan(value,1);ArgumentOutOfRangeException.ThrowIfGreaterThan(value,1024);return value;}
    internal void EnsureWritable(){
        if(Directory.Exists(directory)&&Directory.EnumerateDirectories(directory,"writing-*").Any()){throw new IncompleteExecutionHistoryPeriodException();}
    }
    internal string Write(IReadOnlyList<QueryExecutionHistoryEntry> entries){
        return this.WriteRows(entries.Select(Encode).ToArray());
    }
    internal string WriteRows(IReadOnlyList<ExecutionHistoryRow> entries){
        if(entries.Count<1||entries.Count>this.rowLimit||entries.Any(entry=>entry.SessionId!=entries[0].SessionId)||entries.Select(entry=>entry.Key).Distinct().Count()!=entries.Count){throw new InvalidDataException("実績期間の行が不正です。");}
        foreach(var entry in entries){ValidateRow(entry);}
        // 前回の異常終了領域は自動削除せず、新しい領域の積み増しを止める。
        this.EnsureWritable();
        Directory.CreateDirectory(directory);var id=Guid.NewGuid().ToString("N");var staging=Path.Combine(directory,"writing-"+id);
        var destination=Path.Combine(directory,"period-"+DateTimeOffset.UtcNow.UtcTicks.ToString("D19",System.Globalization.CultureInfo.InvariantCulture)+"-"+id[..12]);
        try{
            Guid checkpoint;
            using(var owner=ExecutionStore.Open(staging,Limits,parentBudget:budget)){
                var data=owner.CreateTsavoriteSet<long,ExecutionHistoryRow>("frames",this.effectiveTuning);
                using(var rows=data.Create()){foreach(var entry in entries){if(!rows.Add(entry)){throw new InvalidDataException("実績の連番が重複しています。");}}}
                checkpoint=owner.Save();
            }
            var evidence=Files(staging).Select(path=>new ExecutionHistoryPeriodFile(Path.GetRelativePath(staging,path).Replace('\\','/'),new FileInfo(path).Length,HashFile(path))).ToArray();
            if(evidence.Length>MaximumFiles){throw new InvalidDataException("実績期間のファイル上限を超えています。");}
            var seal=new ExecutionPeriodSeal(2,payloadKind,entries[0].SessionId,entries.Min(entry=>entry.Key),entries.Max(entry=>entry.Key),entries.Count,checkpoint,evidence);
            WriteSeal(Path.Combine(staging,SealName),seal);Directory.Move(staging,destination);return destination;
        }catch{
            var prefix=Path.GetFullPath(directory)+Path.DirectorySeparatorChar;var target=Path.GetFullPath(staging);
            if(target.StartsWith(prefix,StringComparison.Ordinal)&&Directory.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)==0){try{Directory.Delete(target,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
            throw;
        }
    }
    internal QueryExecutionHistoryEntry[] Read(string period,CancellationToken cancellation){
        var result=this.ReadRows(period,cancellation).Select(Decode).ToArray();
        if(result.Select(entry=>entry.Observation.ExecutionId).Distinct().Count()!=result.Length){throw new InvalidDataException("実績の実行識別子が重複しています。");}
        return result;
    }
    internal ExecutionHistoryRow[] ReadRows(string period,CancellationToken cancellation){
        var seal=ReadSeal(Path.Combine(period,SealName));
        if(seal.Format is not(1 or 2)||seal.PayloadKind!=payloadKind||seal.SessionId==Guid.Empty||seal.First<1||seal.Last<seal.First||seal.Count<1||seal.Count>this.rowLimit||seal.Checkpoint==Guid.Empty||seal.Files is null||seal.Files.Length is <1 or >MaximumFiles){throw new InvalidDataException("実績期間のsealが不正です。");}
        var names=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var file in seal.Files){
            cancellation.ThrowIfCancellationRequested();
            if(file is null||file.Path is null||!names.Add(file.Path)||file.Hash is not{Length:32}||file.Length<0||file.Length>(16L<<20)){throw new InvalidDataException("実績期間のファイル情報が不正です。");}
            var path=ExistingFile(period,file.Path);
            if(new FileInfo(path).Length!=file.Length||!CryptographicOperations.FixedTimeEquals(HashFile(path),file.Hash)){throw new InvalidDataException("実績期間のファイルが欠落または破損しています。");}
        }
        ValidateManifest(period,seal.Checkpoint);
        beforeOpen?.Invoke();
        // 検査後の外部削除でも、欠損した親ディレクトリやマニフェストを新規作成しない。
        using var owner=ExecutionStore.OpenExisting(period,Limits,parentBudget:budget);
        if(owner.CurrentCheckpoint!=seal.Checkpoint){throw new InvalidDataException("実績期間を空Storeへ置き換えることはできません。");}
        var data=owner.CreateTsavoriteSet<long,ExecutionHistoryRow>("frames",this.effectiveTuning);using var rows=data.Create();
        var result=new List<ExecutionHistoryRow>(seal.Count);var keys=new System.Collections.Generic.HashSet<long>();
        foreach(var row in rows){
            cancellation.ThrowIfCancellationRequested();
            if(result.Count==seal.Count||!keys.Add(row.Key)){throw new InvalidDataException("実績期間の件数またはキーが不正です。");}
            ValidateRow(row);
            if(row.SessionId!=seal.SessionId||row.Key<seal.First||row.Key>seal.Last){throw new InvalidDataException("実績行と期間の識別が一致しません。");}
            result.Add(row);
        }
        if(result.Count!=seal.Count||keys.Min()!=seal.First||keys.Max()!=seal.Last){throw new InvalidDataException("実績期間の行が不足しています。");}
        return result.ToArray();
    }
    internal static ExecutionHistoryRow Encode(QueryExecutionHistoryEntry entry){
        return EncodePayload(entry.Sequence,entry.SessionId,MemoryPackSerializer.Serialize(entry));
    }
    internal static ExecutionHistoryRow EncodePayload(long key,Guid session,byte[] payload){
        if(payload.Length>MaximumPayloadBytes){throw new InvalidDataException("実績行の上限を超えています。");}
        return new(key,session,payload,RowHash(key,session,payload));
    }
    internal static QueryExecutionHistoryEntry Decode(ExecutionHistoryRow row){
        ValidateRow(row);
        var entry=MemoryPackSerializer.Deserialize<QueryExecutionHistoryEntry>(row.Payload)??throw new InvalidDataException("実績行がありません。");
        if(entry.Sequence!=row.Key||entry.SessionId!=row.SessionId||entry.Observation is null){throw new InvalidDataException("実績行のキーが一致しません。");}return entry;
    }
    private static void ValidateRow(ExecutionHistoryRow row){
        if(row.Key<1||row.SessionId==Guid.Empty||row.Payload is null||row.Payload.Length>MaximumPayloadBytes||row.Integrity is not{Length:32}||!CryptographicOperations.FixedTimeEquals(RowHash(row.Key,row.SessionId,row.Payload),row.Integrity)){throw new InvalidDataException("実績行の内容hashが一致しません。");}
    }
    private static byte[] RowHash(long key,Guid session,byte[] payload){
        Span<byte> identity=stackalloc byte[24];BinaryPrimitives.WriteInt64LittleEndian(identity,key);session.TryWriteBytes(identity[8..]);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);hash.AppendData(identity);hash.AppendData(payload);return hash.GetHashAndReset();
    }
    private static string[] Files(string period)=>Directory.EnumerateFiles(period,"*",SearchOption.AllDirectories).Where(path=>Path.GetFileName(path) is not ("store.lock" or SealName)).Order(StringComparer.Ordinal).Take(MaximumFiles+1).ToArray();
    private static byte[] HashFile(string path){using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return SHA256.HashData(file);}
    private static string ExistingFile(string period,string relative){
        if(relative.Length>4096||Path.IsPathRooted(relative)||relative.Contains('\\')){throw new InvalidDataException("実績ファイルのパスが不正です。");}
        var path=Path.GetFullPath(Path.Combine(period,relative.Replace('/',Path.DirectorySeparatorChar)));var prefix=Path.GetFullPath(period)+Path.DirectorySeparatorChar;
        if(!path.StartsWith(prefix,StringComparison.Ordinal)||!File.Exists(path)){throw new InvalidDataException("実績ファイルがありません。");}
        var current=path;
        while(current.Length>=period.Length){if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0){throw new InvalidDataException("実績期間のリンクは読み取れません。");}if(current==period){break;}current=Path.GetDirectoryName(current)!;}
        return path;
    }
    private static void ValidateManifest(string period,Guid expected){
        var path=Path.Combine(period,"store.json");if(!File.Exists(path)||new FileInfo(path).Length>65536){throw new InvalidDataException("実績期間のmanifestがありません。");}
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path));var root=manifest.RootElement;
        if(root.GetProperty("Format").GetString()!="seamless-store-v1"){throw new InvalidDataException("実績期間の保存形式が異なります。");}
        var checkpoints=root.GetProperty("Checkpoints");if(checkpoints.GetArrayLength()!=1||checkpoints[0].GetProperty("Token").GetGuid()!=expected){throw new InvalidDataException("実績期間の保存世代が一致しません。");}
        var components=checkpoints[0].GetProperty("Components");if(components.EnumerateObject().Count()!=1||!components.TryGetProperty("frames",out var frame)){throw new InvalidDataException("実績期間のSetがありません。");}
        using var state=JsonDocument.Parse(frame.GetString()??throw new InvalidDataException("実績期間のSet情報がありません。"));var token=state.RootElement.GetProperty("Token").GetGuid().ToString();
        foreach(var kind in new[]{"cpr-checkpoints","index-checkpoints"}){if(!Directory.Exists(Path.Combine(period,"data","frames","checkpoints",kind,token))){throw new InvalidDataException("実績期間のcheckpointがありません。");}}
    }
    private static void WriteSeal(string path,ExecutionPeriodSeal seal){
        var payload=MemoryPackSerializer.Serialize(seal);if(payload.Length>MaximumSealBytes){throw new InvalidDataException("実績期間のseal上限を超えています。");}
        using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write("SEHIST02"u8);Span<byte> length=stackalloc byte[4];BinaryPrimitives.WriteInt32LittleEndian(length,payload.Length);file.Write(length);file.Write(SHA256.HashData(payload));file.Write(payload);file.Flush(true);
    }
    private static ExecutionPeriodSeal ReadSeal(string path){
        if(!File.Exists(path)||new FileInfo(path).Length is <44 or >MaximumSealBytes+44){throw new InvalidDataException("実績期間のsealがありません。");}
        var bytes=File.ReadAllBytes(path);var legacy=bytes.AsSpan(0,8).SequenceEqual("SEHIST01"u8);
        if((!legacy&&!bytes.AsSpan(0,8).SequenceEqual("SEHIST02"u8))||BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8))!=bytes.Length-44||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(44)),bytes.AsSpan(12,32))){throw new InvalidDataException("実績期間のsealが破損しています。");}
        if(legacy){var seal=MemoryPackSerializer.Deserialize<ExecutionHistoryPeriodSeal>(bytes.AsSpan(44))??throw new InvalidDataException("実績期間のseal内容がありません。");if(seal.Format!=1){throw new InvalidDataException("実績期間のseal版が不正です。");}return new(seal.Format,"History",seal.SessionId,seal.First,seal.Last,seal.Count,seal.Checkpoint,seal.Files);}
        var current=MemoryPackSerializer.Deserialize<ExecutionPeriodSeal>(bytes.AsSpan(44))??throw new InvalidDataException("実績期間のseal内容がありません。");
        if(current.Format!=2){throw new InvalidDataException("実績期間のseal版が不正です。");}return current;
    }
}

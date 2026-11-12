using System.Buffers.Binary;
using System.Text;
using MemoryPack;

namespace Seamless.Management.Server;

public sealed partial class ManagementStore{
    internal StoreHeader ExportRaw(string path,CancellationToken cancellationToken=default){
        if(this.Format>=7){throw new NotSupportedException("型付き管理行の再移行には形式7の専用転送が必要です。バックアップは全保存領域のコピーを使用してください。");}
        lock(this.gate){using var read=this.database.BeginReadSession();using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);Span<byte> prefix=stackalloc byte[4];
            foreach(var item in this.database.Entries){cancellationToken.ThrowIfCancellationRequested();var bytes=MemoryPackSerializer.Serialize(item);if(bytes.Length>1<<20){throw new ManagementException("Limit","管理レコードの転送上限を超えています。");}BinaryPrimitives.WriteInt32LittleEndian(prefix,bytes.Length);output.Write(prefix);output.Write(bytes);}output.Flush(true);return this.header;
        }
    }
    internal static ManagementStore CreateAuditStage(string root)=>CreateUpgradeStage(root,5);
    internal static ManagementStore CreateUpgradeStage(string root,int format){
        if(format is not 5 and not 6 and not 7){throw new ArgumentOutOfRangeException(nameof(format));}
        Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"management.format"),format.ToString(System.Globalization.CultureInfo.InvariantCulture),Encoding.UTF8);return new(root,true);
    }
    internal void ImportRaw(string path,StoreHeader original,CancellationToken cancellationToken=default){
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){var prefix=new byte[4];
            while(input.Position<input.Length){cancellationToken.ThrowIfCancellationRequested();var rows=new List<ManagementRecord>(64);for(var i=0;i<64&&input.Position<input.Length;i++){input.ReadExactly(prefix);var length=BinaryPrimitives.ReadInt32LittleEndian(prefix);if(length is <1 or >1<<20||length>input.Length-input.Position){throw new InvalidDataException("管理レコードの転送形式が不正です。");}var bytes=new byte[length];input.ReadExactly(bytes);rows.Add(MemoryPackSerializer.Deserialize<ManagementRecord>(bytes));}
                using var transaction=this.database.BeginTransaction();foreach(var row in rows){if(row.Id!=Key(row.Kind,row.Subject)){throw new InvalidDataException("管理レコードのキーが一致しません。");}if(this.Format>=7&&(this.TypedKind(row.Kind)||row.Kind=="chunk"&&(row.Subject.StartsWith("user\0",StringComparison.Ordinal)||row.Subject.StartsWith("serverSettings\0",StringComparison.Ordinal)))){continue;}this.database.Entries[new(row.Id)]=row;}transaction.Commit();
            }
        }
        this.header=original with{Format=this.Format};this.Write(()=>{});if(original.Format>=4){return;}
        for(var first=1L;first<=original.HistorySequence;){var end=Math.Min(original.HistorySequence,first+63);this.Write(()=>{for(var index=first;index<=end;index++){var entry=this.Read<HistoryEntry>("history",index.ToString(System.Globalization.CultureInfo.InvariantCulture));if(entry is not null){this.AppendLegacyHistoryAudit(entry);}}});first=end+1;}
    }
    private void AppendLegacyHistoryAudit(HistoryEntry entry){
        var action=AuditAction.Other;if(entry.Action is "Applied" or "Rebuilt" or "UpdateAuditEnabled"){action=AuditAction.SchemaChange;}else if(entry.Action is "UserChanged" or "PasswordReset"){action=AuditAction.AccountChange;}else if(entry.Action.StartsWith("Validation",StringComparison.Ordinal)){action=AuditAction.Validation;}
        string? code=null;QueryAuditMetadata? metadata=null;if(entry.Action is "Applied" or "Rebuilt" or "UpdateAuditEnabled"){var version=this.Read<DefinitionVersion>("version",entry.DefinitionId+":"+entry.Version);code=version?.ArtifactHash;if(version is not null){metadata=this.Read<QueryAuditMetadata>("queryAudit",version.ArtifactHash);if(metadata is not null){QueryAuditMetadataBuilder.Validate(metadata,version.Definition.Source.Hash(),version.ArtifactHash);}}}
        var outcome=AuditOutcome.Succeeded;if(entry.Action=="ValidationFailed"){outcome=AuditOutcome.Failed;}
        this.AppendAuditInside(new(Key("audit-history",this.InstanceId.ToString("N")+":"+entry.Sequence),null,entry.At,entry.At,this.InstanceId,"legacy-history:"+Normalize(entry.User),entry.Database,entry.DefinitionId,action,outcome,null,code,null,entry.Action,entry.Action,metadata?.Namespace,metadata?.TypeName,metadata?.MethodName,metadata?.MethodSignature,metadata?.QueryTemplate,metadata?.ModuleId,metadata?.MetadataToken));
    }
}

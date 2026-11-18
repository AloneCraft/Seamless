using System.Reflection;
using Seamless.Database;
using Seamless.Execution.Codecs;

namespace Seamless.Management.Server;

// 停止中の旧ホストと現ホストの間で、実際の保存に使う正規形式で行を受け渡す。
// 現ホストは新世代に取り込み、再オープン後に全キーの値と件数を照合する。
internal sealed class HostRebuildRows:IDisposable{
    private const string Format="SEAMLESS-HOST-REBUILD-1";
    private const int MaximumRecordBytes=64*1024*1024;
    private readonly FileStream transfer;
    private readonly string receipt;

    internal HostRebuildRows(string path,string receipt){
        SourceWorkspace.CheckLinks(path);
        this.transfer=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        this.receipt=receipt;
    }

    internal static void Export(DatabaseInstance instance,string path,string receipt){
        var database=instance.Database;
        var declarations=Tables(database);
        SourceWorkspace.CheckLinks(path);
        using var read=database.BeginReadSession();
        using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        using var writer=new BinaryWriter(file);
        writer.Write(Format);writer.Write(receipt);writer.Write(declarations.Length);
        foreach(var declaration in declarations){
            writer.Write(declaration.Path);
            Handler<Action<object,BinaryWriter>>(declaration,nameof(ExportTable))(declaration.GetTable(database),writer);
        }
        writer.Flush();file.Flush(true);
    }

    internal void Import(DatabaseInstance instance,CancellationToken cancellation){
        using var snapshot=instance.Database.BeginUnpublishedSnapshot();
        this.Read(instance,cancellation,(declaration,reader,token)=>
            Handler<Action<object,BinaryReader,DatabaseContainer.UnpublishedSnapshotBuilder,CancellationToken>>(declaration,nameof(ImportTable))
                (declaration.GetTable(instance.Database),reader,snapshot,token));
        snapshot.Complete();
    }

    internal void Verify(DatabaseInstance instance,CancellationToken cancellation){
        using var read=instance.Database.BeginReadSession(cancellation);
        this.Read(instance,cancellation,(declaration,reader,token)=>
            Handler<Action<object,BinaryReader,CancellationToken>>(declaration,nameof(VerifyTable))(declaration.GetTable(instance.Database),reader,token));
        instance.Database.ValidateRelations();
    }

    private void Read(DatabaseInstance instance,CancellationToken cancellation,Action<DatabaseTableDeclaration,BinaryReader,CancellationToken> readTable){
        this.transfer.Position=0;
        using var reader=new BinaryReader(this.transfer,System.Text.Encoding.UTF8,leaveOpen:true);
        var declarations=Tables(instance.Database);
        if(reader.ReadString()!=Format||reader.ReadString()!=this.receipt||reader.ReadInt32()!=declarations.Length){
            throw new InvalidDataException("旧ホストからの行データが復旧対象と一致しません。");
        }
        foreach(var declaration in declarations){
            cancellation.ThrowIfCancellationRequested();
            if(reader.ReadString()!=declaration.Path){throw new InvalidDataException("復旧前後で表の経路が一致しません。");}
            readTable(declaration,reader,cancellation);
        }
        if(this.transfer.Position!=this.transfer.Length){throw new InvalidDataException("旧ホストの行データに余分な情報があります。");}
    }

    private static DatabaseTableDeclaration[] Tables(DatabaseContainer database){
        var type=database.GetType();
        var declarations=DatabaseTableDeclaration.Read(type);
        if(declarations.Any(table=>table.Property.PropertyType.GetGenericTypeDefinition()!=typeof(Table<,>))||
            DatabaseTableDeclaration.ReadDirectSets(type).Length!=0||HasMaterializedView(type)){
            throw new ManagementException("Unsupported","行を保持するホスト復旧は、主キー付きの通常表だけを持つDBが対象です。");
        }
        return declarations.OrderBy(table=>table.Path,StringComparer.Ordinal).ToArray();
    }

    private static bool HasMaterializedView(Type owner)=>owner.GetProperties(BindingFlags.Public|BindingFlags.Instance).Any(property=>{
        var type=property.PropertyType;
        return type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(MaterializedView<,>)||
            typeof(DatabaseSchema).IsAssignableFrom(type)&&HasMaterializedView(type);
    });

    private static T Handler<T>(DatabaseTableDeclaration table,string method) where T:Delegate=>
        typeof(HostRebuildRows).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!
            .MakeGenericMethod(table.Property.PropertyType.GenericTypeArguments).CreateDelegate<T>();

    private static void ExportTable<TKey,TRecord>(object source,BinaryWriter writer) where TKey:struct where TRecord:struct{
        var table=(StoredTable<TKey,TRecord>)source;
        var count=table.LongCount;
        writer.Write(count);
        foreach(var row in table){
            var value=row;
            var bytes=CanonicalSerializer<TRecord>.Serialize(ref value);
            if(bytes.Length>MaximumRecordBytes){throw new ManagementException("Limit","復旧する行は64MiB以下にしてください。");}
            writer.Write(bytes.Length);writer.Write(bytes);
            count--;
        }
        if(count!=0){throw new InvalidDataException("旧ホストの表の行数と読み出した行数が一致しません。");}
    }

    private static void ImportTable<TKey,TRecord>(object target,BinaryReader reader,DatabaseContainer.UnpublishedSnapshotBuilder snapshot,CancellationToken cancellation)
        where TKey:struct where TRecord:struct{
        var table=(StoredTable<TKey,TRecord>)target;
        for(var remaining=Count(reader);remaining>0;remaining--){
            cancellation.ThrowIfCancellationRequested();
            snapshot.Insert(table,CanonicalSerializer<TRecord>.Deserialize(Bytes(reader)));
        }
    }

    private static void VerifyTable<TKey,TRecord>(object target,BinaryReader reader,CancellationToken cancellation) where TKey:struct where TRecord:struct{
        var table=(StoredTable<TKey,TRecord>)target;
        var count=Count(reader);
        if(count!=table.LongCount){throw new InvalidDataException("復旧前後の表の行数が一致しません。");}
        for(;count>0;count--){
            cancellation.ThrowIfCancellationRequested();
            var expected=Bytes(reader);
            var row=CanonicalSerializer<TRecord>.Deserialize(expected);
            if(!table.TryGetValue(table.RecordKey(row),out var actual)||!CanonicalSerializer<TRecord>.Serialize(ref actual).SequenceEqual(expected)){
                throw new InvalidDataException("再オープン後の行が旧ホストの行と一致しません。");
            }
        }
    }

    private static long Count(BinaryReader reader){
        var count=reader.ReadInt64();
        if(count<0||count>(reader.BaseStream.Length-reader.BaseStream.Position)/sizeof(int)){throw new InvalidDataException("旧ホストの行数が不正です。");}
        return count;
    }

    private static byte[] Bytes(BinaryReader reader){
        var length=reader.ReadInt32();
        if(length<0||length>MaximumRecordBytes||length>reader.BaseStream.Length-reader.BaseStream.Position){
            throw new InvalidDataException("旧ホストの行データ長が不正です。");
        }
        var bytes=reader.ReadBytes(length);
        if(bytes.Length!=length){throw new EndOfStreamException();}
        return bytes;
    }

    public void Dispose()=>this.transfer.Dispose();
}

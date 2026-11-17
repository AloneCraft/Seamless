using System.Reflection;
using System.Runtime.ExceptionServices;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Transfer;

/// <summary>
/// 取り込み元(SQL Server・CSV など)の行を、生成したコンテナへまとめて入れる。
/// 1つの取引に全部を入れると、変更の記録(WAL)の1回分の上限を超える(大きな DB は入らない)。そこで、
/// まだ公開していない新しい領域へ書き、全部の行を入れてから外部キーなどの関係をまとめて検査して公開する。
/// 途中で失敗したら公開しない(全部入るか、何も入らないか)。
/// </summary>
public static class GeneratedDatabaseLoader{
    /// <summary>
    /// 取り込み先の新しい DB の設定。表ごとの保存の大きさ(<paramref name="tableStorage"/>)と、
    /// 表の数に見合うメモリの上限を持たせる。表1つで約15MB を先に取るので、既定の 256MB では表の多い DB が入らない。
    /// </summary>
    public static DatabaseContainerOptions Options(string directory,IReadOnlyList<GeneratedTableBinding> tables,IReadOnlyDictionary<string,DatabaseTableStorageOptions> tableStorage){
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);ArgumentNullException.ThrowIfNull(tables);ArgumentNullException.ThrowIfNull(tableStorage);
        var measured=tableStorage.Values.Sum(static storage=>(long)storage.MemorySize);
        return DatabaseContainerOptions.OnStorage(directory) with{
            // 公開前の領域へまとめて書くには、canonical の保存形式が要る(DatabaseContainer.BeginUnpublishedSnapshot)。
            RecordSerialization=DatabaseRecordSerialization.CanonicalV1,
            // Manager と同じ規則(64MB+表の数×16MB)に、測った表のメモリの合計を足す(W04-MIGRATION-SPEED-TABLES)。
            ManagedMemoryLimitBytes=Math.Max(256L<<20,(64L<<20)+tables.Count*(16L<<20)+measured),
            TableStorage=tableStorage,
        };
    }

    /// <summary>
    /// 新しい(まだ何も書いていない)DB へ、<paramref name="tables"/> の表を取り込み元から入れ、公開する。表ごとの件数を返す。
    /// </summary>
    public static IReadOnlyDictionary<string,long> Load(DatabaseContainer database,IReadOnlyList<GeneratedTableBinding> tables,IDatabaseImportSource source,IProgress<GeneratedTableLoadProgress>? progress=null,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(database);ArgumentNullException.ThrowIfNull(tables);ArgumentNullException.ThrowIfNull(source);
        var counts=new Dictionary<string,long>(StringComparer.Ordinal);
        using var snapshot=database.BeginUnpublishedSnapshot();
        foreach(var binding in tables){
            cancellationToken.ThrowIfCancellationRequested();
            var table=TableObject(database,binding.ObjectName);
            var types=table.GetType().GetGenericArguments();
            var method=typeof(GeneratedDatabaseLoader).GetMethod(nameof(LoadTable),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(types[0],types[1]);
            try{
                counts[binding.ObjectName]=(long)method.Invoke(null,[table,binding.ObjectName,source,snapshot,progress,cancellationToken])!;
            }catch(TargetInvocationException error)when(error.InnerException is OperationCanceledException canceled){
                ExceptionDispatchInfo.Capture(canceled).Throw();
                throw;
            }catch(TargetInvocationException error)when(error.InnerException is not null){
                // どの表で失敗したかを、利用者が分かるように付ける。元の例外は中に残す。
                throw new InvalidOperationException($"表 '{binding.ObjectName}' の取り込みに失敗しました: {error.InnerException.Message}",error.InnerException);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        snapshot.Complete();
        return counts;
    }

    private static long LoadTable<TKey,TRow>(object table,string name,IDatabaseImportSource source,DatabaseContainer.UnpublishedSnapshotBuilder snapshot,IProgress<GeneratedTableLoadProgress>? progress,CancellationToken cancellationToken) where TKey:struct where TRow:struct{
        var typed=(Table<TKey,TRow>)table;long count=0;
        foreach(var row in source.ReadTable<TRow>(new(name))){
            cancellationToken.ThrowIfCancellationRequested();
            snapshot.Insert(typed,row);count=checked(count+1);
            if(progress is not null&&count%10_000==0){progress.Report(new(name,count,false));}
        }
        progress?.Report(new(name,count,true));
        return count;
    }

    // 表の束縛の名前(「スキーマ.表」)をたどって、コンテナの表を得る。
    private static object TableObject(DatabaseContainer database,string path){
        object current=database;
        foreach(var segment in path.Split('.')){
            var property=current.GetType().GetProperty(segment,BindingFlags.Instance|BindingFlags.Public)??throw new MissingMemberException(current.GetType().FullName,segment);
            current=property.GetValue(current)??throw new InvalidDataException($"表 '{path}' をたどる途中の '{segment}' が null です。");
        }
        return current;
    }
}

/// <summary>取り込みの進み具合。<paramref name="Completed"/> はその表を読み終えたか。</summary>
public sealed record GeneratedTableLoadProgress(string Table,long Rows,bool Completed);

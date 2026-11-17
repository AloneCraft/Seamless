using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

using global::Seamless.Collections.Tsavorite;
using ExecutionStore=global::Seamless.Execution.Store;

// 直接使う表（Set<T>・Set<TKey,T>）。DB の型 Seamless.Database.Set を返す。中では並列区間を常に開き、複数のスレッドから同時に使える。
// 取引・WAL・版の管理・DB全体のゲートは通さない。書き込みは取り消せず、保存はDBを閉じたときのチェックポイントだけで行う。異常終了後の復旧は保証しない。
// データ本体は、取引の保存領域とは別の共通ストア(予算の管理を持つ)に作る。区間を開き直すときに、ログと索引をその予算の範囲で広げられる。
// 宣言はトランザクションで使う表（Table<T>）と分け、同じ表を両方の書き方で使えないようにする（生成器が入口を分ける）。
// 設計：Document/refactoring-designs/20261112-DBアクセスモデル.md の I1・I6、Document/refactoring-designs/20261115-取引を通さない表の作り直し.md
public abstract partial class DatabaseContainer {
    // 11/12 から 11/15 までの形(表ごとのフォルダに素の Tsavorite のチェックポイント)。読み替えはしない。
    private const string LegacyDirectSetDirectoryName="direct";
    private const string DirectSetDirectoryName="direct-v2";
    private readonly global::System.Collections.Generic.Dictionary<string,IDisposable> directSets=new(StringComparer.Ordinal);
    private ExecutionStore? directStore;

    /// <summary>
    /// 宣言プロパティから呼ばれ、キーのない直接使う表を宣言名単位で生成・共有する。複数のスレッドから同時に使える。
    /// </summary>
    protected global::Seamless.Database.Set<TRecord> Set<TRecord>([global::System.Runtime.CompilerServices.CallerMemberName] string name="")
        where TRecord:notnull=>
        this.GetDirectSet(name,static(store,name,tuning)=>new global::Seamless.Database.Set<TRecord>(store.CreateTsavoriteHashSet<TRecord>(name,tuning)));

    /// <summary>宣言プロパティから呼ばれ、キーで探索できる直接使う表を宣言名単位で生成・共有する。複数のスレッドから同時に使える。</summary>
    protected global::Seamless.Database.Set<TKey,TRecord> Set<TKey,TRecord>([global::System.Runtime.CompilerServices.CallerMemberName] string name="")
        where TRecord:global::Seamless.Collections.IRecord<TKey> =>
        this.GetDirectSet(name,static(store,name,tuning)=>new global::Seamless.Database.Set<TKey,TRecord>(store.CreateTsavoriteSet<TKey,TRecord>(name,tuning)));

    private TSet GetDirectSet<TSet>(string name,Func<ExecutionStore,string,TsavoriteTuning,TSet> create) where TSet:class,IDisposable {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.directSets.TryGetValue(name,out var existing)) {
                return (TSet)existing;
            }
            var sizing=this.ResolveTableStorage(name);
            var tuning=new TsavoriteTuning{IndexSize=checked(sizing.IndexSize*64),PageSize=sizing.PageSize,SegmentSize=sizing.SegmentSize,LogMemorySize=sizing.MemorySize};
            var created=create(this.EnsureDirectStore(),name,tuning);
            this.directSets.Add(name,created);
            return created;
        }
    }

    // gate を持った状態で呼ぶ。
    private ExecutionStore EnsureDirectStore() {
        if(this.directStore is not null) {
            return this.directStore;
        }
        if(this.Options.StorageMode==DatabaseStorageMode.Memory) {
            this.directStore=ExecutionStore.CreateMemory(options:this.Options.SharedStoreOptions,parentBudget:this.executionResources.Budget);
            return this.directStore;
        }
        var root=this.ResolveStorageRoot();
        if(Directory.Exists(Path.Combine(root,LegacyDirectSetDirectoryName))) {
            throw new InvalidOperationException($"取引を通さない表が古い形式({LegacyDirectSetDirectoryName})で保存されています。自動では読み替えません。新しいフォルダの DB へ明示的に移してください。");
        }
        // 取引の保存領域と混ざらないよう、直接使う表は専用の下位ディレクトリの共通ストアに置く。
        this.directStore=ExecutionStore.Open(Path.Combine(root,DirectSetDirectoryName),this.SharedStoreLimits,this.Options.SharedStoreOptions,this.executionResources.Budget);
        return this.directStore;
    }

    // 直接使う表の保存先は旧方式のDBの印(checkpoint.token)の検出から外す。
    private static bool IsDirectSetPath(string root,string path)=>
        IsUnder(Path.Combine(root,DirectSetDirectoryName),path)||IsUnder(Path.Combine(root,LegacyDirectSetDirectoryName),path);
    private static bool IsUnder(string directory,string path)=>
        Path.GetRelativePath(directory,path) is var relative&&!relative.StartsWith("..",StringComparison.Ordinal)&&!Path.IsPathRooted(relative);

    /// <summary>DBを閉じるときに直接使う表を閉じる。全部の操作の終わりを待って並列区間を閉じ、永続ならチェックポイントを書く。gate を持った状態で呼ぶ。</summary>
    private void DisposeDirectSets(List<Exception> errors) {
        var closed=true;
        foreach(var set in this.directSets.Values) {
            try{set.Dispose();}catch(Exception error){errors.Add(error);closed=false;}
        }
        this.directSets.Clear();
        if(this.directStore is null){return;}
        // 区間を閉じられなかった表があるときは、途中の状態を保存しない(前のチェックポイントのまま)。
        if(closed&&this.directStore.Kind==global::Seamless.Execution.StoreKind.Persistent) {
            try{this.directStore.Save();}catch(Exception error){errors.Add(error);}
        }
        try{this.directStore.Dispose();}catch(Exception error){errors.Add(error);}
        this.directStore=null;
    }
}

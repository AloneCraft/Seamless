namespace Seamless.Database;

/// <summary>
/// 宣言的データベースコンテナの基底。
/// 派生 partial クラスの Table/View/TableFunction/スカラー関数宣言から、
/// Seamless.Database の source generator がスキーマメタデータと取り込み配線を生成する。
/// Table 実体は Tsavorite ストアで、<see cref="DatabaseContainerOptions"/> により
/// メモリ範囲内運用とストレージ利用を切り替える。
/// </summary>
public abstract class DatabaseContainer(DatabaseContainerOptions? options=null):IDisposable {
    private readonly System.Collections.Generic.Dictionary<string,IDisposable> tables= new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string,object>      views = new(StringComparer.Ordinal);
    private readonly object                                                    gate  = new();
    private          bool                                                      disposed;
    public           DatabaseContainerOptions                                  Options { get; }=options??DatabaseContainerOptions.InMemory;
    /// <summary>SQL Server 互換のデータベース名。[Database]属性があれば generator が override を生成する。</summary>
    public virtual string DatabaseName => this.GetType().Name;

    /// <summary>generator が宣言メンバーから生成するオブジェクト一覧。生成前は空。</summary>
    public virtual IReadOnlyList<DatabaseObjectDeclaration> DatabaseObjects => [];

    /// <summary>宣言プロパティから呼ばれ、テーブル実体を宣言名単位で生成・共有する。</summary>
    protected Table<TRecord> Table<TRecord>([CallerMemberName] string name = "") {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.tables.TryGetValue(name,out var existing)) {
                return (Table<TRecord>)existing;
            }
            var table = this.CreateTable<TRecord>(name);
            this.tables.Add(name,table);
            return table;
        }
    }

    /// <summary>宣言プロパティから呼ばれ、ビューを宣言名単位で生成・共有する。</summary>
    protected View<TResult> View<TResult>(Func<IEnumerable<TResult>> definition,[CallerMemberName] string name = "") {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.views.TryGetValue(name,out var existing)) {
                return (View<TResult>)existing;
            }
            var view = new View<TResult>(definition,name,this.ResolveSqlName(DatabaseObjectKind.View,name));
            this.views.Add(name,view);
            return view;
        }
    }

    /// <summary>宣言メソッドから呼ばれ、引数をキャプチャしたテーブル関数を生成する。</summary>
    protected TableFunction<TResult> TableFunction<TResult>(Func<IEnumerable<TResult>> definition,[CallerMemberName] string name = "") {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        return new(definition,name,this.ResolveSqlName(DatabaseObjectKind.TableFunction,name));
    }

    private Table<TRecord> CreateTable<TRecord>(string name) {
        var sqlName = this.ResolveSqlName(DatabaseObjectKind.Table,name);
        var store = global::Seamless.Tsavorite.Collections.Store.Create(
            IndexSize: this.Options.IndexSize,
            PageSize: this.Options.PageSize,
            SegmentSize: this.Options.SegmentSize,
            MemorySize: this.Options.MemorySize);
        if(this.Options.StorageMode==DatabaseStorageMode.Memory) {
            var memoryStore = store.Create<TRecord>();
            return new(memoryStore,memoryStore,name,sqlName);
        }
        var directory = Path.Combine(this.ResolveStorageRoot(),SanitizePathSegment(sqlName));
        Directory.CreateDirectory(directory);
        var logDevice = Devices.CreateLogDevice(Path.Combine(directory,"log"));
        var objectDevice = Devices.CreateLogDevice(Path.Combine(directory,"obj"));
        var storageStore = store.Create<TRecord>(logDevice,objectDevice);
        return new(storageStore,new StackedDisposable(storageStore,objectDevice,logDevice),name,sqlName);
    }

    private string ResolveStorageRoot() =>
        this.Options.StorageDirectory??Path.Combine(Environment.CurrentDirectory,this.DatabaseName);

    private string ResolveSqlName(DatabaseObjectKind kind,string memberName) {
        foreach(var declaration in this.DatabaseObjects) {
            if(declaration.Kind==kind&&declaration.MemberName==memberName) {
                return declaration.SqlName;
            }
        }
        return memberName;
    }

    private static string SanitizePathSegment(string value) {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach(var character in value) {
            if(Array.IndexOf(invalid,character)>=0) {
                builder.Append('_');
            } else {
                builder.Append(character);
            }
        }
        return builder.ToString();
    }

    public void Dispose() {
        lock(this.gate) {
            if(this.disposed) {
                return;
            }
            this.disposed=true;
            foreach(var table in this.tables.Values) {
                table.Dispose();
            }
            this.tables.Clear();
            this.views.Clear();
        }
        GC.SuppressFinalize(this);
    }

    private sealed class StackedDisposable(params IDisposable[] resources):IDisposable {
        public void Dispose() {
            foreach(var resource in resources) {
                resource.Dispose();
            }
        }
    }
}

namespace Seamless.Database;

/// <summary>
/// 宣言コンテナのテーブル実体。既定実体は Tsavorite HashSet で、
/// FLinq は基底型チェーン経由で Tsavorite ルートとして展開する。
/// 寿命はコンテナが管理し、Dispose でストアとデバイスも解放する。
/// </summary>
public sealed class Table<TRecord>:global::Seamless.Tsavorite.Collections.HashSet<TRecord> {
    internal Table(HashSetStore<TRecord> store,IDisposable ownedResources,string name,string sqlName) : base(store,ownedResources) {
        this.Name=name;
        this.SqlName=sqlName;
    }
    /// <summary>宣言プロパティ名。</summary>
    public string Name { get; }
    /// <summary>SQL Server 互換名。[Table]属性が無ければ宣言名。</summary>
    public string SqlName { get; }
}

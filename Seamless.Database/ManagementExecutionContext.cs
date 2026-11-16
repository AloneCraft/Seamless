namespace Seamless.Management.Server;

/// <summary>
/// Manager の登録クエリが受け取る実行の文脈。検証時は検証DBだけを束縛する。任意名のDB取得は公開しない。
/// 登録クエリを SDK でビルドするとき、参照が DB の部品だけで済むよう、Manager のサーバーの部品から移した(2026-11-16 の利用者の決定)。
/// 名前空間は元のまま。サーバーの部品には転送の印(TypeForwardedTo)があり、公開済みの登録クエリもそのまま動く。
/// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階7(3)。
/// </summary>
public sealed class ManagementExecutionContext{
    private readonly global::Seamless.Database.DatabaseContainer database;
    private readonly global::System.Func<global::System.Exception> mismatch;
    public ManagementExecutionContext(global::Seamless.Database.DatabaseContainer database,global::System.Threading.CancellationToken cancellationToken)
        :this(database,cancellationToken,static()=>new global::System.InvalidOperationException("DB型が実行環境と一致しません。")){}
    // Manager のサーバーは、型が合わないときに自分の例外(ManagementException の SchemaMismatch)を出す。
    internal ManagementExecutionContext(global::Seamless.Database.DatabaseContainer database,global::System.Threading.CancellationToken cancellationToken,global::System.Func<global::System.Exception> mismatch){
        this.database=database;this.CancellationToken=cancellationToken;this.mismatch=mismatch;
    }
    public global::System.Threading.CancellationToken CancellationToken{get;}
    public T GetDatabase<T>() where T:global::Seamless.Database.DatabaseContainer{
        this.CancellationToken.ThrowIfCancellationRequested();return this.database as T??throw this.mismatch();
    }
}

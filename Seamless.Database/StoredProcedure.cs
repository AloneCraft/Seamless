namespace Seamless.Database;

/// <summary>呼び出し時に実行済みとなるストアドプロシージャの結果。</summary>
public sealed class StoredProcedure<TResult> {
    internal StoredProcedure(TResult result,string name) {
        this.Result=result;
        this.Name=name;
    }

    public string Name { get; }
    public TResult Result { get; }
}

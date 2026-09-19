namespace Seamless.Database;

/// <summary>呼び出し時に評価済みとなるスカラー関数の結果。</summary>
public sealed class ScalarFunction<TResult> {
    internal ScalarFunction(TResult value,string name) {
        this.Value=value;
        this.Name=name;
    }

    public string Name { get; }
    public TResult Value { get; }
}

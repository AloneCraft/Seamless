namespace Benchmark.Dynamic;

public interface I動的UI設定 {
    T 取得<T>(string キー,T 既定値);
    void 保存<T>(string キー,T 値);
}

public sealed class Maui動的UI設定:I動的UI設定 {
    public T 取得<T>(string キー,T 既定値) {
        return Preferences.Default.Get(キー,既定値);
    }

    public void 保存<T>(string キー,T 値) {
        Preferences.Default.Set(キー,値);
    }
}

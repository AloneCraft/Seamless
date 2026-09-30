namespace Seamless.Execution;
/// <summary>ストアの寿命の種類。</summary>
public enum StoreKind{
    /// <summary>一時フォルダを使い、破棄で全削除する。</summary>
    Temporary,
    /// <summary>指定フォルダを使い、<see cref="Store.Save"/> した状態を次回も開き直せる。</summary>
    Persistent,
    /// <summary>ディスクを使わない。上限を超えたら例外にする。</summary>
    Memory,
}
/// <summary>ストア全体が使ってよいメモリとディスクの上限。既定値は <see cref="ExecutionOptions"/> と同じ。</summary>
public sealed record StoreLimits{
    public long MemoryBytes{get;init;}=256L*1024*1024;
    public long DiskBytes{get;init;}=16L*1024*1024*1024;
}
/// <summary>
/// ストアに登録するエンジン側の子資源(Tsavorite や Persisted のデータ本体)。
/// ストアは保存時に <see cref="Save"/> が返した状態文字列を保存番号ごとに記録し、復元時に <see cref="Load"/> へ渡す。
/// 状態文字列の中身はエンジンが決める。
/// </summary>
public interface IStoreComponent:IDisposable{
    string Name{get;}
    string Save();
    /// <summary>
    /// <see cref="Save"/> が書いた内容を、電源断でも残るようディスクへ確実に書き出す。
    /// ストアは永続ストアの保存で、全子資源の <see cref="Save"/> が終わってからマニフェストを書く前に 1 回呼ぶ
    /// (マニフェストが指す先のデータを先に確実にするため。全子資源の書き出しがまとまり、OS の書き出しも並行して進む)。
    /// <see cref="Save"/> の中で既に確実に書き出している子資源は何もしなくてよい。
    /// </summary>
    void Flush();
    void Load(string state);
}

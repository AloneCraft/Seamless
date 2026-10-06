namespace Seamless.Collections.Persisted;
/// <summary>メモリストア(ディスクを使わない共通ストア)で Persisted のコレクションを作るときの置き場所。</summary>
public enum PersistedMemoryBacking{
    /// <summary>メモリ上の仮想ファイル。ファイルを作らず、ストアのメモリ予算で確認する。速い。</summary>
    InMemory,
    /// <summary>一時フォルダのファイル。コレクションの破棄で削除する。ストアのディスク予算で確認する。メモリに載らない大きさを扱える。</summary>
    TemporaryFolder,
}

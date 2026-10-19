namespace Sample.FLinqChainVerifier.Core;

/// <summary>検証の4段階。</summary>
public enum 検証段階{
    ペア網羅=0,
    代表チェーン=1,
    全組み合わせ自動生成=2,
    ランダム無限探索=3,
}

/// <summary>起点となる入力ソースの種別。</summary>
public enum 入力ソース種別{
    Array=0,
    List=1,
    IEnumerable=2,
    Range=3,
    Persisted=4,
    Tsavorite=5,
}

/// <summary>LINQの6つの基本プリミティブ分類。</summary>
public enum メソッド分類{
    フィルタ射影=0,
    平坦化結合=1,
    状態ストリーム=2,
    グループ集約=3,
    順序変更=4,
    終端スカラー=5,
    終端コレクション=6,
}

/// <summary>棒グラフの色種別。</summary>
public enum 棒の色{
    通常=0,
    強調=1,
    比較=2,
    理想=3,
    警告=4,
}

/// <summary>ログの種別。</summary>
public enum ログ種別{
    通常=0,
    成功=1,
    警告=2,
    失敗=3,
}

/// <summary>棒グラフの1本分のデータ。</summary>
public readonly record struct 棒グラフ項目(string ラベル,double 値,棒の色 色);

/// <summary>棒グラフ1つ分の報告データ。</summary>
public sealed record 棒グラフ報告(string 題名,string 単位,IReadOnlyList<棒グラフ項目>棒一覧);

/// <summary>隣接2メソッドペア(A.B, B.C, C.D)の遷移・融合特性。</summary>
public sealed record ペア遷移特性(
    string 前段メソッド名,
    string 後段メソッド名,
    string 融合パターン名,
    int 期待ループ増分,
    long 期待最大追加バイト数Per要素,
    bool 完全インライン融合か,
    string 理想構造説明,
    string 未融合時の原因候補,
    string 改善案);

/// <summary>チェーン全体の推定理想特性(2メソッドペアの組み合わせから合成)。</summary>
public sealed record 推定理想特性(
    int 期待ループ本数,
    long 期待最大確保バイト数,
    IReadOnlyList<ペア遷移特性>ペア遷移一覧,
    string 推定理想構造コード,
    string 特性要約);

/// <summary>検証対象の1つのメソッドチェーン仕様。</summary>
public sealed record チェーン検証項目(
    string 識別子,
    検証段階 段階,
    入力ソース種別 ソース種別,
    ulong シード値,
    int 試行番号,
    IReadOnlyList<string>メソッド名一覧,
    string クエリ表示式,
    string クエリ本体コード,
    string? 手書き理想コード,
    推定理想特性 推定特性);

/// <summary>1つのメソッドチェーンを実行・検証した結果。</summary>
public sealed record チェーン検証結果(
    チェーン検証項目 項目,
    bool コンパイル成功か,
    bool 全メソッド展開済みか,
    int 実行時フォールバック数,
    bool 全データ結果一致か,
    bool パフォーマンス合格か,
    bool 理想構造一致か,
    int 実測ループ本数,
    double System時間マイクロ秒,
    double 非最適化FLinq時間マイクロ秒,
    double 最適化FLinq時間マイクロ秒,
    double 理想または推定時間マイクロ秒,
    long System確保バイト数,
    long 非最適化FLinq確保バイト数,
    long 最適化FLinq確保バイト数,
    long 理想または推定確保バイト数,
    string 展開コード抜粋,
    string 結果要約,
    string 原因診断,
    string 改善案,
    string 再現テストコード,
    string? 保存先ファイルパス){
    public bool 完全合格か{
        get{
            if(this.コンパイル成功か&&this.全メソッド展開済みか&&this.実行時フォールバック数==0&&this.全データ結果一致か&&this.パフォーマンス合格か){
                return true;
            }else{
                return false;
            }
        }
    }
}

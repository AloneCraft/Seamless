using System.Text;
using System.Text.RegularExpressions;

namespace Sample.FLinqChainVerifier.Core;

/// <summary>
/// 2メソッドペア(A.B, B.C, C.D)の組み合わせ特性からチェーン全体の理想特性を合成推定し、
/// 実測結果および生成コードと比較して原因と改善案を自動診断する。
/// </summary>
public static class 理想特性推定と原因診断器{
    private static readonly Regex ループ検出正規表現=new(@"\b(for|while|foreach)\s*\(",RegexOptions.Compiled);

    /// <summary>
    /// メソッド名一覧(A, B, C, D...)から、隣接ペア(A.B, B.C, C.D...)の特性を合成してチェーン全体の理想特性を推定する。
    /// </summary>
    public static 推定理想特性 理想特性を推定(入力ソース種別 ソース種別,IReadOnlyList<string>メソッド名一覧,int 標準要素数=512){
        var ペア一覧=new List<ペア遷移特性>();
        var 期待ループ数=1;
        var 要素あたり追加バイト=0L;

        // Range + Sum / Count などの閉形式(O(1)ループ0本)判定
        if(ソース種別==入力ソース種別.Range&&メソッド名一覧.Count==1&&(メソッド名一覧[0]=="Count"||メソッド名一覧[0]=="LongCount")){
            期待ループ数=0;
        }else if((ソース種別==入力ソース種別.Array||ソース種別==入力ソース種別.List)&&メソッド名一覧.Count==1&&(メソッド名一覧[0]=="Count"||メソッド名一覧[0]=="LongCount"||メソッド名一覧[0]=="Any"||メソッド名一覧[0]=="First"||メソッド名一覧[0]=="FirstOrDefault"||メソッド名一覧[0]=="Last"||メソッド名一覧[0]=="LastOrDefault"||メソッド名一覧[0]=="ElementAt"||メソッド名一覧[0]=="ElementAtOrDefault")){
            期待ループ数=0;
        }

        for(var 番号=0;番号<メソッド名一覧.Count-1;番号++){
            var ペア=拡張メソッドカタログ.ペア遷移を取得(メソッド名一覧[番号],メソッド名一覧[番号+1]);
            ペア一覧.Add(ペア);
            期待ループ数+=ペア.期待ループ増分;
            要素あたり追加バイト+=ペア.期待最大追加バイト数Per要素;
        }

        // 終端がコレクション生成の場合は出力コレクション分のメモリを加算
        var 末尾名=メソッド名一覧[^1];
        var 末尾記述子=拡張メソッドカタログ.取得(末尾名);
        var 基礎確保バイト=0L;
        if(末尾記述子.分類==メソッド分類.終端コレクション){
            var 実効要素数 = (long)標準要素数;
            if(メソッド名一覧.Contains("SelectMany")){
                実効要素数=標準要素数*16L;
            }
            基礎確保バイト=実効要素数*16L+128L;
        }else if(末尾記述子.分類==メソッド分類.終端スカラー&&期待ループ数==1&&要素あたり追加バイト==0){
            if(ソース種別==入力ソース種別.Array||ソース種別==入力ソース種別.List||ソース種別==入力ソース種別.Range){
                基礎確保バイト=0L;
            }else{
                基礎確保バイト=128L;
            }
        }
        var 期待最大バイト=基礎確保バイト+要素あたり追加バイト*標準要素数;

        var 擬似コード=推定理想構造コードを生成(ソース種別,メソッド名一覧,ペア一覧,期待ループ数);
        var 要約Builder=new StringBuilder();
        要約Builder.Append($"期待ループ数: {期待ループ数}本 / 期待確保上限: {期待最大バイト:N0} B");
        if(ペア一覧.Count>0){
            要約Builder.Append(" / ペア合成: ");
            for(var 番号=0;番号<ペア一覧.Count;番号++){
                if(番号>0){
                    要約Builder.Append(" + ");
                }
                要約Builder.Append($"[{ペア一覧[番号].前段メソッド名}.{ペア一覧[番号].後段メソッド名}: {ペア一覧[番号].融合パターン名}]");
            }
        }

        return new 推定理想特性(
            期待ループ数,
            期待最大バイト,
            ペア一覧,
            擬似コード,
            要約Builder.ToString());
    }

    private static string 推定理想構造コードを生成(
        入力ソース種別 ソース種別,
        IReadOnlyList<string>メソッド名一覧,
        IReadOnlyList<ペア遷移特性>ペア一覧,
        int 期待ループ数){
        var ビルダー=new StringBuilder();
        ビルダー.AppendLine($"// [2メソッドペア合成による推定理想構造] (期待ループ本数: {期待ループ数}本)");
        for(var 番号=0;番号<ペア一覧.Count;番号++){
            var ペア=ペア一覧[番号];
            ビルダー.AppendLine($"// ペア{番号+1} ({ペア.前段メソッド名} -> {ペア.後段メソッド名}): {ペア.融合パターン名}");
            ビルダー.AppendLine($"//   => {ペア.理想構造説明}");
        }
        ビルダー.AppendLine();
        if(期待ループ数==0){
            ビルダー.AppendLine("// O(1) 直接インデックスまたは算術閉形式 (ループ走査なし)");
            ビルダー.AppendLine("return DirectClosedFormOrIndexAccess(source);");
            return ビルダー.ToString();
        }

        var フェーズ番号=1;
        ビルダー.AppendLine($"// --- フェーズ {フェーズ番号}: {ソース種別} 直接走査ループ ---");
        if(ソース種別==入力ソース種別.Array||ソース種別==入力ソース種別.List){
            ビルダー.AppendLine("ReadOnlySpan<int> span = AsSpan(source);");
            ビルダー.AppendLine("for(int i=0;i<span.Length;i++){");
            ビルダー.AppendLine("    int x = span[i];");
        }else if(ソース種別==入力ソース種別.Range){
            ビルダー.AppendLine("for(int i=0;i<count;i++){");
            ビルダー.AppendLine("    int x = start+i;");
        }else{
            ビルダー.AppendLine("foreach(int x in source){");
        }

        for(var 番号=0;番号<メソッド名一覧.Count;番号++){
            var 記述子=拡張メソッドカタログ.取得(メソッド名一覧[番号]);
            if(番号>0&&ペア一覧[番号-1].期待ループ増分>0){
                ビルダー.AppendLine("}");
                フェーズ番号++;
                ビルダー.AppendLine($"// --- フェーズ {フェーズ番号}: バリア確定後の継続走査ループ ({ペア一覧[番号-1].融合パターン名}) ---");
                ビルダー.AppendLine("for(int j=0;j<phaseBufferCount;j++){");
            }
            ビルダー.AppendLine($"    // [{記述子.メソッド名}] {記述子.理想処理説明}");
        }
        ビルダー.AppendLine("}");
        return ビルダー.ToString();
    }

    /// <summary>生成されたインターセプトメソッド本体内のループ数を数える。</summary>
    public static int ループ本数を数える(string 展開コード抜粋){
        if(string.IsNullOrWhiteSpace(展開コード抜粋)){
            return 0;
        }
        return ループ検出正規表現.Matches(展開コード抜粋).Count;
    }

    /// <summary>
    /// 展開コードと実行計測結果をペア知識ベースと照合し、原因と改善案を診断する。
    /// </summary>
    public static (string 原因診断,string 改善案,bool 理想構造一致か) 診断する(
        チェーン検証項目 項目,
        bool コンパイル成功か,
        bool 全メソッド展開済みか,
        int 実行時フォールバック数,
        bool 全データ結果一致か,
        bool パフォーマンス合格か,
        int 実測ループ本数,
        double System時間マイクロ秒,
        double 非最適化FLinq時間マイクロ秒,
        double 最適化FLinq時間マイクロ秒,
        long System確保バイト数,
        long 最適化FLinq確保バイト数,
        string 展開コード抜粋,
        string 診断詳細){
        var 原因Builder=new StringBuilder();
        var 改善Builder=new StringBuilder();
        var 構造一致=true;

        if(!コンパイル成功か){
            構造一致=false;
            原因Builder.AppendLine($"[コンパイル失敗] 生成されたインターセプトコードでコンパイルエラーが発生しました: {診断詳細}");
            改善Builder.AppendLine("PipelineEnumerable の変数スコープ(重複宣言や未定義変数)および型推論を確認し、StageLoops.cs のコード生成テンプレートを修正してください。");
            return (原因Builder.ToString().Trim(),改善Builder.ToString().Trim(),構造一致);
        }

        if(!全メソッド展開済みか||実行時フォールバック数>0){
            構造一致=false;
            原因Builder.AppendLine($"[未展開・フォールバック検出] 実行時フォールバック数={実行時フォールバック数}。生成コード内に未展開の System.Linq 呼び出しまたは Fallback マーカーが残っています。");
            foreach(var ペア in 項目.推定特性.ペア遷移一覧){
                原因Builder.AppendLine($"- ペア ({ペア.前段メソッド名} -> {ペア.後段メソッド名}): {ペア.未融合時の原因候補}");
                改善Builder.AppendLine($"- [{ペア.前段メソッド名}.{ペア.後段メソッド名}] {ペア.改善案}");
            }
            if(項目.推定特性.ペア遷移一覧.Count==0){
                改善Builder.AppendLine($"- 演算子 {項目.メソッド名一覧[0]} の OperatorRegistry 登録と PipelineBuilder.Validate.cs の検証条件を確認してください。");
            }
            return (原因Builder.ToString().Trim(),改善Builder.ToString().Trim(),構造一致);
        }

        if(!全データ結果一致か){
            構造一致=false;
            原因Builder.AppendLine($"[結果不一致] System.Linq と FLinq の出力結果(または空入力時の例外挙動)が一致しません: {診断詳細}");
            foreach(var ペア in 項目.推定特性.ペア遷移一覧){
                原因Builder.AppendLine($"- 確認対象ペア ({ペア.前段メソッド名} -> {ペア.後段メソッド名}): 境界条件(0件・重複キー・順序・短絡break位置)の不整合の疑い。");
            }
            改善Builder.AppendLine("空シーケンス時の初期状態、Skip/Takeのインデックス計算、およびOrderBy/GroupByの比較器・挿入順保持ロジックを修正してください。");
            return (原因Builder.ToString().Trim(),改善Builder.ToString().Trim(),構造一致);
        }

        // 生成コードの構造検査(TryGetNextの残存、ToLookupの残存など)
        if(展開コード抜粋.Contains("TryGetNext",StringComparison.Ordinal)){
            構造一致=false;
            原因Builder.AppendLine("[列挙子構造体ラッパー残存] 生成コード内に TryGetNext ステートマシン呼び出しが残っており、完全な直接ループへ平坦化されていません。");
            foreach(var ペア in 項目.推定特性.ペア遷移一覧){
                if(ペア.完全インライン融合か){
                    原因Builder.AppendLine($"- ペア ({ペア.前段メソッド名} -> {ペア.後段メソッド名}): {ペア.未融合時の原因候補}");
                    改善Builder.AppendLine($"- [{ペア.前段メソッド名}.{ペア.後段メソッド名}] {ペア.改善案}");
                }
            }
        }

        if(項目.メソッド名一覧.Any(static m=>m.StartsWith("GroupBy_",StringComparison.Ordinal))&&展開コード抜粋.Contains("ToLookup",StringComparison.Ordinal)){
            構造一致=false;
            原因Builder.AppendLine("[GroupBy 1パス融合漏れ] GroupBy で中間 ToLookup(要素リスト構築)が呼ばれており、DictionarySlim への 1パス直接集計融合が効いていません。");
            改善Builder.AppendLine("- GroupByAggregateFusion.cs の TryCreate パターン認識を拡張し、後続のステージ構成でも 1パス集計が維持されるようにしてください。");
        }

        if(!パフォーマンス合格か){
            if(最適化FLinq確保バイト数>System確保バイト数&&System確保バイト数>0){
                構造一致=false;
                原因Builder.AppendLine($"[メモリ割り当て増加] FLinq ({最適化FLinq確保バイト数:N0} B) が System.Linq ({System確保バイト数:N0} B) より多くメモリを確保しています。");
                改善Builder.AppendLine("- 中間バッファに List<T> や新規配列を使わず、ArrayPool<T>.Shared または DictionarySlim / ValueTuple スロットを使用してください。");
            }
            if(最適化FLinq時間マイクロ秒>System時間マイクロ秒*1.15&&最適化FLinq時間マイクロ秒>非最適化FLinq時間マイクロ秒*1.15){
                原因Builder.AppendLine($"[実行速度逆転] 最適化FLinq ({最適化FLinq時間マイクロ秒:F1} μs) が System.Linq ({System時間マイクロ秒:F1} μs) / 非最適化 ({非最適化FLinq時間マイクロ秒:F1} μs) より遅くなっています。");
                foreach(var ペア in 項目.推定特性.ペア遷移一覧){
                    原因Builder.AppendLine($"- ペア境界 ({ペア.前段メソッド名} -> {ペア.後段メソッド名}): {ペア.未融合時の原因候補}");
                    改善Builder.AppendLine($"- [{ペア.前段メソッド名}.{ペア.後段メソッド名}] {ペア.改善案}");
                }
            }
        }

        if(原因Builder.Length==0){
            原因Builder.Append("問題なし: 全メソッドがフォールバックなしでインライン展開され、2メソッドペア合成から推定される理想ループ特性と一致しています。");
            改善Builder.Append("対応不要 (理想展開済み)");
        }else if(改善Builder.Length==0){
            改善Builder.Append("生成コード内のループ境界とバッファ確保箇所を確認し、隣接ペア間の直接転送パスを追加してください。");
        }

        return (原因Builder.ToString().Trim(),改善Builder.ToString().Trim(),構造一致);
    }
}

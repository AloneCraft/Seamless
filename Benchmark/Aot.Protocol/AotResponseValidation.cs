namespace Benchmark.Aot.Protocol;

public static class AOT応答検証 {
    public static void 検証(
        AOTワーカー応答 応答,
        string 要求指紋,
        string ツールチェーン指紋,
        int 論理CPU,
        IReadOnlyList<AOTケース要求> ケース一覧) {
        ArgumentNullException.ThrowIfNull(応答);
        ArgumentNullException.ThrowIfNull(ケース一覧);
        if(応答.契約バージョン!=AOT契約.現在バージョン) {
            throw new InvalidDataException("AOT応答の契約バージョンが一致しません。");
        }
        if(!string.Equals(応答.状態,"Completed",StringComparison.Ordinal)) {
            throw new InvalidDataException("AOT応答が完了状態ではありません: "+応答.失敗理由);
        }
        if(!応答.NativeAOTか) {
            throw new InvalidDataException("NativeAOT以外の応答を拒否しました。");
        }
        if(!応答.アフィニティ指定成功か||応答.論理CPU!=論理CPU) {
            throw new InvalidDataException($"AOT応答のCPUまたはaffinityが一致しません。期待={論理CPU} 実際={応答.論理CPU}");
        }
        if(!string.Equals(応答.要求指紋,要求指紋,StringComparison.Ordinal)
            ||!string.Equals(応答.ツールチェーン指紋,ツールチェーン指紋,StringComparison.Ordinal)) {
            throw new InvalidDataException("AOT応答の再開指紋が一致しません。");
        }
        if(応答.結果一覧.Count!=ケース一覧.Count) {
            throw new InvalidDataException($"AOT結果件数が一致しません。期待={ケース一覧.Count} 実際={応答.結果一覧.Count}");
        }
        var 期待キー一覧=ケース一覧.Select(static ケース=>(ケース.メソッド名,ケース.引数キー)).ToHashSet();
        var 結果キー一覧=応答.結果一覧.Select(static 結果=>(結果.メソッド名,結果.引数キー)).ToArray();
        if(結果キー一覧.Length!=結果キー一覧.Distinct().Count()) {
            throw new InvalidDataException("AOT応答に重複ケースがあります。");
        }
        if(!期待キー一覧.SetEquals(結果キー一覧)) {
            throw new InvalidDataException("AOT応答に欠落または未要求ケースがあります。");
        }
        foreach(var 結果 in 応答.結果一覧) {
            if(!string.Equals(結果.状態,"Completed",StringComparison.Ordinal)
                ||結果.平均ナノ秒 is not { } 平均
                ||結果.誤差ナノ秒 is not { } 誤差
                ||結果.標準偏差ナノ秒 is not { } 標準偏差
                ||!double.IsFinite(平均)
                ||!double.IsFinite(誤差)
                ||!double.IsFinite(標準偏差)
                ||結果.割り当てバイト数 is not { } 割り当てバイト数
                ||割り当てバイト数<0
                ||string.IsNullOrWhiteSpace(結果.結果ダイジェスト)) {
                throw new InvalidDataException($"AOT結果が不完全です: {結果.メソッド名}({結果.引数キー})");
            }
        }
    }

    public static bool 有効か(
        AOTワーカー応答 応答,
        string 要求指紋,
        string ツールチェーン指紋,
        int 論理CPU,
        IReadOnlyList<AOTケース要求> ケース一覧) {
        try {
            検証(応答,要求指紋,ツールチェーン指紋,論理CPU,ケース一覧);
            return true;
        } catch(InvalidDataException) {
            return false;
        }
    }
}

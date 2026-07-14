namespace Benchmark.Tsavorite.ParameterSweep;

public static class パラメータスイープ比較器{
    public static IReadOnlyList<パラメータスイープ比較行> 比較する(
        IReadOnlyList<パラメータスイープ結果> 今回一覧,
        IReadOnlyList<パラメータスイープ結果> 前回一覧){
        var 前回ケース別=前回一覧.ToDictionary(static 項目=>項目.ケース名,StringComparer.OrdinalIgnoreCase);
        return 今回一覧
            .Select(項目=>行を作成(項目,前回ケース別.GetValueOrDefault(項目.ケース名)))
            .ToArray();
    }

    private static パラメータスイープ比較行 行を作成(パラメータスイープ結果 今回,パラメータスイープ結果? 前回){
        if(前回 is null){
            return new(今回,null,null,null,null,null,null,false);
        }
        var 追加時間シェア=シェアを作成(今回.追加経過ミリ秒,前回.追加経過ミリ秒);
        var 検索時間シェア=シェアを作成(今回.検索平均ナノ秒,前回.検索平均ナノ秒);
        return new(
            今回,
            前回,
            今回.最大成功Count-前回.最大成功Count,
            追加時間シェア.今回,
            追加時間シェア.前回,
            検索時間シェア.今回,
            検索時間シェア.前回,
            今回.失敗種別!=前回.失敗種別);
    }

    private static (double? 今回,double? 前回)シェアを作成(double? 今回,double? 前回){
        if(今回 is not { } 今回値||前回 is not { } 前回値
            ||!double.IsFinite(今回値)||!double.IsFinite(前回値)||今回値<0||前回値<0){
            return (null,null);
        }
        if(今回値==0&&前回値==0){
            return (0,0);
        }
        var 合計=今回値+前回値;
        return (今回値/合計,前回値/合計);
    }
}

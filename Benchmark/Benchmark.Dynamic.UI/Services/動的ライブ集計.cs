namespace Benchmark.Dynamic.UI;

public static class 動的ライブ集計{
    public static double? 比率(double 基準,double 比較,bool メモリか){
        if(!double.IsFinite(基準)||!double.IsFinite(比較)){return null;}
        if(メモリか){
            if(基準<0||比較<0){return null;}
            return 比較>0?基準/比較:基準==0?1:double.PositiveInfinity;
        }
        if(基準<=0||比較<=0){return null;}
        var 倍率=基準/比較;
        return 倍率>0&&double.IsFinite(倍率)?倍率:null;
    }

    public static double 速度の幾何平均(IEnumerable<(動的ライブ対象 対象,double 比率)> 有効値){
        return Math.Exp(有効値.GroupBy(static 行=>行.対象)
            .Average(static 組=>組.Average(static 行=>Math.Log(行.比率))));
    }
}

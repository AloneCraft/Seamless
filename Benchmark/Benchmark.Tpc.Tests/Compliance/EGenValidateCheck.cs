namespace Benchmark.TpcE.Compliance.Tests;
public sealed class EGenValidateCheck{
    [Fact,Trait("Category","Unit")]public void StandardDeviationRoundsUpToThreeDigits(){
        {
            //標準偏差 = sqrt(Σ(平均 − n_i)² / (負荷単位の数 − 1))。小数第 3 位まで、切り上げ(仕様 6.7.4.2)
            Assert.Equal(0,Compliance.EGenValidateCheck.StandardDeviation([100]));
            Assert.Equal(0,Compliance.EGenValidateCheck.StandardDeviation([100,100,100]));
            // 100, 102 → 平均 101、Σ = 2、/(2−1) = 2、√2 = 1.41421… → 1.415
            Assert.Equal(1.415,Compliance.EGenValidateCheck.StandardDeviation([100,102]));
            // 1, 2, 3 → 平均 2、Σ = 2、/2 = 1、√1 = 1 ちょうどは増やさない
            Assert.Equal(1.0,Compliance.EGenValidateCheck.StandardDeviation([1,2,3]));
        }
    }
    [Fact,Trait("Category","Unit")]public void NeedsTwoLoadUnits(){
        {
            //負荷単位が 1 つだと標準偏差を求められない(顧客 1,000 人の実行)。失敗ではなく判定できない扱い
            var result=Compliance.DriverResult.Parse("""{"customers":1000,"load_unit_trade_results":[500]}""");
            var check=Compliance.EGenValidateCheck.Run(result,Path.GetTempPath(),TimeSpan.FromSeconds(1));
            Assert.Equal(CheckOutcome.NotExercised,check.Outcome);
        }
    }
}

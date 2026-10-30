namespace Benchmark.TpcE.Compliance.Tests;
public sealed class EGenLogChecks{
    [Fact,Trait("Category","Unit")]public void FlagsNonDefaultSettings(){
        {
            //EGen のログの『既定か』の欄が NO の行(例: 初期取引の日数が 300 でない)を不合格として挙げる
            var check=Compliance.EGenLogChecks.Scan([
                "EGenDriverAll (EGen v1.14.0) 0 2026-10-23 01:34:51.553 CE object constructed using constructor 1 (valid for publication: YES).",
                "Scale Factor:                           500\t500\tYES",
                "Days of Initial Trades:                 300\t1\tNO",
            ]);
            Assert.Equal(CheckOutcome.Fail,check.Outcome);
            Assert.Contains("Days of Initial Trades",check.Detail);
        }
    }
    [Fact,Trait("Category","Unit")]public void FlagsObjectsInvalidForPublication(){
        {
            var check=Compliance.EGenLogChecks.Scan([
                "EGenDriverAll (EGen v1.14.0) 0 2026-10-23 01:34:51.551 MEE object constructed using c'tor 3 (valid for publication: NO).",
            ]);
            Assert.Equal(CheckOutcome.Fail,check.Outcome);
            Assert.Contains("MEE",check.Detail);
        }
    }
    [Fact,Trait("Category","Unit")]public void AcceptsAnAllDefaultLog(){
        {
            var check=Compliance.EGenLogChecks.Scan([
                "EGenDriverAll (EGen v1.14.0) 0 2026-10-23 01:34:51.553 CE object constructed using constructor 1 (valid for publication: YES).",
                "Scale Factor:                           500\t500\tYES",
                "Days of Initial Trades:                 300\t300\tYES",
            ]);
            Assert.Equal(CheckOutcome.Pass,check.Outcome);
        }
    }
}

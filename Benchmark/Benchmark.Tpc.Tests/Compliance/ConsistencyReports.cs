namespace Benchmark.TpcE.Compliance.Tests;
public sealed class ConsistencyReports{
    [Fact,Trait("Category","Unit")]public void ReadsStartupLines(){
        {
            //SUT の起動時のログ行から、条項・合否・内容を取り出す
            var checks=Compliance.ConsistencyReports.FromStartupLines([
                "[sut] loaded 1,482,326 rows in 5.4 s",
                "[sut] consistency after-load 7.3.2.1 PASS checked=10 violations=0 ",
                "[sut] consistency after-load 7.3.2.2 FAIL checked=10 violations=2 ブローカー 1: 記録 1 実際 0; ",
                "[sut] consistency checked in 0.3 s",
            ]);
            Assert.Equal(2,checks.Count);
            Assert.Equal("7.3.2.1",checks[0].Clause);
            Assert.Equal(CheckOutcome.Pass,checks[0].Outcome);
            Assert.Contains("初期投入後",checks[0].Name);
            Assert.Equal(CheckOutcome.Fail,checks[1].Outcome);
            Assert.Contains("ブローカー 1",checks[1].Detail);
        }
    }
    [Fact,Trait("Category","Unit")]public void ReadsAdminResponse(){
        {
            //管理要求の応答の results を点検項目にする
            var checks=Compliance.ConsistencyReports.FromAdminResponse("""
                {"ok":true,"admin":"consistency","seq":5,"results":[
                  {"clause":"7.3.2.1","name":"x","checked":10,"violations":0,"passed":true,"detail":""},
                  {"clause":"7.3.2.3","name":"y","checked":7,"violations":1,"passed":false,"detail":"口座 5"}]}
                ""","計測後");
            Assert.Equal(2,checks.Count);
            Assert.Equal(CheckOutcome.Pass,checks[0].Outcome);
            Assert.Equal(CheckOutcome.Fail,checks[1].Outcome);
            Assert.Contains("計測後",checks[1].Name);
        }
    }
}

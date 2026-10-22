namespace Benchmark.TpcE.Compliance.Tests;
public sealed class AuditReport{
    [Fact,Trait("Category","Unit")]public void ListsEveryAuditSection(){
        {
            //10.2〜10.9 の各節に項目があり、条項と要件は空でなく、条項が重複しない
            var items=Compliance.AuditReport.Items;
            foreach(var section in new[]{"10.2","10.3","10.4","10.5","10.6","10.7","10.8","10.9"}){
                Assert.Contains(items,item=>item.Clause.StartsWith(section,StringComparison.Ordinal));
            }
            Assert.All(items,static item=>{
                Assert.False(string.IsNullOrWhiteSpace(item.Clause));
                Assert.False(string.IsNullOrWhiteSpace(item.Requirement));
                Assert.False(string.IsNullOrWhiteSpace(item.Note));
            });
            Assert.Equal(items.Count,items.Select(static item=>item.Clause).Distinct().Count());
        }
    }
    [Fact,Trait("Category","Unit")]public void DisclosesKnownGaps(){
        {
            //公式化を妨げる既知の隔たりを、未達として隠さず出している
            var gaps=Compliance.AuditReport.Items.Where(static item=>item.Status==AuditStatus.Gap).Select(static item=>item.Clause).ToArray();
            Assert.Contains("10.5.Harness",gaps);
            Assert.Contains("10.7.4",gaps);
            Assert.Contains("10.2.1.7",gaps);
            Assert.Contains("10.4.7",gaps);
            var markdown=Compliance.AuditReport.Markdown();
            Assert.Contains(Program.Notice,markdown);
            Assert.Contains("既知の隔たり",markdown);
        }
    }
}

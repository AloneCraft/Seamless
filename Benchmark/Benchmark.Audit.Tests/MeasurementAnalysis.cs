using Target = Benchmark.Audit;

namespace Benchmark.Audit.Tests;

/// <summary>同じビルドを2回測った結果から、ぶれと理想比で行を分類する処理の検査。</summary>
public sealed class MeasurementAnalysis {
    private static string Line(string method,double flinq,double ideal,double baseline,bool matched = true,string state = "安定") {
        string Implementation(string key,double nanoseconds) =>
            "{\"Key\":\""+key+"\",\"WarmupState\":\""+state+"\",\"Measurement\":{\"中央値ナノ秒\":"+nanoseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +",\"割当バイト数\":0,\"ナノ秒サンプル\":["+string.Join(",",Enumerable.Repeat(nanoseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),9))+"]}}";
        var digest = "false";
        if(matched) {
            digest="true";
        }
        return "{\"Tab\":\"System.Array\",\"Type\":\"T\",\"Method\":\""+method+"\",\"ArgumentKey\":\"\",\"Result\":{\"DigestMatched\":"+digest+",\"Implementations\":["
            +Implementation("FLinq",flinq)+","+Implementation("Baseline",baseline)+","+Implementation("Ideal",ideal)+"]}}";
    }

    private static IReadOnlyList<Target.MeasuredCase> Cases(params string[] lines) {
        var path = Path.GetTempFileName();
        try {
            File.WriteAllLines(path,lines);
            return Target.MeasurementAnalysis.Read(path);
        } finally {
            File.Delete(path);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void WithinRunSpread() {
        {
            //すべて同じ値ならぶれは0
            Assert.Equal(0,Target.MeasurementAnalysis.WithinRunSpread([10,10,10,10,10,10,10,10,10]));
        }
        {
            //四分位範囲/中央値の半分
            Assert.Equal((12d-8d)/10d/2d,Target.MeasurementAnalysis.WithinRunSpread([6,8,8,9,10,11,12,12,14]),6);
        }
        {
            //標本が少なすぎると0
            Assert.Equal(0,Target.MeasurementAnalysis.WithinRunSpread([1,2,3]));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Classify() {
        var first = Cases(
            Line("Slow",150,100,200),
            Line("Small",102,100,200),
            Line("Fast",80,100,200),
            Line("Same",100,100,200),
            Line("Broken",100,100,200,matched: false));
        var second = Cases(
            Line("Slow",152,100,200),
            Line("Small",102.5,100,200),
            Line("Fast",79,100,200),
            Line("Same",101,100,200),
            Line("Broken",100,100,200,matched: false));
        var rows = Target.MeasurementAnalysis.Classify(first,second).ToDictionary(static row => row.Method);
        {
            //2回ともぶれを超えて理想より遅く、3%以上なら改善対象
            Assert.Equal(Target.GapClass.改善対象,rows["Slow"].Class);
        }
        {
            //ぶれは超えるが3%未満なら記録だけ
            Assert.Equal(Target.GapClass.小さい差,rows["Small"].Class);
        }
        {
            //2回とも理想より速ければ理想版の見直し対象
            Assert.Equal(Target.GapClass.理想より速い,rows["Fast"].Class);
        }
        {
            //ぶれの範囲内なら同程度
            Assert.Equal(Target.GapClass.同程度,rows["Same"].Class);
        }
        {
            //ダイジェストが一致しない行は判定しない
            Assert.Equal(Target.GapClass.判定不能,rows["Broken"].Class);
        }
    }
}

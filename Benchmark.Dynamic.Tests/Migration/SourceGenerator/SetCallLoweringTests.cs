using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Seamless.Linq.Set の呼び出し(ビュー・関数の本体の書き方)を、FLinq が System.Linq の同じ演算へ下げて展開すること。
/// Set の Select・SelectMany・Join は重複を除くので、展開しても結果は Set のままと同じになる。
/// </summary>
[LinqIntercept(false)]
public sealed class SetCallLoweringTests {
    private const string Source = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        using Seamless.Linq;
        namespace Sample;
        public sealed class Rows:Seamless.Collections.ISet<int>{
            private readonly HashSet<int> items;
            public Rows(params int[] values){this.items=new HashSet<int>(values);}
            public int Count=>this.items.Count;
            public bool Contains(int item)=>this.items.Contains(item);
            public IEnumerator<int> GetEnumerator()=>this.items.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
        }
        public sealed class Probe{
            [LinqIntercept(true)]
            private static int[] Remainders(Rows rows)=>rows.Where(x=>x>1).Select(x=>x%3).ToArray();
            [LinqIntercept(true)]
            private static int CountRemainders(Rows rows)=>rows.Select(x=>x%3).Count();
            [LinqIntercept(true)]
            private static bool HasRemainder(Rows rows,int value)=>rows.Select(x=>x%3).Contains(value);
            [LinqIntercept(true)]
            private static int[] Pairs(Rows rows,Rows other)=>rows.SelectMany(x=>other.Select(y=>(x+y)%2)).ToArray();
            [LinqIntercept(true)]
            private static int[] Common(Rows rows,Rows other)=>rows.Intersect(other).Where(x=>x>2).ToArray();
            public static string Run(){
                var rows=new Rows(1,2,3,4,5,6);
                var other=new Rows(4,5,6,7);
                var remainders=Remainders(rows).OrderBy(x=>x);
                var pairs=Pairs(rows,other).OrderBy(x=>x);
                var common=Common(rows,other).OrderBy(x=>x);
                return string.Join(",",remainders)+"|"+CountRemainders(rows)+"|"+HasRemainder(rows,2)+","+HasRemainder(rows,3)+"|"+string.Join(",",pairs)+"|"+string.Join(",",common);
            }
        }
        """;

    // 遅延のまま返した結果は、何度列挙しても同じ結果になる(列挙ごとに評価し直す)。
    private const string LazySource = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        using Seamless.Linq;
        namespace Sample;
        public sealed class Rows:Seamless.Collections.ISet<int>{
            private readonly HashSet<int> items;
            public Rows(params int[] values){this.items=new HashSet<int>(values);}
            public int Count=>this.items.Count;
            public bool Contains(int item)=>this.items.Contains(item);
            public IEnumerator<int> GetEnumerator()=>this.items.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
        }
        public sealed class Probe{
            [LinqIntercept(true)]
            private static IEnumerable<int> FromSet(Rows rows)=>rows.Where(x=>x>1).Select(x=>x%3);
            [LinqIntercept(true)]
            private static IEnumerable<int> FromArray(int[] rows)=>rows.Where(x=>x>1).Select(x=>x%3);
            public static string Run(){
                var set=FromSet(new Rows(1,2,3,4,5,6));
                var array=FromArray([1,2,3,4,5,6]);
                return string.Join(",",set.Order())+"|"+string.Join(",",set.Order())+"|"+string.Join(",",array.Order())+"|"+string.Join(",",array.Order());
            }
        }
        """;

    [Fact]
    public void LazyResultsCanBeEnumeratedAgain() {
        var (compilation,_) = ExecutableGeneration.Generate(LazySource,"LazySetCallLowering",0);
        var result = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (string)probe.GetMethod("Run")!.Invoke(null,null)!);
        Assert.Equal("0,1,2|0,1,2|0,0,1,2,2|0,0,1,2,2",result);
    }

    [Fact]
    public void SetCallsAreExpandedWithTheSameSetResults() {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"SetCallLowering",4);
        // Contains は ISet 自身のメソッドに結び付くので LINQ の呼び出しではなく、差し替えない(Set のまま同じ結果)。
        var result = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (string)probe.GetMethod("Run")!.Invoke(null,null)!);
        Assert.Equal("0,1,2|3|True,False|0,1|4,5,6",result);
        var executable = GeneratedSourceAssert.ExecutableText(generated);
        Assert.DoesNotContain("global::Seamless.Linq.Set.",executable);
        Assert.DoesNotContain("rows.Select(",executable);
        Assert.DoesNotContain("rows.Where(",executable);
    }
}

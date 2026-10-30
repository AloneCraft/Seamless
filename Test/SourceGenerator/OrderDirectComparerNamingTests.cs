using Seamless.FLinq;

using System.Text.RegularExpressions;

namespace Test.SourceGenerator;

/// <summary>
/// 配列の OrderBy/OrderByDescending(と GroupBy 集約の後の OrderBy)を ToArray/ToList へ直接展開するときの添字比較用の補助 struct は、
/// 差し替えメソッドを入れるクラスの直下に置かれる。断片の番号はメソッドごとに 0 から振り直すので、
/// 同じクラスに複数の差し替えメソッドがあっても補助 struct の名前が重ならず(CS0102/CS0111 にならず)、結果が System.Linq と同じであること。
/// </summary>
[LinqIntercept(false)]
public sealed class OrderDirectComparerNamingTests {
    private const string Source = """
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Container {
            private static readonly int[] Values = [5,3,8,1,9,3,7,2,8,0];
            public static string RunFLinq() {
                var container = new Container();
                return string.Join("|",
                    string.Join(",",container.OrderByToArray()),
                    string.Join(",",container.OrderByDescendingToList()),
                    string.Join(",",container.OrderToArray()),
                    string.Join(",",container.OrderBySelectToList()),
                    string.Join(",",container.GroupByCountOrderByToArray()));
            }
            [LinqIntercept(false)]
            public static string RunLinq() =>
                string.Join("|",
                    string.Join(",",Values.OrderBy(x => x%4).ToArray()),
                    string.Join(",",Values.OrderByDescending(x => x).ToList()),
                    string.Join(",",Values.Order().ToArray()),
                    string.Join(",",Values.OrderBy(x => -x).Select(x => x*10).ToList()),
                    string.Join(",",Values.GroupBy(x => x%3).Select(g => g.Count()).OrderBy(x => x).ToArray()));
            [LinqIntercept(true)]
            private int[] OrderByToArray() => Values.OrderBy(x => x%4).ToArray();
            [LinqIntercept(true)]
            private System.Collections.Generic.List<int> OrderByDescendingToList() => Values.OrderByDescending(x => x).ToList();
            [LinqIntercept(true)]
            private int[] OrderToArray() => Values.Order().ToArray();
            [LinqIntercept(true)]
            private System.Collections.Generic.List<int> OrderBySelectToList() => Values.OrderBy(x => -x).Select(x => x*10).ToList();
            [LinqIntercept(true)]
            private int[] GroupByCountOrderByToArray() => Values.GroupBy(x => x%3).Select(g => g.Count()).OrderBy(x => x).ToArray();
        }
        """;

    [Fact]
    public void 同じクラスの複数の並べ替え展開で補助structの名前が重ならずSystem_Linqと同じ結果になる() {
        // 生成コードのコンパイルエラー(名前の重複を含む)は Generate の中で検査される。
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"OrderDirectComparerNamingProbe",5);
        var names = Regex.Matches(generated,@"private readonly struct (\w*OrderDirectComparer\w*)")
            .Select(static match => match.Groups[1].Value)
            .ToArray();
        // 直接展開の経路を通らないとこのテストは空振りになるので、補助 struct が複数のメソッド分出ていることを確かめる。
        Assert.True(names.Length>=2,"補助 struct の数="+names.Length);
        Assert.Equal(names.Length,names.Distinct().Count());
        var (flinq,linq) = ExecutableGeneration.Run(compilation,"Sample.Container",container => (
            (string)container.GetMethod("RunFLinq")!.Invoke(null,null)!,
            (string)container.GetMethod("RunLinq")!.Invoke(null,null)!));
        Assert.Equal(linq,flinq);
    }
}

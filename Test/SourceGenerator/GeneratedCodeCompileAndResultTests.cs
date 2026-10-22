using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 生成コードのコンパイルエラー(Generate の中で検査される)と、System.Linq との結果の一致を、原因ごとに確かめる。
/// </summary>
[LinqIntercept(false)]
public sealed class GeneratedCodeCompileAndResultTests {
    private static void AssertSameAsLinq(string source,string assemblyName,int minimumInterceptions) {
        var (compilation,_) = ExecutableGeneration.Generate(source,assemblyName,minimumInterceptions);
        var (flinq,linq) = ExecutableGeneration.Run(compilation,"Sample.Container",container => (
            (string)container.GetMethod("RunFLinq")!.Invoke(null,null)!,
            (string)container.GetMethod("RunLinq")!.Invoke(null,null)!));
        Assert.Equal(linq,flinq);
    }

    // ExceptBy/IntersectBy の第2引数は要素ではなく鍵そのもの。鍵選択関数を通すと int に .Key を読もうとして CS1061 になり、
    // 要素と同じ型の鍵(x%3 など)では黙って結果が変わる。
    [Fact]
    public void ExceptByとIntersectByは第2引数を鍵として扱いSystem_Linqと同じ結果になる() {
        AssertSameAsLinq("""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed record Row(int Key,int Value);
            public sealed class Container {
                private static Row[] Rows() => [new(1,10),new(2,20),new(3,30),new(1,40)];
                private static int[] Second() => [3,4,5];
                private static int[] Numbers() => [1,2,3,4,5,6,7,8];
                private static int[] Keys() => [0,1];
                public static string RunFLinq() => string.Join("|",
                    string.Join(",",ExceptByRows().Select(r => r.Value)),
                    string.Join(",",IntersectByRows().Select(r => r.Value)),
                    string.Join(",",ExceptBySameType()),
                    string.Join(",",IntersectBySameType()));
                [LinqIntercept(false)]
                public static string RunLinq() => string.Join("|",
                    string.Join(",",Rows().ExceptBy(Second(),r => r.Key).ToList().Select(r => r.Value)),
                    string.Join(",",Rows().IntersectBy(Second(),r => r.Key).ToList().Select(r => r.Value)),
                    string.Join(",",Numbers().ExceptBy(Keys(),x => x%3).ToList()),
                    string.Join(",",Numbers().IntersectBy(Keys(),x => x%3).ToList()));
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<Row> ExceptByRows() => Rows().ExceptBy(Second(),r => r.Key).ToList();
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<Row> IntersectByRows() => Rows().IntersectBy(Second(),r => r.Key).ToList();
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> ExceptBySameType() => Numbers().ExceptBy(Keys(),x => x%3).ToList();
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> IntersectBySameType() => Numbers().IntersectBy(Keys(),x => x%3).ToList();
            }
            ""","ExceptByKeysProbe",4);
    }

    // Select の中の c.Sum() のようなスカラーの直接展開は、結果の変数を自分で宣言する。
    // 呼び出し側が先に宣言しているのに同じ名前で宣言し直すと CS0128(同じ範囲で二重定義)になる。
    [Fact]
    public void Chunkの後のSelectでSumを直接展開しても結果の変数が二重に宣言されない() {
        AssertSameAsLinq("""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container {
                private static System.Collections.Generic.List<int> Source() => [1,2,3,4,5,6,7];
                public static string RunFLinq() => string.Join(",",ChunkSums());
                [LinqIntercept(false)]
                public static string RunLinq() => string.Join(",",Source().Where(x => x>0).Chunk(2).Select(c => c.Sum()).ToList());
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> ChunkSums() => Source().Where(x => x>0).Chunk(2).Select(c => c.Sum()).ToList();
            }
            ""","ChunkSumNestedScalarProbe",1);
    }

    // GroupBy の結果選択子が外の値に触れる式で集約と並べ替えを融合するとき、
    // 束縛済みローカルは static メソッド内のローカル変数であり、this. を付けると CS0026、宣言を出し忘れると CS0103 になる。
    [Fact]
    public void GroupByの結果選択子が外の値を使う集約とOrderByの融合がstaticメソッドでコンパイルできる() {
        AssertSameAsLinq("""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed record Item(int Key,int Id);
            public sealed class Container {
                private static Item[] Items() => [new(1,5),new(2,3),new(1,9),new(3,1),new(2,4)];
                public static string RunFLinq() => string.Join(",",Run(3,4));
                [LinqIntercept(false)]
                public static string RunLinq() => string.Join(",",Items()
                    .GroupBy(i => i.Key,i => i.Id+3*3,(k,g) => k+g.Sum()+4*4).OrderBy(x => x).ToList());
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> Run(int a,int b) => Items()
                    .GroupBy(i => i.Key,i => i.Id+a*a,(k,g) => k+g.Sum()+b*b).OrderBy(x => x).ToList();
            }
            ""","GroupByResultHoistProbe",1);
    }

    // Skip を続けた式は、それぞれの負数を 0 に直してから足す。足し合わせが int を超えるときは頭打ちにする。
    [Fact]
    public void 連続したSkipは負数を0に直し桁あふれせずSystem_Linqと同じ結果になる() {
        AssertSameAsLinq("""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container {
                public static string RunFLinq() => string.Join("|",
                    string.Join(",",Skip2(5,-10)),string.Join(",",Skip2(-10,2)),string.Join(",",Skip2(int.MaxValue,int.MaxValue)),
                    string.Join(",",SkipLast2(5,-10)),string.Join(",",SkipLast2(-3,2)),
                    string.Join(",",SkipLiteral()));
                [LinqIntercept(false)]
                public static string RunLinq() => string.Join("|",
                    string.Join(",",System.Linq.Enumerable.Range(10,6).Skip(5).Skip(-10).ToList()),
                    string.Join(",",System.Linq.Enumerable.Range(10,6).Skip(-10).Skip(2).ToList()),
                    string.Join(",",System.Linq.Enumerable.Range(10,6).Skip(int.MaxValue).Skip(int.MaxValue).ToList()),
                    string.Join(",",System.Linq.Enumerable.Range(10,6).SkipLast(5).SkipLast(-10).ToList()),
                    string.Join(",",System.Linq.Enumerable.Range(10,6).SkipLast(-3).SkipLast(2).ToList()),
                    string.Join(",",System.Linq.Enumerable.Range(10,6).Skip(-3).Skip(2).ToList()));
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> Skip2(int a,int b) => System.Linq.Enumerable.Range(10,6).Skip(a).Skip(b).ToList();
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> SkipLast2(int a,int b) => System.Linq.Enumerable.Range(10,6).SkipLast(a).SkipLast(b).ToList();
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> SkipLiteral() => System.Linq.Enumerable.Range(10,6).Skip(-3).Skip(2).ToList();
            }
            ""","ConsecutiveSkipProbe",3);
    }

    // Concat の後ろに Where などの出力が詰まる段が続くとき、件数を先に確定して添字で書く展開を使うと、
    // 間引かれた位置に既定値が残る。
    [Fact]
    public void Concatの後のWhereやSkipが付いたToListとToArrayはSystem_Linqと同じ結果になる() {
        AssertSameAsLinq("""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container {
                private static int[] A() => [1,2,3,4,5,6,7,8];
                private static int[] B() => [4,5,6,9,10];
                public static string RunFLinq() => string.Join("|",
                    string.Join(",",WhereList()),string.Join(",",WhereArray()),string.Join(",",SkipList()),string.Join(",",SelectList()));
                [LinqIntercept(false)]
                public static string RunLinq() => string.Join("|",
                    string.Join(",",A().Concat(B()).Where(v => v%2==0).ToList()),
                    string.Join(",",A().Concat(B()).Where(v => v%2==0).ToArray()),
                    string.Join(",",A().Concat(B()).Skip(3).ToList()),
                    string.Join(",",A().Concat(B()).Select(v => v*2).ToList()));
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> WhereList() => A().Concat(B()).Where(v => v%2==0).ToList();
                [LinqIntercept(true)]
                private static int[] WhereArray() => A().Concat(B()).Where(v => v%2==0).ToArray();
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> SkipList() => A().Concat(B()).Skip(3).ToList();
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> SelectList() => A().Concat(B()).Select(v => v*2).ToList();
            }
            ""","ConcatFilteredMaterializeProbe",4);
    }
}

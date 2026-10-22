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
}

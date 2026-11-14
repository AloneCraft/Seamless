using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 名前を付けて書いたメンバー(拡張プロパティ・拡張メソッド・同じ型のメンバー)の本体を、呼び出し側へ取り込んで展開すること。
/// ビューはプロパティ、関数はメソッドとして書くので、取り込めば呼び出し側の LINQ と1つのループになる。
/// </summary>
[LinqIntercept(false)]
public sealed class MemberBodyInliningTests {
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
        public interface IRows{ Rows Items{get;} }
        public sealed class Store:IRows{ public Rows Items{get;}=new Rows(1,2,3,4,5,6); }
        public static class Queries{
            extension(IRows q){
                public Seamless.Collections.ISet<int> Even=>q.Items.Where(x=>x%2==0);
                public Seamless.Collections.ISet<int> Remainders(int divisor)=>q.Items.Select(x=>x%divisor);
                public int Total(int minimum)=>q.Items.Where(x=>x>=minimum).Sum();
                public int EvenTotal=>q.Even.Sum();
            }
        }
        public sealed class Probe{
            private readonly int[] values=[1,2,3];
            private int Doubled=>this.values.Select(x=>x*2).Sum();
            [LinqIntercept(true)]
            private static int[] EvenTimesTen(Store store)=>store.Even.Select(x=>x*10).ToArray();
            [LinqIntercept(true)]
            private static int CountRemainders(Store store,int divisor)=>store.Remainders(divisor).Count();
            [LinqIntercept(true)]
            private static int Total(Store store)=>store.Total(4);
            [LinqIntercept(true)]
            private static int Nested(Store store)=>store.EvenTotal;
            [LinqIntercept(true)]
            private int SameType()=>this.Doubled+1;
            public static string Run(){
                var store=new Store();
                return string.Join(",",EvenTimesTen(store).OrderBy(x=>x))+"|"+CountRemainders(store,3)+"|"+Total(store)+"|"+Nested(store)+"|"+new Probe().SameType();
            }
        }
        """;

    // 非公開のフィールドの読み書きと LINQ が同じメソッドにある形(取り込みで生まれる形でもある)。
    // 以前は生成コードが非公開のフィールドへの補助を宣言せずに参照し、コンパイルできなかった(return などの文の書き出しと、日本語の名前の補助を使われていないと見なしていたこと)。
    private const string PrivateFieldSource = """
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Store{
            private long 最後の値=-1;
            private readonly int[] orders=[3,7,5];
            private long SavedMaximum()=>this.orders.Select(x=>(long)x).DefaultIfEmpty(0).Max();
            [LinqIntercept(true)]
            private long Next(){
                if(this.最後の値<0){
                    this.最後の値=this.SavedMaximum();
                }
                return this.最後の値+1;
            }
            [LinqIntercept(true)]
            private long NextDirect(){
                if(this.最後の値<0){
                    this.最後の値=this.orders.Select(x=>(long)x).DefaultIfEmpty(0).Max();
                }
                return this.最後の値+1;
            }
            public static string Run(){
                var first=new Store();var second=new Store();
                return first.Next()+","+first.Next()+"|"+second.NextDirect();
            }
        }
        """;

    // ビューの本体が別のビューを使い、呼び出し側と本体で同じラムダの引数名(v)を使う形と、ビューの射影にオブジェクト初期化子がある形。
    private const string NestedSource = """
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
        public readonly record struct Doubled(int Id,int Twice);
        public readonly record struct Nested(int Id);
        public readonly record struct Initialized(int Id,int Value);
        public interface IRows{ Rows Items{get;} }
        public sealed class Store:IRows{ public Rows Items{get;}=new Rows(1,2,3,4,5,6); }
        public static class Queries{
            extension(IRows q){
                public Seamless.Collections.ISet<Doubled> Doubled=>q.Items.Select(static v=>new Doubled(v,v*10));
                public Seamless.Collections.ISet<Nested> Nested=>q.Doubled.Where(static v=>v.Twice>40).Select(static v=>new Nested(v.Id));
                public Seamless.Collections.ISet<Initialized> Initialized=>q.Items.Select(static v=>new Initialized(v,v*10){Id=0});
            }
        }
        public sealed class Probe{
            [LinqIntercept(true)]
            private static int[] NestedIds(Store store)=>store.Nested.Select(static v=>v.Id).ToArray();
            [LinqIntercept(true)]
            private static int[] InitializedFirst(Store store)=>store.Initialized.Where(static v=>v.Id==1).Select(static v=>v.Value).ToArray();
            public static string Run(){
                var store=new Store();
                return string.Join(",",NestedIds(store).OrderBy(x=>x))+"|"+string.Join(",",InitializedFirst(store));
            }
        }
        """;

    [Fact]
    public void NestedViewsWithTheSameLambdaNamesAndInitializerProjectionsKeepTheirResults() {
        var (compilation,generated) = ExecutableGeneration.Generate(NestedSource,"NestedMemberBodyInlining",2);
        var result = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (string)probe.GetMethod("Run")!.Invoke(null,null)!);
        // Nested は Twice>40(5,6)。Initialized は初期化子で Id を 0 にするので、Id==1 の行は無い。
        Assert.Equal("5,6|",result);
        var executable = GeneratedSourceAssert.ExecutableText(generated);
        Assert.DoesNotContain("store.Nested",executable);
        Assert.DoesNotContain("store.Initialized",executable);
    }

    [Fact]
    public void PrivateFieldWritesAndLinqInTheSameMethodCompile() {
        var (compilation,_) = ExecutableGeneration.Generate(PrivateFieldSource,"PrivateFieldAndLinq",2);
        var result = ExecutableGeneration.Run(compilation,"Sample.Store",store => (string)store.GetMethod("Run")!.Invoke(null,null)!);
        Assert.Equal("8,8|8",result);
    }

    [Fact]
    public void NamedMemberBodiesAreInlinedIntoTheCaller() {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"MemberBodyInlining",5);
        var result = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (string)probe.GetMethod("Run")!.Invoke(null,null)!);
        // Remainders は Set.Select なので重複を除く(1..6 を 3 で割った余りは 0,1,2 の3つ)。
        Assert.Equal("20,40,60|3|15|12|13",result);
        var executable = GeneratedSourceAssert.ExecutableText(generated);
        Assert.DoesNotContain("store.Even",executable);
        Assert.DoesNotContain("store.Remainders(",executable);
        Assert.DoesNotContain("store.Total(",executable);
        Assert.DoesNotContain("store.EvenTotal",executable);
        Assert.DoesNotContain("this.Doubled",executable);
    }
}

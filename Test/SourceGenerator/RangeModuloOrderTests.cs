using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Range を x%C のキーで並べる OrderBy/OrderByDescending(続く ThenBy(x)/ThenByDescending(x) を含む)を、
/// 並べ替えずに剰余類ごとに C 刻みで回す変換が System.Linq と同じ順を返すこと。
/// 負の値(C# の剰余は負になる)・0 をまたぐ区間・int の端・空を含める。Consume へ直接流す経路と列挙子の経路の両方を見る。
/// </summary>
[LinqIntercept(false)]
public sealed class RangeModuloOrderTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Sink {
            public readonly List<int> Values = new();
            public void Consume<T>(T value) => this.Values.Add((int)(object)value!);
        }
        public sealed class Probe {
            private static string Collect(Action<Sink> action) {
                var sink = new Sink();
                try {
                    action(sink);
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
                return string.Join(",",sink.Values);
            }
            private static string Describe(Func<List<int>> evaluate) {
                try {
                    return string.Join(",",evaluate());
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
            }
            public static string RunFLinq(int s,int c) => string.Join("|",
                Collect(sink => F.Ascending7(s,c,sink)),
                Collect(sink => F.Descending7(s,c,sink)),
                Collect(sink => F.ThenBy7(s,c,sink)),
                Collect(sink => F.ThenByDescending7(s,c,sink)),
                Collect(sink => F.Ascending1000(s,c,sink)),
                Describe(() => F.DescendingThenByDescendingList(s,c)),
                Describe(() => F.AscendingList(s,c)),
                Describe(() => F.NotIdentityThenBy(s,c)));
            public static string RunLinq(int s,int c) => string.Join("|",
                Collect(sink => L.Ascending7(s,c,sink)),
                Collect(sink => L.Descending7(s,c,sink)),
                Collect(sink => L.ThenBy7(s,c,sink)),
                Collect(sink => L.ThenByDescending7(s,c,sink)),
                Collect(sink => L.Ascending1000(s,c,sink)),
                Describe(() => L.DescendingThenByDescendingList(s,c)),
                Describe(() => L.AscendingList(s,c)),
                Describe(() => L.NotIdentityThenBy(s,c)));
        }
        public sealed class F {
            [LinqIntercept(true)] public static void Ascending7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).OrderBy(x => x%7));
            [LinqIntercept(true)] public static void Descending7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).OrderByDescending(x => x%7));
            [LinqIntercept(true)] public static void ThenBy7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).OrderBy(x => x%7).ThenBy(x => x));
            [LinqIntercept(true)] public static void ThenByDescending7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).OrderBy(x => x%7).ThenByDescending(x => x));
            [LinqIntercept(true)] public static void Ascending1000(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).OrderBy(x => x%1000));
            [LinqIntercept(true)] public static List<int> DescendingThenByDescendingList(int s,int c) => Enumerable.Range(s,c).OrderByDescending(x => x%7).ThenByDescending(x => x).ToList();
            [LinqIntercept(true)] public static List<int> AscendingList(int s,int c) => Enumerable.Range(s,c).OrderBy(x => x%7).ToList();
            [LinqIntercept(true)] public static List<int> NotIdentityThenBy(int s,int c) => Enumerable.Range(s,c).OrderBy(x => x%7).ThenBy(x => -x).ToList();
        }
        [LinqIntercept(false)]
        public sealed class L {
            public static void Ascending7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).OrderBy(x => x%7)) sink.Consume(v); }
            public static void Descending7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).OrderByDescending(x => x%7)) sink.Consume(v); }
            public static void ThenBy7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).OrderBy(x => x%7).ThenBy(x => x)) sink.Consume(v); }
            public static void ThenByDescending7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).OrderBy(x => x%7).ThenByDescending(x => x)) sink.Consume(v); }
            public static void Ascending1000(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).OrderBy(x => x%1000)) sink.Consume(v); }
            public static List<int> DescendingThenByDescendingList(int s,int c) => Enumerable.Range(s,c).OrderByDescending(x => x%7).ThenByDescending(x => x).ToList();
            public static List<int> AscendingList(int s,int c) => Enumerable.Range(s,c).OrderBy(x => x%7).ToList();
            public static List<int> NotIdentityThenBy(int s,int c) => Enumerable.Range(s,c).OrderBy(x => x%7).ThenBy(x => -x).ToList();
        }
        """;

    [Theory]
    [InlineData(1,0)]
    [InlineData(0,1)]
    [InlineData(1,5)]
    [InlineData(1,4096)]
    [InlineData(-20,45)]
    [InlineData(-5,3)]
    [InlineData(-14,15)]
    [InlineData(int.MaxValue-10,11)]
    [InlineData(int.MinValue,10)]
    [InlineData(1,-1)]
    public void System_Linqと同じ順を返す(int start,int count) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"RangeModuloOrderProbe",8);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
    }

    [Fact]
    public void 剰余のキーと恒等のThenByだけを剰余類の巡回にする() {
        var (_,generated) = ExecutableGeneration.Generate(Source,"RangeModuloOrderShape",8);
        // Consume へ流す5つは剰余類を回すループ、ToList の2つは列挙子(x => -x の ThenBy は従来の並べ替え)。
        // 同じキーの中が昇順の4つは、キーごとに剰余を求めず前のキーの最初の値から次を求める。ThenByDescending の1つは剰余で求める。
        // 列挙子の構造体は根ごとに出るので7つ。
        Assert.Equal(4,global::System.Text.RegularExpressions.Regex.Matches(generated,@"__previousFirst\d+=__first\d+;").Count);
        Assert.Equal(1,global::System.Text.RegularExpressions.Regex.Matches(generated,@"var __residue\d+=__key\d+<0").Count);
        Assert.Equal(7,global::System.Text.RegularExpressions.Regex.Matches(generated,@"var __residue\d+=this\.key<0").Count);
    }
}

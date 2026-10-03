using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 添字を使わない Select/Where だけが2段以上並ぶ列を Consume へ流すとき、列挙子の構造体を入れ子に呼ばず、
/// 差し替えメソッドの中で span(または根の列挙子)を直接回すこと。Range は Range 専用の経路のまま。参照型の要素の共有ジェネリックコードでも内側がインライン化される形にする。
/// </summary>
[LinqIntercept(false)]
public sealed class StatelessChainDirectConsumeTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Item(int value) {
            public int Value { get; } = value;
        }
        public sealed class Sink {
            public readonly List<string> Values = new();
            public void Consume<T>(T value) => this.Values.Add(value!.ToString()!);
        }
        public abstract class Holder<T> where T : notnull {
            public abstract T[] Source();
            public abstract int ToInt(T value);
            // ベンチと同じく、差し替える呼び出しはジェネリックなクラスの中に置く(差し替えメソッドもジェネリックになる)。
            public void RunAll(Sink sink) {
                this.SelectWhere(sink);
                this.WhereWhereSelect(sink);
                this.RangeSelectWhere(6,sink);
            }
            [LinqIntercept(true)]
            private void SelectWhere(Sink sink) => sink.Consume(this.Source().Select(x => (Value: x,Number: ToIntStatic(x))).Where(p => p.Number%2==0).Select(p => p.Number*10));
            [LinqIntercept(true)]
            private void WhereWhereSelect(Sink sink) => sink.Consume(this.Source().Where(x => ToIntStatic(x)>1).Where(x => ToIntStatic(x)<6).Select(x => ToIntStatic(x)+100));
            [LinqIntercept(true)]
            private void RangeSelectWhere(int count,Sink sink) => sink.Consume(Enumerable.Range(1,count).Select(x => x*3).Where(x => x%2==1));
            public static int ToIntStatic(T value) => value is Item item ? item.Value : (int)(object)value;
        }
        public sealed class ItemHolder:Holder<Item> {
            public override Item[] Source() => [new(1),new(2),new(3),new(4),new(5),new(6),new(7)];
            public override int ToInt(Item value) => value.Value;
        }
        public sealed class Probe {
            public static string Run() {
                var holder = new ItemHolder();
                var sink = new Sink();
                holder.RunAll(sink);
                return string.Join(",",sink.Values);
            }
        }
        """;

    [Fact]
    public void 状態を持たない段の並びを直接回す() {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"StatelessChainDirectConsumeProbe",3);
        var actual = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (string)probe.GetMethod("Run")!.Invoke(null,null)!);
        Assert.Equal("20,40,60,102,103,104,105,3,9,15",actual);
        // 配列の2つは直接のループ。Range は Range 専用の最適化(偶奇の刻みなど)を使う既存の経路のまま。
        Assert.Equal(2,global::System.Text.RegularExpressions.Regex.Matches(generated,@"for\(var __i\w+=0;__i\w+<__chain(Span\w+\.Length|Count\w+);").Count);
    }
}

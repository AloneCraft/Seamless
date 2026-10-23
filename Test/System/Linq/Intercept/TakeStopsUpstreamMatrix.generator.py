import os
cases = [
 ("WhereTake", "src.Where(x => P(x)).Take(2)"),
 ("SecurityDetailDaysShape", "src.Select(x => F(x)).Where(x => x%20==0).Select(x => F(x)).Take(2).Select(x => F(x))"),
 ("WhereTakeZero", "src.Where(x => P(x)).Take(0)"),
 ("SelectTakeOne", "src.Select(x => F(x)).Take(1)"),
 ("SelectManyTakeOne", "src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1)"),
 ("ConcatTakeBoundary", "src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9)"),
 ("SelectTake", "src.Select(x => F(x)).Take(2)"),
 ("WhereSelectTake", "src.Where(x => P(x)).Select(x => F(x)).Take(2)"),
 ("SelectWhereTake", "src.Select(x => F(x)).Where(x => x%20==0).Take(2)"),
 ("SelectThenSelectManyTake", "src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3)"),
 ("SelectManyThenSelectTake", "src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3)"),
 ("WhereThenSelectManyTake", "src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3)"),
 ("SelectConcatTake", "src.Select(x => F(x)).Concat(Other()).Take(2)"),
 ("ConcatSelectTake", "src.Concat(Other().Select(x => F(x))).Take(10)"),
 ("WhereConcatWhereTake", "src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3)"),
 ("IndexedSelectTake", "src.Select((x,i) => FI(x,i)).Take(2)"),
 ("IndexedWhereTake", "src.Where((x,i) => PI(x,i)).Take(2)"),
 ("SelectSkipTake", "src.Select(x => F(x)).Skip(1).Take(2)"),
 ("SelectDistinctTake", "src.Select(x => F(x)).Distinct().Take(2)"),
 ("SelectAppendTake", "src.Select(x => F(x)).Append(7).Take(2)"),
 ("SelectPrependTake", "src.Select(x => F(x)).Prepend(7).Take(2)"),
 ("ZipTake", "src.Zip(Other(),(x,y) => F(x+y)).Take(2)"),
 ("SelectTakeWhileTake", "src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2)"),
 ("SelectTakeSelectTake", "src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1)"),
 ("SelectUnionTake", "src.Select(x => F(x)).Union(Other()).Take(2)"),
 ("SelectDistinctByTake", "src.Select(x => F(x)).DistinctBy(x => x%3).Take(2)"),
 ("IndexedSelectManyTake", "src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3)"),
 ("SelectExceptTake", "src.Select(x => F(x)).Except(Other()).Take(2)"),
 ("SelectSkipWhileTake", "src.Select(x => F(x)).SkipWhile(x => x<30).Take(2)"),
]
sources = [("Array","var src = A();"),("List","var src = new List<int>(A());"),("Enumerable","var src = Lazy(A());")]
terms = [("ToArray","{0}.ToArray()"),("Sum","new[] {{ {0}.Sum() }}"),("Consume","CONSUME")]
out=[]
out.append('''namespace Test.System.Linq.Intercept;

/// <summary>
/// 自動生成(TakeStopsUpstreamMatrix.generator.py): Take より手前のラムダが、System.Linq より多く評価されないことを、段の並びごとに確かめる。
/// 各ケースは、最適化ありのメソッドと、最適化なし(System.Linq 相当)のメソッドの組で、結果が同じで、ラムダの呼び出し回数が増えないこと。
/// </summary>
public sealed class TakeStopsUpstreamMatrix {
    private static int calls;

    private static int[] A() => [1,2,3,4,5,6,7,8];
    private static int[] Other() => [11,12,13,14,15,16];
    private static IEnumerable<int> Lazy(int[] values) {
        foreach(var value in values) {
            yield return value;
        }
    }
    private static int F(int x) { calls++; return x*10; }
    private static bool P(int x) { calls++; return x%2==0; }
    private static int FI(int x,int i) { calls++; return x+i; }
    private static bool PI(int x,int i) { calls++; return (x+i)%2==0; }

    private static (int[]? Result,int Calls,string? Error) Measure(Func<int[]> run) {
        calls=0;
        try {
            return (run(),calls,null);
        } catch(Exception exception) {
            return (null,calls,exception.GetType().Name);
        }
    }
''')
def ret(tsuf,expr):
    if tsuf=="CONSUME":
        return "var consumer = new Consumer<int>();" + chr(10) + "        " + expr + ".Consume(consumer);" + chr(10) + "        return consumer.Items.ToArray();"
    return "return " + tsuf.format(expr) + ";"
names=[]
for cname,expr in cases:
    for sname,sdecl in sources:
        for tname,tsuf in terms:
            n=f"{cname}_{sname}_{tname}"
            names.append(n)
            for mode,flag in (("Opt","true"),("Ref","false")):
                out.append(f'''    [global::Seamless.FLinq.LinqIntercept({flag})]
    private static int[] {mode}_{n}() {{
        {sdecl}
        {ret(tsuf,expr)}
    }}
''')
out.append('''    private static void Check(Func<int[]> optimized,Func<int[]> reference,string name) {
        var (optimizedResult,optimizedCalls,optimizedError) = Measure(optimized);
        var (referenceResult,referenceCalls,referenceError) = Measure(reference);
        Assert.Equal(referenceError,optimizedError);
        Assert.Equal(referenceResult,optimizedResult);
        Assert.True(optimizedCalls<=referenceCalls,$"{name}: 最適化あり {optimizedCalls} 回 > System.Linq {referenceCalls} 回");
    }
''')
for n in names:
    skip = ''
    out.append(f'''    [Fact({skip.rstrip(',')}), Trait("Category","Unit")]
    public void {n}() => Check(() => Opt_{n}(),() => Ref_{n}(),"{n}");
''')
out.append('}\n')
open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'TakeStopsUpstreamMatrix.cs'),'w',encoding='utf-8').write(chr(10).join(out))
print(len(names))

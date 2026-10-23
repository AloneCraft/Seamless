namespace Test.System.Linq.Intercept;

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

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTake_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTake_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTake_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTake_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTakeZero_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).Take(0).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTakeZero_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).Take(0).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTakeZero_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).Take(0).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTakeZero_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).Take(0).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTakeZero_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(0).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTakeZero_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(0).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTakeZero_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).Take(0).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTakeZero_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).Take(0).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTakeZero_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).Take(0).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTakeZero_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).Take(0).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTakeZero_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(0).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTakeZero_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(0).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTakeZero_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).Take(0).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTakeZero_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).Take(0).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTakeZero_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).Take(0).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTakeZero_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).Take(0).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereTakeZero_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(0).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereTakeZero_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Take(0).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeOne_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeOne_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeOne_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeOne_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeOne_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeOne_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeOne_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeOne_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeOne_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeOne_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeOne_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeOne_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeOne_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeOne_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeOne_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeOne_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeOne_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeOne_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyTakeOne_Array_ToArray() {
        var src = A();
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyTakeOne_Array_ToArray() {
        var src = A();
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyTakeOne_Array_Sum() {
        var src = A();
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyTakeOne_Array_Sum() {
        var src = A();
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyTakeOne_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyTakeOne_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyTakeOne_List_ToArray() {
        var src = new List<int>(A());
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyTakeOne_List_ToArray() {
        var src = new List<int>(A());
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyTakeOne_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyTakeOne_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyTakeOne_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyTakeOne_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyTakeOne_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyTakeOne_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyTakeOne_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyTakeOne_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyTakeOne_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyTakeOne_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatTakeBoundary_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatTakeBoundary_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatTakeBoundary_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatTakeBoundary_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatTakeBoundary_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatTakeBoundary_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatTakeBoundary_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatTakeBoundary_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatTakeBoundary_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatTakeBoundary_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatTakeBoundary_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatTakeBoundary_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatTakeBoundary_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatTakeBoundary_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatTakeBoundary_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatTakeBoundary_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatTakeBoundary_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatTakeBoundary_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other().Select(x => F(x))).Take(9).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereSelectTake_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereSelectTake_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).Select(x => F(x)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).Select(x => F(x)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Select(x => F(x)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectWhereTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Where(x => x%20==0).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectWhereTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Where(x => x%20==0).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectWhereTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Where(x => x%20==0).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectWhereTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Where(x => x%20==0).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectWhereTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Where(x => x%20==0).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectWhereTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Where(x => x%20==0).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectWhereTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Where(x => x%20==0).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectWhereTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Where(x => x%20==0).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectWhereTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Where(x => x%20==0).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectWhereTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Where(x => x%20==0).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectWhereTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Where(x => x%20==0).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectWhereTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Where(x => x%20==0).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectWhereTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Where(x => x%20==0).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectWhereTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Where(x => x%20==0).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectWhereTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Where(x => x%20==0).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectWhereTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Where(x => x%20==0).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectWhereTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Where(x => x%20==0).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectWhereTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Where(x => x%20==0).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectThenSelectManyTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectThenSelectManyTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectThenSelectManyTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectThenSelectManyTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectThenSelectManyTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectThenSelectManyTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectThenSelectManyTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectThenSelectManyTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectThenSelectManyTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectThenSelectManyTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectThenSelectManyTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectThenSelectManyTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectThenSelectManyTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectThenSelectManyTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectThenSelectManyTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectThenSelectManyTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectThenSelectManyTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectThenSelectManyTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany(x => new[] { x,x+1 }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyThenSelectTake_Array_ToArray() {
        var src = A();
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyThenSelectTake_Array_ToArray() {
        var src = A();
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyThenSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyThenSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyThenSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyThenSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyThenSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyThenSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyThenSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyThenSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyThenSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyThenSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyThenSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyThenSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyThenSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyThenSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectManyThenSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectManyThenSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.SelectMany(x => new[] { x,x+1 }).Select(x => F(x)).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereThenSelectManyTake_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereThenSelectManyTake_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereThenSelectManyTake_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereThenSelectManyTake_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereThenSelectManyTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereThenSelectManyTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereThenSelectManyTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereThenSelectManyTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereThenSelectManyTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereThenSelectManyTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereThenSelectManyTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereThenSelectManyTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereThenSelectManyTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereThenSelectManyTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereThenSelectManyTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereThenSelectManyTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereThenSelectManyTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereThenSelectManyTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).SelectMany(x => new[] { x,x }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectConcatTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Concat(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectConcatTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Concat(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectConcatTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Concat(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectConcatTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Concat(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectConcatTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectConcatTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectConcatTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Concat(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectConcatTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Concat(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectConcatTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Concat(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectConcatTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Concat(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectConcatTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectConcatTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectConcatTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Concat(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectConcatTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Concat(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectConcatTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Concat(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectConcatTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Concat(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectConcatTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectConcatTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Concat(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatSelectTake_Array_ToArray() {
        var src = A();
        return src.Concat(Other().Select(x => F(x))).Take(10).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatSelectTake_Array_ToArray() {
        var src = A();
        return src.Concat(Other().Select(x => F(x))).Take(10).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Concat(Other().Select(x => F(x))).Take(10).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Concat(Other().Select(x => F(x))).Take(10).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Concat(Other().Select(x => F(x))).Take(10).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Concat(Other().Select(x => F(x))).Take(10).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Concat(Other().Select(x => F(x))).Take(10).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Concat(Other().Select(x => F(x))).Take(10).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Concat(Other().Select(x => F(x))).Take(10).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Concat(Other().Select(x => F(x))).Take(10).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Concat(Other().Select(x => F(x))).Take(10).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Concat(Other().Select(x => F(x))).Take(10).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Concat(Other().Select(x => F(x))).Take(10).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Concat(Other().Select(x => F(x))).Take(10).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Concat(Other().Select(x => F(x))).Take(10).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Concat(Other().Select(x => F(x))).Take(10).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ConcatSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Concat(Other().Select(x => F(x))).Take(10).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ConcatSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Concat(Other().Select(x => F(x))).Take(10).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereConcatWhereTake_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereConcatWhereTake_Array_ToArray() {
        var src = A();
        return src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereConcatWhereTake_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereConcatWhereTake_Array_Sum() {
        var src = A();
        return new[] { src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereConcatWhereTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereConcatWhereTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereConcatWhereTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereConcatWhereTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereConcatWhereTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereConcatWhereTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereConcatWhereTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereConcatWhereTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereConcatWhereTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereConcatWhereTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereConcatWhereTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereConcatWhereTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_WhereConcatWhereTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_WhereConcatWhereTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where(x => P(x)).Concat(Other().Where(x => P(x))).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectTake_Array_ToArray() {
        var src = A();
        return src.Select((x,i) => FI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectTake_Array_ToArray() {
        var src = A();
        return src.Select((x,i) => FI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Select((x,i) => FI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Select((x,i) => FI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select((x,i) => FI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select((x,i) => FI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select((x,i) => FI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select((x,i) => FI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select((x,i) => FI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select((x,i) => FI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select((x,i) => FI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select((x,i) => FI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select((x,i) => FI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select((x,i) => FI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select((x,i) => FI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select((x,i) => FI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select((x,i) => FI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select((x,i) => FI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedWhereTake_Array_ToArray() {
        var src = A();
        return src.Where((x,i) => PI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedWhereTake_Array_ToArray() {
        var src = A();
        return src.Where((x,i) => PI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedWhereTake_Array_Sum() {
        var src = A();
        return new[] { src.Where((x,i) => PI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedWhereTake_Array_Sum() {
        var src = A();
        return new[] { src.Where((x,i) => PI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedWhereTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where((x,i) => PI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedWhereTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Where((x,i) => PI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedWhereTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where((x,i) => PI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedWhereTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Where((x,i) => PI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedWhereTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where((x,i) => PI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedWhereTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Where((x,i) => PI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedWhereTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where((x,i) => PI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedWhereTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Where((x,i) => PI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedWhereTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where((x,i) => PI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedWhereTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Where((x,i) => PI(x,i)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedWhereTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where((x,i) => PI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedWhereTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Where((x,i) => PI(x,i)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedWhereTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where((x,i) => PI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedWhereTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Where((x,i) => PI(x,i)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Skip(1).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Skip(1).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Skip(1).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Skip(1).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Skip(1).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Skip(1).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Skip(1).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Skip(1).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Skip(1).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Skip(1).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Skip(1).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Skip(1).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Skip(1).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Skip(1).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Skip(1).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Skip(1).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Skip(1).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Skip(1).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Distinct().Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Distinct().Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Distinct().Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Distinct().Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Distinct().Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Distinct().Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Distinct().Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Distinct().Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Distinct().Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Distinct().Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Distinct().Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Distinct().Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Distinct().Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Distinct().Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Distinct().Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Distinct().Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Distinct().Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Distinct().Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectAppendTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Append(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectAppendTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Append(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectAppendTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Append(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectAppendTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Append(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectAppendTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Append(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectAppendTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Append(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectAppendTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Append(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectAppendTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Append(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectAppendTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Append(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectAppendTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Append(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectAppendTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Append(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectAppendTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Append(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectAppendTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Append(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectAppendTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Append(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectAppendTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Append(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectAppendTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Append(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectAppendTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Append(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectAppendTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Append(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectPrependTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Prepend(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectPrependTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Prepend(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectPrependTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Prepend(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectPrependTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Prepend(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectPrependTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Prepend(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectPrependTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Prepend(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectPrependTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Prepend(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectPrependTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Prepend(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectPrependTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Prepend(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectPrependTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Prepend(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectPrependTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Prepend(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectPrependTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Prepend(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectPrependTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Prepend(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectPrependTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Prepend(7).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectPrependTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Prepend(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectPrependTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Prepend(7).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectPrependTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Prepend(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectPrependTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Prepend(7).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ZipTake_Array_ToArray() {
        var src = A();
        return src.Zip(Other(),(x,y) => F(x+y)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ZipTake_Array_ToArray() {
        var src = A();
        return src.Zip(Other(),(x,y) => F(x+y)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ZipTake_Array_Sum() {
        var src = A();
        return new[] { src.Zip(Other(),(x,y) => F(x+y)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ZipTake_Array_Sum() {
        var src = A();
        return new[] { src.Zip(Other(),(x,y) => F(x+y)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ZipTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Zip(Other(),(x,y) => F(x+y)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ZipTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Zip(Other(),(x,y) => F(x+y)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ZipTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Zip(Other(),(x,y) => F(x+y)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ZipTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Zip(Other(),(x,y) => F(x+y)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ZipTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Zip(Other(),(x,y) => F(x+y)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ZipTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Zip(Other(),(x,y) => F(x+y)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ZipTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Zip(Other(),(x,y) => F(x+y)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ZipTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Zip(Other(),(x,y) => F(x+y)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ZipTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Zip(Other(),(x,y) => F(x+y)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ZipTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Zip(Other(),(x,y) => F(x+y)).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ZipTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Zip(Other(),(x,y) => F(x+y)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ZipTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Zip(Other(),(x,y) => F(x+y)).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_ZipTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Zip(Other(),(x,y) => F(x+y)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_ZipTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Zip(Other(),(x,y) => F(x+y)).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeWhileTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeWhileTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeWhileTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeWhileTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeWhileTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeWhileTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeWhileTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeWhileTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeWhileTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeWhileTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeWhileTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeWhileTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeWhileTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeWhileTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeWhileTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeWhileTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeWhileTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeWhileTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).TakeWhile(x => x<1000).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeSelectTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeSelectTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeSelectTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeSelectTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeSelectTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeSelectTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeSelectTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeSelectTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeSelectTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectTakeSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectTakeSelectTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Take(2).Select(x => F(x)).Take(1).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectUnionTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Union(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectUnionTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Union(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectUnionTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Union(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectUnionTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Union(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectUnionTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Union(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectUnionTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Union(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectUnionTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Union(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectUnionTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Union(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectUnionTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Union(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectUnionTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Union(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectUnionTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Union(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectUnionTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Union(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectUnionTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Union(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectUnionTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Union(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectUnionTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Union(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectUnionTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Union(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectUnionTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Union(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectUnionTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Union(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctByTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctByTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctByTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctByTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctByTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctByTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctByTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctByTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctByTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctByTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctByTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctByTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctByTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctByTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctByTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctByTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectDistinctByTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectDistinctByTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).DistinctBy(x => x%3).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectManyTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectManyTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectManyTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectManyTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectManyTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectManyTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectManyTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectManyTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectManyTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectManyTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectManyTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectManyTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectManyTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectManyTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectManyTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectManyTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_IndexedSelectManyTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_IndexedSelectManyTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SelectMany((x,i) => new[] { x,i }).Take(3).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectExceptTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Except(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectExceptTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).Except(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectExceptTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Except(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectExceptTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).Except(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectExceptTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Except(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectExceptTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Except(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectExceptTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Except(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectExceptTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).Except(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectExceptTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Except(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectExceptTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).Except(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectExceptTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Except(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectExceptTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Except(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectExceptTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Except(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectExceptTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).Except(Other()).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectExceptTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Except(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectExceptTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).Except(Other()).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectExceptTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Except(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectExceptTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).Except(Other()).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipWhileTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipWhileTake_Array_ToArray() {
        var src = A();
        return src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipWhileTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipWhileTake_Array_Sum() {
        var src = A();
        return new[] { src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipWhileTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipWhileTake_Array_Consume() {
        var src = A();
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipWhileTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipWhileTake_List_ToArray() {
        var src = new List<int>(A());
        return src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipWhileTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipWhileTake_List_Sum() {
        var src = new List<int>(A());
        return new[] { src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipWhileTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipWhileTake_List_Consume() {
        var src = new List<int>(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipWhileTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipWhileTake_Enumerable_ToArray() {
        var src = Lazy(A());
        return src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipWhileTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipWhileTake_Enumerable_Sum() {
        var src = Lazy(A());
        return new[] { src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Sum() };
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] Opt_SelectSkipWhileTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] Ref_SelectSkipWhileTake_Enumerable_Consume() {
        var src = Lazy(A());
        var consumer = new Consumer<int>();
        src.Select(x => F(x)).SkipWhile(x => x<30).Take(2).Consume(consumer);
        return consumer.Items.ToArray();
    }

    private static void Check(Func<int[]> optimized,Func<int[]> reference,string name) {
        var (optimizedResult,optimizedCalls,optimizedError) = Measure(optimized);
        var (referenceResult,referenceCalls,referenceError) = Measure(reference);
        Assert.Equal(referenceError,optimizedError);
        Assert.Equal(referenceResult,optimizedResult);
        Assert.True(optimizedCalls<=referenceCalls,$"{name}: 最適化あり {optimizedCalls} 回 > System.Linq {referenceCalls} 回");
    }

    [Fact(), Trait("Category","Unit")]
    public void WhereTake_Array_ToArray() => Check(() => Opt_WhereTake_Array_ToArray(),() => Ref_WhereTake_Array_ToArray(),"WhereTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereTake_Array_Sum() => Check(() => Opt_WhereTake_Array_Sum(),() => Ref_WhereTake_Array_Sum(),"WhereTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereTake_Array_Consume() => Check(() => Opt_WhereTake_Array_Consume(),() => Ref_WhereTake_Array_Consume(),"WhereTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereTake_List_ToArray() => Check(() => Opt_WhereTake_List_ToArray(),() => Ref_WhereTake_List_ToArray(),"WhereTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereTake_List_Sum() => Check(() => Opt_WhereTake_List_Sum(),() => Ref_WhereTake_List_Sum(),"WhereTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereTake_List_Consume() => Check(() => Opt_WhereTake_List_Consume(),() => Ref_WhereTake_List_Consume(),"WhereTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereTake_Enumerable_ToArray() => Check(() => Opt_WhereTake_Enumerable_ToArray(),() => Ref_WhereTake_Enumerable_ToArray(),"WhereTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereTake_Enumerable_Sum() => Check(() => Opt_WhereTake_Enumerable_Sum(),() => Ref_WhereTake_Enumerable_Sum(),"WhereTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereTake_Enumerable_Consume() => Check(() => Opt_WhereTake_Enumerable_Consume(),() => Ref_WhereTake_Enumerable_Consume(),"WhereTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereTakeZero_Array_ToArray() => Check(() => Opt_WhereTakeZero_Array_ToArray(),() => Ref_WhereTakeZero_Array_ToArray(),"WhereTakeZero_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereTakeZero_Array_Sum() => Check(() => Opt_WhereTakeZero_Array_Sum(),() => Ref_WhereTakeZero_Array_Sum(),"WhereTakeZero_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereTakeZero_Array_Consume() => Check(() => Opt_WhereTakeZero_Array_Consume(),() => Ref_WhereTakeZero_Array_Consume(),"WhereTakeZero_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereTakeZero_List_ToArray() => Check(() => Opt_WhereTakeZero_List_ToArray(),() => Ref_WhereTakeZero_List_ToArray(),"WhereTakeZero_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereTakeZero_List_Sum() => Check(() => Opt_WhereTakeZero_List_Sum(),() => Ref_WhereTakeZero_List_Sum(),"WhereTakeZero_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereTakeZero_List_Consume() => Check(() => Opt_WhereTakeZero_List_Consume(),() => Ref_WhereTakeZero_List_Consume(),"WhereTakeZero_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereTakeZero_Enumerable_ToArray() => Check(() => Opt_WhereTakeZero_Enumerable_ToArray(),() => Ref_WhereTakeZero_Enumerable_ToArray(),"WhereTakeZero_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereTakeZero_Enumerable_Sum() => Check(() => Opt_WhereTakeZero_Enumerable_Sum(),() => Ref_WhereTakeZero_Enumerable_Sum(),"WhereTakeZero_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereTakeZero_Enumerable_Consume() => Check(() => Opt_WhereTakeZero_Enumerable_Consume(),() => Ref_WhereTakeZero_Enumerable_Consume(),"WhereTakeZero_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeOne_Array_ToArray() => Check(() => Opt_SelectTakeOne_Array_ToArray(),() => Ref_SelectTakeOne_Array_ToArray(),"SelectTakeOne_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeOne_Array_Sum() => Check(() => Opt_SelectTakeOne_Array_Sum(),() => Ref_SelectTakeOne_Array_Sum(),"SelectTakeOne_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeOne_Array_Consume() => Check(() => Opt_SelectTakeOne_Array_Consume(),() => Ref_SelectTakeOne_Array_Consume(),"SelectTakeOne_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeOne_List_ToArray() => Check(() => Opt_SelectTakeOne_List_ToArray(),() => Ref_SelectTakeOne_List_ToArray(),"SelectTakeOne_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeOne_List_Sum() => Check(() => Opt_SelectTakeOne_List_Sum(),() => Ref_SelectTakeOne_List_Sum(),"SelectTakeOne_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeOne_List_Consume() => Check(() => Opt_SelectTakeOne_List_Consume(),() => Ref_SelectTakeOne_List_Consume(),"SelectTakeOne_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeOne_Enumerable_ToArray() => Check(() => Opt_SelectTakeOne_Enumerable_ToArray(),() => Ref_SelectTakeOne_Enumerable_ToArray(),"SelectTakeOne_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeOne_Enumerable_Sum() => Check(() => Opt_SelectTakeOne_Enumerable_Sum(),() => Ref_SelectTakeOne_Enumerable_Sum(),"SelectTakeOne_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeOne_Enumerable_Consume() => Check(() => Opt_SelectTakeOne_Enumerable_Consume(),() => Ref_SelectTakeOne_Enumerable_Consume(),"SelectTakeOne_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyTakeOne_Array_ToArray() => Check(() => Opt_SelectManyTakeOne_Array_ToArray(),() => Ref_SelectManyTakeOne_Array_ToArray(),"SelectManyTakeOne_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyTakeOne_Array_Sum() => Check(() => Opt_SelectManyTakeOne_Array_Sum(),() => Ref_SelectManyTakeOne_Array_Sum(),"SelectManyTakeOne_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyTakeOne_Array_Consume() => Check(() => Opt_SelectManyTakeOne_Array_Consume(),() => Ref_SelectManyTakeOne_Array_Consume(),"SelectManyTakeOne_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyTakeOne_List_ToArray() => Check(() => Opt_SelectManyTakeOne_List_ToArray(),() => Ref_SelectManyTakeOne_List_ToArray(),"SelectManyTakeOne_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyTakeOne_List_Sum() => Check(() => Opt_SelectManyTakeOne_List_Sum(),() => Ref_SelectManyTakeOne_List_Sum(),"SelectManyTakeOne_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyTakeOne_List_Consume() => Check(() => Opt_SelectManyTakeOne_List_Consume(),() => Ref_SelectManyTakeOne_List_Consume(),"SelectManyTakeOne_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyTakeOne_Enumerable_ToArray() => Check(() => Opt_SelectManyTakeOne_Enumerable_ToArray(),() => Ref_SelectManyTakeOne_Enumerable_ToArray(),"SelectManyTakeOne_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyTakeOne_Enumerable_Sum() => Check(() => Opt_SelectManyTakeOne_Enumerable_Sum(),() => Ref_SelectManyTakeOne_Enumerable_Sum(),"SelectManyTakeOne_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyTakeOne_Enumerable_Consume() => Check(() => Opt_SelectManyTakeOne_Enumerable_Consume(),() => Ref_SelectManyTakeOne_Enumerable_Consume(),"SelectManyTakeOne_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void ConcatTakeBoundary_Array_ToArray() => Check(() => Opt_ConcatTakeBoundary_Array_ToArray(),() => Ref_ConcatTakeBoundary_Array_ToArray(),"ConcatTakeBoundary_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void ConcatTakeBoundary_Array_Sum() => Check(() => Opt_ConcatTakeBoundary_Array_Sum(),() => Ref_ConcatTakeBoundary_Array_Sum(),"ConcatTakeBoundary_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void ConcatTakeBoundary_Array_Consume() => Check(() => Opt_ConcatTakeBoundary_Array_Consume(),() => Ref_ConcatTakeBoundary_Array_Consume(),"ConcatTakeBoundary_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void ConcatTakeBoundary_List_ToArray() => Check(() => Opt_ConcatTakeBoundary_List_ToArray(),() => Ref_ConcatTakeBoundary_List_ToArray(),"ConcatTakeBoundary_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void ConcatTakeBoundary_List_Sum() => Check(() => Opt_ConcatTakeBoundary_List_Sum(),() => Ref_ConcatTakeBoundary_List_Sum(),"ConcatTakeBoundary_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void ConcatTakeBoundary_List_Consume() => Check(() => Opt_ConcatTakeBoundary_List_Consume(),() => Ref_ConcatTakeBoundary_List_Consume(),"ConcatTakeBoundary_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void ConcatTakeBoundary_Enumerable_ToArray() => Check(() => Opt_ConcatTakeBoundary_Enumerable_ToArray(),() => Ref_ConcatTakeBoundary_Enumerable_ToArray(),"ConcatTakeBoundary_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void ConcatTakeBoundary_Enumerable_Sum() => Check(() => Opt_ConcatTakeBoundary_Enumerable_Sum(),() => Ref_ConcatTakeBoundary_Enumerable_Sum(),"ConcatTakeBoundary_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void ConcatTakeBoundary_Enumerable_Consume() => Check(() => Opt_ConcatTakeBoundary_Enumerable_Consume(),() => Ref_ConcatTakeBoundary_Enumerable_Consume(),"ConcatTakeBoundary_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTake_Array_ToArray() => Check(() => Opt_SelectTake_Array_ToArray(),() => Ref_SelectTake_Array_ToArray(),"SelectTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTake_Array_Sum() => Check(() => Opt_SelectTake_Array_Sum(),() => Ref_SelectTake_Array_Sum(),"SelectTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTake_Array_Consume() => Check(() => Opt_SelectTake_Array_Consume(),() => Ref_SelectTake_Array_Consume(),"SelectTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTake_List_ToArray() => Check(() => Opt_SelectTake_List_ToArray(),() => Ref_SelectTake_List_ToArray(),"SelectTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTake_List_Sum() => Check(() => Opt_SelectTake_List_Sum(),() => Ref_SelectTake_List_Sum(),"SelectTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTake_List_Consume() => Check(() => Opt_SelectTake_List_Consume(),() => Ref_SelectTake_List_Consume(),"SelectTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTake_Enumerable_ToArray() => Check(() => Opt_SelectTake_Enumerable_ToArray(),() => Ref_SelectTake_Enumerable_ToArray(),"SelectTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTake_Enumerable_Sum() => Check(() => Opt_SelectTake_Enumerable_Sum(),() => Ref_SelectTake_Enumerable_Sum(),"SelectTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTake_Enumerable_Consume() => Check(() => Opt_SelectTake_Enumerable_Consume(),() => Ref_SelectTake_Enumerable_Consume(),"SelectTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereSelectTake_Array_ToArray() => Check(() => Opt_WhereSelectTake_Array_ToArray(),() => Ref_WhereSelectTake_Array_ToArray(),"WhereSelectTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereSelectTake_Array_Sum() => Check(() => Opt_WhereSelectTake_Array_Sum(),() => Ref_WhereSelectTake_Array_Sum(),"WhereSelectTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereSelectTake_Array_Consume() => Check(() => Opt_WhereSelectTake_Array_Consume(),() => Ref_WhereSelectTake_Array_Consume(),"WhereSelectTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereSelectTake_List_ToArray() => Check(() => Opt_WhereSelectTake_List_ToArray(),() => Ref_WhereSelectTake_List_ToArray(),"WhereSelectTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereSelectTake_List_Sum() => Check(() => Opt_WhereSelectTake_List_Sum(),() => Ref_WhereSelectTake_List_Sum(),"WhereSelectTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereSelectTake_List_Consume() => Check(() => Opt_WhereSelectTake_List_Consume(),() => Ref_WhereSelectTake_List_Consume(),"WhereSelectTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereSelectTake_Enumerable_ToArray() => Check(() => Opt_WhereSelectTake_Enumerable_ToArray(),() => Ref_WhereSelectTake_Enumerable_ToArray(),"WhereSelectTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereSelectTake_Enumerable_Sum() => Check(() => Opt_WhereSelectTake_Enumerable_Sum(),() => Ref_WhereSelectTake_Enumerable_Sum(),"WhereSelectTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereSelectTake_Enumerable_Consume() => Check(() => Opt_WhereSelectTake_Enumerable_Consume(),() => Ref_WhereSelectTake_Enumerable_Consume(),"WhereSelectTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectWhereTake_Array_ToArray() => Check(() => Opt_SelectWhereTake_Array_ToArray(),() => Ref_SelectWhereTake_Array_ToArray(),"SelectWhereTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectWhereTake_Array_Sum() => Check(() => Opt_SelectWhereTake_Array_Sum(),() => Ref_SelectWhereTake_Array_Sum(),"SelectWhereTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectWhereTake_Array_Consume() => Check(() => Opt_SelectWhereTake_Array_Consume(),() => Ref_SelectWhereTake_Array_Consume(),"SelectWhereTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectWhereTake_List_ToArray() => Check(() => Opt_SelectWhereTake_List_ToArray(),() => Ref_SelectWhereTake_List_ToArray(),"SelectWhereTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectWhereTake_List_Sum() => Check(() => Opt_SelectWhereTake_List_Sum(),() => Ref_SelectWhereTake_List_Sum(),"SelectWhereTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectWhereTake_List_Consume() => Check(() => Opt_SelectWhereTake_List_Consume(),() => Ref_SelectWhereTake_List_Consume(),"SelectWhereTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectWhereTake_Enumerable_ToArray() => Check(() => Opt_SelectWhereTake_Enumerable_ToArray(),() => Ref_SelectWhereTake_Enumerable_ToArray(),"SelectWhereTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectWhereTake_Enumerable_Sum() => Check(() => Opt_SelectWhereTake_Enumerable_Sum(),() => Ref_SelectWhereTake_Enumerable_Sum(),"SelectWhereTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectWhereTake_Enumerable_Consume() => Check(() => Opt_SelectWhereTake_Enumerable_Consume(),() => Ref_SelectWhereTake_Enumerable_Consume(),"SelectWhereTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectThenSelectManyTake_Array_ToArray() => Check(() => Opt_SelectThenSelectManyTake_Array_ToArray(),() => Ref_SelectThenSelectManyTake_Array_ToArray(),"SelectThenSelectManyTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectThenSelectManyTake_Array_Sum() => Check(() => Opt_SelectThenSelectManyTake_Array_Sum(),() => Ref_SelectThenSelectManyTake_Array_Sum(),"SelectThenSelectManyTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectThenSelectManyTake_Array_Consume() => Check(() => Opt_SelectThenSelectManyTake_Array_Consume(),() => Ref_SelectThenSelectManyTake_Array_Consume(),"SelectThenSelectManyTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectThenSelectManyTake_List_ToArray() => Check(() => Opt_SelectThenSelectManyTake_List_ToArray(),() => Ref_SelectThenSelectManyTake_List_ToArray(),"SelectThenSelectManyTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectThenSelectManyTake_List_Sum() => Check(() => Opt_SelectThenSelectManyTake_List_Sum(),() => Ref_SelectThenSelectManyTake_List_Sum(),"SelectThenSelectManyTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectThenSelectManyTake_List_Consume() => Check(() => Opt_SelectThenSelectManyTake_List_Consume(),() => Ref_SelectThenSelectManyTake_List_Consume(),"SelectThenSelectManyTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectThenSelectManyTake_Enumerable_ToArray() => Check(() => Opt_SelectThenSelectManyTake_Enumerable_ToArray(),() => Ref_SelectThenSelectManyTake_Enumerable_ToArray(),"SelectThenSelectManyTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectThenSelectManyTake_Enumerable_Sum() => Check(() => Opt_SelectThenSelectManyTake_Enumerable_Sum(),() => Ref_SelectThenSelectManyTake_Enumerable_Sum(),"SelectThenSelectManyTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectThenSelectManyTake_Enumerable_Consume() => Check(() => Opt_SelectThenSelectManyTake_Enumerable_Consume(),() => Ref_SelectThenSelectManyTake_Enumerable_Consume(),"SelectThenSelectManyTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyThenSelectTake_Array_ToArray() => Check(() => Opt_SelectManyThenSelectTake_Array_ToArray(),() => Ref_SelectManyThenSelectTake_Array_ToArray(),"SelectManyThenSelectTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyThenSelectTake_Array_Sum() => Check(() => Opt_SelectManyThenSelectTake_Array_Sum(),() => Ref_SelectManyThenSelectTake_Array_Sum(),"SelectManyThenSelectTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyThenSelectTake_Array_Consume() => Check(() => Opt_SelectManyThenSelectTake_Array_Consume(),() => Ref_SelectManyThenSelectTake_Array_Consume(),"SelectManyThenSelectTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyThenSelectTake_List_ToArray() => Check(() => Opt_SelectManyThenSelectTake_List_ToArray(),() => Ref_SelectManyThenSelectTake_List_ToArray(),"SelectManyThenSelectTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyThenSelectTake_List_Sum() => Check(() => Opt_SelectManyThenSelectTake_List_Sum(),() => Ref_SelectManyThenSelectTake_List_Sum(),"SelectManyThenSelectTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyThenSelectTake_List_Consume() => Check(() => Opt_SelectManyThenSelectTake_List_Consume(),() => Ref_SelectManyThenSelectTake_List_Consume(),"SelectManyThenSelectTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyThenSelectTake_Enumerable_ToArray() => Check(() => Opt_SelectManyThenSelectTake_Enumerable_ToArray(),() => Ref_SelectManyThenSelectTake_Enumerable_ToArray(),"SelectManyThenSelectTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyThenSelectTake_Enumerable_Sum() => Check(() => Opt_SelectManyThenSelectTake_Enumerable_Sum(),() => Ref_SelectManyThenSelectTake_Enumerable_Sum(),"SelectManyThenSelectTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectManyThenSelectTake_Enumerable_Consume() => Check(() => Opt_SelectManyThenSelectTake_Enumerable_Consume(),() => Ref_SelectManyThenSelectTake_Enumerable_Consume(),"SelectManyThenSelectTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereThenSelectManyTake_Array_ToArray() => Check(() => Opt_WhereThenSelectManyTake_Array_ToArray(),() => Ref_WhereThenSelectManyTake_Array_ToArray(),"WhereThenSelectManyTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereThenSelectManyTake_Array_Sum() => Check(() => Opt_WhereThenSelectManyTake_Array_Sum(),() => Ref_WhereThenSelectManyTake_Array_Sum(),"WhereThenSelectManyTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereThenSelectManyTake_Array_Consume() => Check(() => Opt_WhereThenSelectManyTake_Array_Consume(),() => Ref_WhereThenSelectManyTake_Array_Consume(),"WhereThenSelectManyTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereThenSelectManyTake_List_ToArray() => Check(() => Opt_WhereThenSelectManyTake_List_ToArray(),() => Ref_WhereThenSelectManyTake_List_ToArray(),"WhereThenSelectManyTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereThenSelectManyTake_List_Sum() => Check(() => Opt_WhereThenSelectManyTake_List_Sum(),() => Ref_WhereThenSelectManyTake_List_Sum(),"WhereThenSelectManyTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereThenSelectManyTake_List_Consume() => Check(() => Opt_WhereThenSelectManyTake_List_Consume(),() => Ref_WhereThenSelectManyTake_List_Consume(),"WhereThenSelectManyTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereThenSelectManyTake_Enumerable_ToArray() => Check(() => Opt_WhereThenSelectManyTake_Enumerable_ToArray(),() => Ref_WhereThenSelectManyTake_Enumerable_ToArray(),"WhereThenSelectManyTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereThenSelectManyTake_Enumerable_Sum() => Check(() => Opt_WhereThenSelectManyTake_Enumerable_Sum(),() => Ref_WhereThenSelectManyTake_Enumerable_Sum(),"WhereThenSelectManyTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereThenSelectManyTake_Enumerable_Consume() => Check(() => Opt_WhereThenSelectManyTake_Enumerable_Consume(),() => Ref_WhereThenSelectManyTake_Enumerable_Consume(),"WhereThenSelectManyTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectConcatTake_Array_ToArray() => Check(() => Opt_SelectConcatTake_Array_ToArray(),() => Ref_SelectConcatTake_Array_ToArray(),"SelectConcatTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectConcatTake_Array_Sum() => Check(() => Opt_SelectConcatTake_Array_Sum(),() => Ref_SelectConcatTake_Array_Sum(),"SelectConcatTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectConcatTake_Array_Consume() => Check(() => Opt_SelectConcatTake_Array_Consume(),() => Ref_SelectConcatTake_Array_Consume(),"SelectConcatTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectConcatTake_List_ToArray() => Check(() => Opt_SelectConcatTake_List_ToArray(),() => Ref_SelectConcatTake_List_ToArray(),"SelectConcatTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectConcatTake_List_Sum() => Check(() => Opt_SelectConcatTake_List_Sum(),() => Ref_SelectConcatTake_List_Sum(),"SelectConcatTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectConcatTake_List_Consume() => Check(() => Opt_SelectConcatTake_List_Consume(),() => Ref_SelectConcatTake_List_Consume(),"SelectConcatTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectConcatTake_Enumerable_ToArray() => Check(() => Opt_SelectConcatTake_Enumerable_ToArray(),() => Ref_SelectConcatTake_Enumerable_ToArray(),"SelectConcatTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectConcatTake_Enumerable_Sum() => Check(() => Opt_SelectConcatTake_Enumerable_Sum(),() => Ref_SelectConcatTake_Enumerable_Sum(),"SelectConcatTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectConcatTake_Enumerable_Consume() => Check(() => Opt_SelectConcatTake_Enumerable_Consume(),() => Ref_SelectConcatTake_Enumerable_Consume(),"SelectConcatTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void ConcatSelectTake_Array_ToArray() => Check(() => Opt_ConcatSelectTake_Array_ToArray(),() => Ref_ConcatSelectTake_Array_ToArray(),"ConcatSelectTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void ConcatSelectTake_Array_Sum() => Check(() => Opt_ConcatSelectTake_Array_Sum(),() => Ref_ConcatSelectTake_Array_Sum(),"ConcatSelectTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void ConcatSelectTake_Array_Consume() => Check(() => Opt_ConcatSelectTake_Array_Consume(),() => Ref_ConcatSelectTake_Array_Consume(),"ConcatSelectTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void ConcatSelectTake_List_ToArray() => Check(() => Opt_ConcatSelectTake_List_ToArray(),() => Ref_ConcatSelectTake_List_ToArray(),"ConcatSelectTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void ConcatSelectTake_List_Sum() => Check(() => Opt_ConcatSelectTake_List_Sum(),() => Ref_ConcatSelectTake_List_Sum(),"ConcatSelectTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void ConcatSelectTake_List_Consume() => Check(() => Opt_ConcatSelectTake_List_Consume(),() => Ref_ConcatSelectTake_List_Consume(),"ConcatSelectTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void ConcatSelectTake_Enumerable_ToArray() => Check(() => Opt_ConcatSelectTake_Enumerable_ToArray(),() => Ref_ConcatSelectTake_Enumerable_ToArray(),"ConcatSelectTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void ConcatSelectTake_Enumerable_Sum() => Check(() => Opt_ConcatSelectTake_Enumerable_Sum(),() => Ref_ConcatSelectTake_Enumerable_Sum(),"ConcatSelectTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void ConcatSelectTake_Enumerable_Consume() => Check(() => Opt_ConcatSelectTake_Enumerable_Consume(),() => Ref_ConcatSelectTake_Enumerable_Consume(),"ConcatSelectTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereConcatWhereTake_Array_ToArray() => Check(() => Opt_WhereConcatWhereTake_Array_ToArray(),() => Ref_WhereConcatWhereTake_Array_ToArray(),"WhereConcatWhereTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereConcatWhereTake_Array_Sum() => Check(() => Opt_WhereConcatWhereTake_Array_Sum(),() => Ref_WhereConcatWhereTake_Array_Sum(),"WhereConcatWhereTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereConcatWhereTake_Array_Consume() => Check(() => Opt_WhereConcatWhereTake_Array_Consume(),() => Ref_WhereConcatWhereTake_Array_Consume(),"WhereConcatWhereTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereConcatWhereTake_List_ToArray() => Check(() => Opt_WhereConcatWhereTake_List_ToArray(),() => Ref_WhereConcatWhereTake_List_ToArray(),"WhereConcatWhereTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereConcatWhereTake_List_Sum() => Check(() => Opt_WhereConcatWhereTake_List_Sum(),() => Ref_WhereConcatWhereTake_List_Sum(),"WhereConcatWhereTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereConcatWhereTake_List_Consume() => Check(() => Opt_WhereConcatWhereTake_List_Consume(),() => Ref_WhereConcatWhereTake_List_Consume(),"WhereConcatWhereTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void WhereConcatWhereTake_Enumerable_ToArray() => Check(() => Opt_WhereConcatWhereTake_Enumerable_ToArray(),() => Ref_WhereConcatWhereTake_Enumerable_ToArray(),"WhereConcatWhereTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void WhereConcatWhereTake_Enumerable_Sum() => Check(() => Opt_WhereConcatWhereTake_Enumerable_Sum(),() => Ref_WhereConcatWhereTake_Enumerable_Sum(),"WhereConcatWhereTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void WhereConcatWhereTake_Enumerable_Consume() => Check(() => Opt_WhereConcatWhereTake_Enumerable_Consume(),() => Ref_WhereConcatWhereTake_Enumerable_Consume(),"WhereConcatWhereTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectTake_Array_ToArray() => Check(() => Opt_IndexedSelectTake_Array_ToArray(),() => Ref_IndexedSelectTake_Array_ToArray(),"IndexedSelectTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectTake_Array_Sum() => Check(() => Opt_IndexedSelectTake_Array_Sum(),() => Ref_IndexedSelectTake_Array_Sum(),"IndexedSelectTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectTake_Array_Consume() => Check(() => Opt_IndexedSelectTake_Array_Consume(),() => Ref_IndexedSelectTake_Array_Consume(),"IndexedSelectTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectTake_List_ToArray() => Check(() => Opt_IndexedSelectTake_List_ToArray(),() => Ref_IndexedSelectTake_List_ToArray(),"IndexedSelectTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectTake_List_Sum() => Check(() => Opt_IndexedSelectTake_List_Sum(),() => Ref_IndexedSelectTake_List_Sum(),"IndexedSelectTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectTake_List_Consume() => Check(() => Opt_IndexedSelectTake_List_Consume(),() => Ref_IndexedSelectTake_List_Consume(),"IndexedSelectTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectTake_Enumerable_ToArray() => Check(() => Opt_IndexedSelectTake_Enumerable_ToArray(),() => Ref_IndexedSelectTake_Enumerable_ToArray(),"IndexedSelectTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectTake_Enumerable_Sum() => Check(() => Opt_IndexedSelectTake_Enumerable_Sum(),() => Ref_IndexedSelectTake_Enumerable_Sum(),"IndexedSelectTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectTake_Enumerable_Consume() => Check(() => Opt_IndexedSelectTake_Enumerable_Consume(),() => Ref_IndexedSelectTake_Enumerable_Consume(),"IndexedSelectTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void IndexedWhereTake_Array_ToArray() => Check(() => Opt_IndexedWhereTake_Array_ToArray(),() => Ref_IndexedWhereTake_Array_ToArray(),"IndexedWhereTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void IndexedWhereTake_Array_Sum() => Check(() => Opt_IndexedWhereTake_Array_Sum(),() => Ref_IndexedWhereTake_Array_Sum(),"IndexedWhereTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void IndexedWhereTake_Array_Consume() => Check(() => Opt_IndexedWhereTake_Array_Consume(),() => Ref_IndexedWhereTake_Array_Consume(),"IndexedWhereTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void IndexedWhereTake_List_ToArray() => Check(() => Opt_IndexedWhereTake_List_ToArray(),() => Ref_IndexedWhereTake_List_ToArray(),"IndexedWhereTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void IndexedWhereTake_List_Sum() => Check(() => Opt_IndexedWhereTake_List_Sum(),() => Ref_IndexedWhereTake_List_Sum(),"IndexedWhereTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void IndexedWhereTake_List_Consume() => Check(() => Opt_IndexedWhereTake_List_Consume(),() => Ref_IndexedWhereTake_List_Consume(),"IndexedWhereTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void IndexedWhereTake_Enumerable_ToArray() => Check(() => Opt_IndexedWhereTake_Enumerable_ToArray(),() => Ref_IndexedWhereTake_Enumerable_ToArray(),"IndexedWhereTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void IndexedWhereTake_Enumerable_Sum() => Check(() => Opt_IndexedWhereTake_Enumerable_Sum(),() => Ref_IndexedWhereTake_Enumerable_Sum(),"IndexedWhereTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void IndexedWhereTake_Enumerable_Consume() => Check(() => Opt_IndexedWhereTake_Enumerable_Consume(),() => Ref_IndexedWhereTake_Enumerable_Consume(),"IndexedWhereTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipTake_Array_ToArray() => Check(() => Opt_SelectSkipTake_Array_ToArray(),() => Ref_SelectSkipTake_Array_ToArray(),"SelectSkipTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipTake_Array_Sum() => Check(() => Opt_SelectSkipTake_Array_Sum(),() => Ref_SelectSkipTake_Array_Sum(),"SelectSkipTake_Array_Sum");

    [Fact(Skip = "System.Linq は配列・List の Select.Skip で、飛ばす要素の選択子を呼ばない(FLinq の Consume の経路は呼ぶ)。Take の件とは別の差で、未対応。"), Trait("Category","Unit")]
    public void SelectSkipTake_Array_Consume() => Check(() => Opt_SelectSkipTake_Array_Consume(),() => Ref_SelectSkipTake_Array_Consume(),"SelectSkipTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipTake_List_ToArray() => Check(() => Opt_SelectSkipTake_List_ToArray(),() => Ref_SelectSkipTake_List_ToArray(),"SelectSkipTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipTake_List_Sum() => Check(() => Opt_SelectSkipTake_List_Sum(),() => Ref_SelectSkipTake_List_Sum(),"SelectSkipTake_List_Sum");

    [Fact(Skip = "System.Linq は配列・List の Select.Skip で、飛ばす要素の選択子を呼ばない(FLinq の Consume の経路は呼ぶ)。Take の件とは別の差で、未対応。"), Trait("Category","Unit")]
    public void SelectSkipTake_List_Consume() => Check(() => Opt_SelectSkipTake_List_Consume(),() => Ref_SelectSkipTake_List_Consume(),"SelectSkipTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipTake_Enumerable_ToArray() => Check(() => Opt_SelectSkipTake_Enumerable_ToArray(),() => Ref_SelectSkipTake_Enumerable_ToArray(),"SelectSkipTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipTake_Enumerable_Sum() => Check(() => Opt_SelectSkipTake_Enumerable_Sum(),() => Ref_SelectSkipTake_Enumerable_Sum(),"SelectSkipTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipTake_Enumerable_Consume() => Check(() => Opt_SelectSkipTake_Enumerable_Consume(),() => Ref_SelectSkipTake_Enumerable_Consume(),"SelectSkipTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctTake_Array_ToArray() => Check(() => Opt_SelectDistinctTake_Array_ToArray(),() => Ref_SelectDistinctTake_Array_ToArray(),"SelectDistinctTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctTake_Array_Sum() => Check(() => Opt_SelectDistinctTake_Array_Sum(),() => Ref_SelectDistinctTake_Array_Sum(),"SelectDistinctTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctTake_Array_Consume() => Check(() => Opt_SelectDistinctTake_Array_Consume(),() => Ref_SelectDistinctTake_Array_Consume(),"SelectDistinctTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctTake_List_ToArray() => Check(() => Opt_SelectDistinctTake_List_ToArray(),() => Ref_SelectDistinctTake_List_ToArray(),"SelectDistinctTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctTake_List_Sum() => Check(() => Opt_SelectDistinctTake_List_Sum(),() => Ref_SelectDistinctTake_List_Sum(),"SelectDistinctTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctTake_List_Consume() => Check(() => Opt_SelectDistinctTake_List_Consume(),() => Ref_SelectDistinctTake_List_Consume(),"SelectDistinctTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctTake_Enumerable_ToArray() => Check(() => Opt_SelectDistinctTake_Enumerable_ToArray(),() => Ref_SelectDistinctTake_Enumerable_ToArray(),"SelectDistinctTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctTake_Enumerable_Sum() => Check(() => Opt_SelectDistinctTake_Enumerable_Sum(),() => Ref_SelectDistinctTake_Enumerable_Sum(),"SelectDistinctTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctTake_Enumerable_Consume() => Check(() => Opt_SelectDistinctTake_Enumerable_Consume(),() => Ref_SelectDistinctTake_Enumerable_Consume(),"SelectDistinctTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectAppendTake_Array_ToArray() => Check(() => Opt_SelectAppendTake_Array_ToArray(),() => Ref_SelectAppendTake_Array_ToArray(),"SelectAppendTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectAppendTake_Array_Sum() => Check(() => Opt_SelectAppendTake_Array_Sum(),() => Ref_SelectAppendTake_Array_Sum(),"SelectAppendTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectAppendTake_Array_Consume() => Check(() => Opt_SelectAppendTake_Array_Consume(),() => Ref_SelectAppendTake_Array_Consume(),"SelectAppendTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectAppendTake_List_ToArray() => Check(() => Opt_SelectAppendTake_List_ToArray(),() => Ref_SelectAppendTake_List_ToArray(),"SelectAppendTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectAppendTake_List_Sum() => Check(() => Opt_SelectAppendTake_List_Sum(),() => Ref_SelectAppendTake_List_Sum(),"SelectAppendTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectAppendTake_List_Consume() => Check(() => Opt_SelectAppendTake_List_Consume(),() => Ref_SelectAppendTake_List_Consume(),"SelectAppendTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectAppendTake_Enumerable_ToArray() => Check(() => Opt_SelectAppendTake_Enumerable_ToArray(),() => Ref_SelectAppendTake_Enumerable_ToArray(),"SelectAppendTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectAppendTake_Enumerable_Sum() => Check(() => Opt_SelectAppendTake_Enumerable_Sum(),() => Ref_SelectAppendTake_Enumerable_Sum(),"SelectAppendTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectAppendTake_Enumerable_Consume() => Check(() => Opt_SelectAppendTake_Enumerable_Consume(),() => Ref_SelectAppendTake_Enumerable_Consume(),"SelectAppendTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectPrependTake_Array_ToArray() => Check(() => Opt_SelectPrependTake_Array_ToArray(),() => Ref_SelectPrependTake_Array_ToArray(),"SelectPrependTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectPrependTake_Array_Sum() => Check(() => Opt_SelectPrependTake_Array_Sum(),() => Ref_SelectPrependTake_Array_Sum(),"SelectPrependTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectPrependTake_Array_Consume() => Check(() => Opt_SelectPrependTake_Array_Consume(),() => Ref_SelectPrependTake_Array_Consume(),"SelectPrependTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectPrependTake_List_ToArray() => Check(() => Opt_SelectPrependTake_List_ToArray(),() => Ref_SelectPrependTake_List_ToArray(),"SelectPrependTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectPrependTake_List_Sum() => Check(() => Opt_SelectPrependTake_List_Sum(),() => Ref_SelectPrependTake_List_Sum(),"SelectPrependTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectPrependTake_List_Consume() => Check(() => Opt_SelectPrependTake_List_Consume(),() => Ref_SelectPrependTake_List_Consume(),"SelectPrependTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectPrependTake_Enumerable_ToArray() => Check(() => Opt_SelectPrependTake_Enumerable_ToArray(),() => Ref_SelectPrependTake_Enumerable_ToArray(),"SelectPrependTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectPrependTake_Enumerable_Sum() => Check(() => Opt_SelectPrependTake_Enumerable_Sum(),() => Ref_SelectPrependTake_Enumerable_Sum(),"SelectPrependTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectPrependTake_Enumerable_Consume() => Check(() => Opt_SelectPrependTake_Enumerable_Consume(),() => Ref_SelectPrependTake_Enumerable_Consume(),"SelectPrependTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void ZipTake_Array_ToArray() => Check(() => Opt_ZipTake_Array_ToArray(),() => Ref_ZipTake_Array_ToArray(),"ZipTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void ZipTake_Array_Sum() => Check(() => Opt_ZipTake_Array_Sum(),() => Ref_ZipTake_Array_Sum(),"ZipTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void ZipTake_Array_Consume() => Check(() => Opt_ZipTake_Array_Consume(),() => Ref_ZipTake_Array_Consume(),"ZipTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void ZipTake_List_ToArray() => Check(() => Opt_ZipTake_List_ToArray(),() => Ref_ZipTake_List_ToArray(),"ZipTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void ZipTake_List_Sum() => Check(() => Opt_ZipTake_List_Sum(),() => Ref_ZipTake_List_Sum(),"ZipTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void ZipTake_List_Consume() => Check(() => Opt_ZipTake_List_Consume(),() => Ref_ZipTake_List_Consume(),"ZipTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void ZipTake_Enumerable_ToArray() => Check(() => Opt_ZipTake_Enumerable_ToArray(),() => Ref_ZipTake_Enumerable_ToArray(),"ZipTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void ZipTake_Enumerable_Sum() => Check(() => Opt_ZipTake_Enumerable_Sum(),() => Ref_ZipTake_Enumerable_Sum(),"ZipTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void ZipTake_Enumerable_Consume() => Check(() => Opt_ZipTake_Enumerable_Consume(),() => Ref_ZipTake_Enumerable_Consume(),"ZipTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeWhileTake_Array_ToArray() => Check(() => Opt_SelectTakeWhileTake_Array_ToArray(),() => Ref_SelectTakeWhileTake_Array_ToArray(),"SelectTakeWhileTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeWhileTake_Array_Sum() => Check(() => Opt_SelectTakeWhileTake_Array_Sum(),() => Ref_SelectTakeWhileTake_Array_Sum(),"SelectTakeWhileTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeWhileTake_Array_Consume() => Check(() => Opt_SelectTakeWhileTake_Array_Consume(),() => Ref_SelectTakeWhileTake_Array_Consume(),"SelectTakeWhileTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeWhileTake_List_ToArray() => Check(() => Opt_SelectTakeWhileTake_List_ToArray(),() => Ref_SelectTakeWhileTake_List_ToArray(),"SelectTakeWhileTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeWhileTake_List_Sum() => Check(() => Opt_SelectTakeWhileTake_List_Sum(),() => Ref_SelectTakeWhileTake_List_Sum(),"SelectTakeWhileTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeWhileTake_List_Consume() => Check(() => Opt_SelectTakeWhileTake_List_Consume(),() => Ref_SelectTakeWhileTake_List_Consume(),"SelectTakeWhileTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeWhileTake_Enumerable_ToArray() => Check(() => Opt_SelectTakeWhileTake_Enumerable_ToArray(),() => Ref_SelectTakeWhileTake_Enumerable_ToArray(),"SelectTakeWhileTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeWhileTake_Enumerable_Sum() => Check(() => Opt_SelectTakeWhileTake_Enumerable_Sum(),() => Ref_SelectTakeWhileTake_Enumerable_Sum(),"SelectTakeWhileTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeWhileTake_Enumerable_Consume() => Check(() => Opt_SelectTakeWhileTake_Enumerable_Consume(),() => Ref_SelectTakeWhileTake_Enumerable_Consume(),"SelectTakeWhileTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeSelectTake_Array_ToArray() => Check(() => Opt_SelectTakeSelectTake_Array_ToArray(),() => Ref_SelectTakeSelectTake_Array_ToArray(),"SelectTakeSelectTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeSelectTake_Array_Sum() => Check(() => Opt_SelectTakeSelectTake_Array_Sum(),() => Ref_SelectTakeSelectTake_Array_Sum(),"SelectTakeSelectTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeSelectTake_Array_Consume() => Check(() => Opt_SelectTakeSelectTake_Array_Consume(),() => Ref_SelectTakeSelectTake_Array_Consume(),"SelectTakeSelectTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeSelectTake_List_ToArray() => Check(() => Opt_SelectTakeSelectTake_List_ToArray(),() => Ref_SelectTakeSelectTake_List_ToArray(),"SelectTakeSelectTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeSelectTake_List_Sum() => Check(() => Opt_SelectTakeSelectTake_List_Sum(),() => Ref_SelectTakeSelectTake_List_Sum(),"SelectTakeSelectTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeSelectTake_List_Consume() => Check(() => Opt_SelectTakeSelectTake_List_Consume(),() => Ref_SelectTakeSelectTake_List_Consume(),"SelectTakeSelectTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeSelectTake_Enumerable_ToArray() => Check(() => Opt_SelectTakeSelectTake_Enumerable_ToArray(),() => Ref_SelectTakeSelectTake_Enumerable_ToArray(),"SelectTakeSelectTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeSelectTake_Enumerable_Sum() => Check(() => Opt_SelectTakeSelectTake_Enumerable_Sum(),() => Ref_SelectTakeSelectTake_Enumerable_Sum(),"SelectTakeSelectTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectTakeSelectTake_Enumerable_Consume() => Check(() => Opt_SelectTakeSelectTake_Enumerable_Consume(),() => Ref_SelectTakeSelectTake_Enumerable_Consume(),"SelectTakeSelectTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectUnionTake_Array_ToArray() => Check(() => Opt_SelectUnionTake_Array_ToArray(),() => Ref_SelectUnionTake_Array_ToArray(),"SelectUnionTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectUnionTake_Array_Sum() => Check(() => Opt_SelectUnionTake_Array_Sum(),() => Ref_SelectUnionTake_Array_Sum(),"SelectUnionTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectUnionTake_Array_Consume() => Check(() => Opt_SelectUnionTake_Array_Consume(),() => Ref_SelectUnionTake_Array_Consume(),"SelectUnionTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectUnionTake_List_ToArray() => Check(() => Opt_SelectUnionTake_List_ToArray(),() => Ref_SelectUnionTake_List_ToArray(),"SelectUnionTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectUnionTake_List_Sum() => Check(() => Opt_SelectUnionTake_List_Sum(),() => Ref_SelectUnionTake_List_Sum(),"SelectUnionTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectUnionTake_List_Consume() => Check(() => Opt_SelectUnionTake_List_Consume(),() => Ref_SelectUnionTake_List_Consume(),"SelectUnionTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectUnionTake_Enumerable_ToArray() => Check(() => Opt_SelectUnionTake_Enumerable_ToArray(),() => Ref_SelectUnionTake_Enumerable_ToArray(),"SelectUnionTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectUnionTake_Enumerable_Sum() => Check(() => Opt_SelectUnionTake_Enumerable_Sum(),() => Ref_SelectUnionTake_Enumerable_Sum(),"SelectUnionTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectUnionTake_Enumerable_Consume() => Check(() => Opt_SelectUnionTake_Enumerable_Consume(),() => Ref_SelectUnionTake_Enumerable_Consume(),"SelectUnionTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctByTake_Array_ToArray() => Check(() => Opt_SelectDistinctByTake_Array_ToArray(),() => Ref_SelectDistinctByTake_Array_ToArray(),"SelectDistinctByTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctByTake_Array_Sum() => Check(() => Opt_SelectDistinctByTake_Array_Sum(),() => Ref_SelectDistinctByTake_Array_Sum(),"SelectDistinctByTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctByTake_Array_Consume() => Check(() => Opt_SelectDistinctByTake_Array_Consume(),() => Ref_SelectDistinctByTake_Array_Consume(),"SelectDistinctByTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctByTake_List_ToArray() => Check(() => Opt_SelectDistinctByTake_List_ToArray(),() => Ref_SelectDistinctByTake_List_ToArray(),"SelectDistinctByTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctByTake_List_Sum() => Check(() => Opt_SelectDistinctByTake_List_Sum(),() => Ref_SelectDistinctByTake_List_Sum(),"SelectDistinctByTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctByTake_List_Consume() => Check(() => Opt_SelectDistinctByTake_List_Consume(),() => Ref_SelectDistinctByTake_List_Consume(),"SelectDistinctByTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctByTake_Enumerable_ToArray() => Check(() => Opt_SelectDistinctByTake_Enumerable_ToArray(),() => Ref_SelectDistinctByTake_Enumerable_ToArray(),"SelectDistinctByTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctByTake_Enumerable_Sum() => Check(() => Opt_SelectDistinctByTake_Enumerable_Sum(),() => Ref_SelectDistinctByTake_Enumerable_Sum(),"SelectDistinctByTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectDistinctByTake_Enumerable_Consume() => Check(() => Opt_SelectDistinctByTake_Enumerable_Consume(),() => Ref_SelectDistinctByTake_Enumerable_Consume(),"SelectDistinctByTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectManyTake_Array_ToArray() => Check(() => Opt_IndexedSelectManyTake_Array_ToArray(),() => Ref_IndexedSelectManyTake_Array_ToArray(),"IndexedSelectManyTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectManyTake_Array_Sum() => Check(() => Opt_IndexedSelectManyTake_Array_Sum(),() => Ref_IndexedSelectManyTake_Array_Sum(),"IndexedSelectManyTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectManyTake_Array_Consume() => Check(() => Opt_IndexedSelectManyTake_Array_Consume(),() => Ref_IndexedSelectManyTake_Array_Consume(),"IndexedSelectManyTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectManyTake_List_ToArray() => Check(() => Opt_IndexedSelectManyTake_List_ToArray(),() => Ref_IndexedSelectManyTake_List_ToArray(),"IndexedSelectManyTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectManyTake_List_Sum() => Check(() => Opt_IndexedSelectManyTake_List_Sum(),() => Ref_IndexedSelectManyTake_List_Sum(),"IndexedSelectManyTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectManyTake_List_Consume() => Check(() => Opt_IndexedSelectManyTake_List_Consume(),() => Ref_IndexedSelectManyTake_List_Consume(),"IndexedSelectManyTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectManyTake_Enumerable_ToArray() => Check(() => Opt_IndexedSelectManyTake_Enumerable_ToArray(),() => Ref_IndexedSelectManyTake_Enumerable_ToArray(),"IndexedSelectManyTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectManyTake_Enumerable_Sum() => Check(() => Opt_IndexedSelectManyTake_Enumerable_Sum(),() => Ref_IndexedSelectManyTake_Enumerable_Sum(),"IndexedSelectManyTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void IndexedSelectManyTake_Enumerable_Consume() => Check(() => Opt_IndexedSelectManyTake_Enumerable_Consume(),() => Ref_IndexedSelectManyTake_Enumerable_Consume(),"IndexedSelectManyTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectExceptTake_Array_ToArray() => Check(() => Opt_SelectExceptTake_Array_ToArray(),() => Ref_SelectExceptTake_Array_ToArray(),"SelectExceptTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectExceptTake_Array_Sum() => Check(() => Opt_SelectExceptTake_Array_Sum(),() => Ref_SelectExceptTake_Array_Sum(),"SelectExceptTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectExceptTake_Array_Consume() => Check(() => Opt_SelectExceptTake_Array_Consume(),() => Ref_SelectExceptTake_Array_Consume(),"SelectExceptTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectExceptTake_List_ToArray() => Check(() => Opt_SelectExceptTake_List_ToArray(),() => Ref_SelectExceptTake_List_ToArray(),"SelectExceptTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectExceptTake_List_Sum() => Check(() => Opt_SelectExceptTake_List_Sum(),() => Ref_SelectExceptTake_List_Sum(),"SelectExceptTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectExceptTake_List_Consume() => Check(() => Opt_SelectExceptTake_List_Consume(),() => Ref_SelectExceptTake_List_Consume(),"SelectExceptTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectExceptTake_Enumerable_ToArray() => Check(() => Opt_SelectExceptTake_Enumerable_ToArray(),() => Ref_SelectExceptTake_Enumerable_ToArray(),"SelectExceptTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectExceptTake_Enumerable_Sum() => Check(() => Opt_SelectExceptTake_Enumerable_Sum(),() => Ref_SelectExceptTake_Enumerable_Sum(),"SelectExceptTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectExceptTake_Enumerable_Consume() => Check(() => Opt_SelectExceptTake_Enumerable_Consume(),() => Ref_SelectExceptTake_Enumerable_Consume(),"SelectExceptTake_Enumerable_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipWhileTake_Array_ToArray() => Check(() => Opt_SelectSkipWhileTake_Array_ToArray(),() => Ref_SelectSkipWhileTake_Array_ToArray(),"SelectSkipWhileTake_Array_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipWhileTake_Array_Sum() => Check(() => Opt_SelectSkipWhileTake_Array_Sum(),() => Ref_SelectSkipWhileTake_Array_Sum(),"SelectSkipWhileTake_Array_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipWhileTake_Array_Consume() => Check(() => Opt_SelectSkipWhileTake_Array_Consume(),() => Ref_SelectSkipWhileTake_Array_Consume(),"SelectSkipWhileTake_Array_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipWhileTake_List_ToArray() => Check(() => Opt_SelectSkipWhileTake_List_ToArray(),() => Ref_SelectSkipWhileTake_List_ToArray(),"SelectSkipWhileTake_List_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipWhileTake_List_Sum() => Check(() => Opt_SelectSkipWhileTake_List_Sum(),() => Ref_SelectSkipWhileTake_List_Sum(),"SelectSkipWhileTake_List_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipWhileTake_List_Consume() => Check(() => Opt_SelectSkipWhileTake_List_Consume(),() => Ref_SelectSkipWhileTake_List_Consume(),"SelectSkipWhileTake_List_Consume");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipWhileTake_Enumerable_ToArray() => Check(() => Opt_SelectSkipWhileTake_Enumerable_ToArray(),() => Ref_SelectSkipWhileTake_Enumerable_ToArray(),"SelectSkipWhileTake_Enumerable_ToArray");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipWhileTake_Enumerable_Sum() => Check(() => Opt_SelectSkipWhileTake_Enumerable_Sum(),() => Ref_SelectSkipWhileTake_Enumerable_Sum(),"SelectSkipWhileTake_Enumerable_Sum");

    [Fact(), Trait("Category","Unit")]
    public void SelectSkipWhileTake_Enumerable_Consume() => Check(() => Opt_SelectSkipWhileTake_Enumerable_Consume(),() => Ref_SelectSkipWhileTake_Enumerable_Consume(),"SelectSkipWhileTake_Enumerable_Consume");

}

using System;
using System.Linq;

namespace Test.System.Linq.Intercept.Optimization;

public sealed class OrderByFirstToMinMax:OptimizationTestBase{
    private static Item[] EmptyItems() => [];

    [Fact,Trait("Category","Unit")]
    public static void OrderBy_First() {
        Assert.Equal(Numbers().OrderBy(static x => x).First(),OrderBy_First_IntOptimized());
        Assert.Equal(Numbers().OrderBy(static x => x).First(),OrderBy_First_IntNoOptimized());
        Assert.Equal(Points().OrderBy(static p => p.X).First(),OrderBy_First_PointOptimized());
        Assert.Equal(Points().OrderBy(static p => p.X).First(),OrderBy_First_PointNoOptimized());
        Assert.Throws<InvalidOperationException>(static () => EmptyNumbers().OrderBy(static x => x).First());
        Assert.Throws<InvalidOperationException>(static () => OrderBy_First_EmptyOptimized());
        Assert.Throws<InvalidOperationException>(static () => OrderBy_First_EmptyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int OrderBy_First_IntOptimized() => Numbers().OrderBy(static x => x).First();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int OrderBy_First_IntNoOptimized() => Numbers().OrderBy(static x => x).First();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Point OrderBy_First_PointOptimized() => Points().OrderBy(static p => p.X).First();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Point OrderBy_First_PointNoOptimized() => Points().OrderBy(static p => p.X).First();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int OrderBy_First_EmptyOptimized() => EmptyNumbers().OrderBy(static x => x).First();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int OrderBy_First_EmptyNoOptimized() => EmptyNumbers().OrderBy(static x => x).First();

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_First() {
        Assert.Equal(Numbers().OrderByDescending(static x => x).First(),OrderByDescending_First_IntOptimized());
        Assert.Equal(Numbers().OrderByDescending(static x => x).First(),OrderByDescending_First_IntNoOptimized());
        Assert.Equal(Points().OrderByDescending(static p => p.X).First(),OrderByDescending_First_PointOptimized());
        Assert.Equal(Points().OrderByDescending(static p => p.X).First(),OrderByDescending_First_PointNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int OrderByDescending_First_IntOptimized() => Numbers().OrderByDescending(static x => x).First();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int OrderByDescending_First_IntNoOptimized() => Numbers().OrderByDescending(static x => x).First();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Point OrderByDescending_First_PointOptimized() => Points().OrderByDescending(static p => p.X).First();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Point OrderByDescending_First_PointNoOptimized() => Points().OrderByDescending(static p => p.X).First();

    [Fact,Trait("Category","Unit")]
    public static void OrderBy_FirstOrDefault() {
        Assert.Equal(Outers().OrderBy(static i => i.Key).FirstOrDefault(),OrderBy_FirstOrDefault_ItemOptimized());
        Assert.Equal(Outers().OrderBy(static i => i.Key).FirstOrDefault(),OrderBy_FirstOrDefault_ItemNoOptimized());
        Assert.Equal(EmptyItems().OrderBy(static i => i.Key).FirstOrDefault(),OrderBy_FirstOrDefault_EmptyItemOptimized());
        Assert.Equal(EmptyItems().OrderBy(static i => i.Key).FirstOrDefault(),OrderBy_FirstOrDefault_EmptyItemNoOptimized());
        Assert.Equal(EmptyNumbers().OrderBy(static x => x).FirstOrDefault(),OrderBy_FirstOrDefault_EmptyIntOptimized());
        Assert.Equal(EmptyNumbers().OrderBy(static x => x).FirstOrDefault(),OrderBy_FirstOrDefault_EmptyIntNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Item? OrderBy_FirstOrDefault_ItemOptimized() => Outers().OrderBy(static i => i.Key).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Item? OrderBy_FirstOrDefault_ItemNoOptimized() => Outers().OrderBy(static i => i.Key).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Item? OrderBy_FirstOrDefault_EmptyItemOptimized() => EmptyItems().OrderBy(static i => i.Key).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Item? OrderBy_FirstOrDefault_EmptyItemNoOptimized() => EmptyItems().OrderBy(static i => i.Key).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int OrderBy_FirstOrDefault_EmptyIntOptimized() => EmptyNumbers().OrderBy(static x => x).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int OrderBy_FirstOrDefault_EmptyIntNoOptimized() => EmptyNumbers().OrderBy(static x => x).FirstOrDefault();

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_FirstOrDefault() {
        Assert.Equal(Outers().OrderByDescending(static i => i.Key).FirstOrDefault(),OrderByDescending_FirstOrDefault_ItemOptimized());
        Assert.Equal(Outers().OrderByDescending(static i => i.Key).FirstOrDefault(),OrderByDescending_FirstOrDefault_ItemNoOptimized());
        Assert.Equal(EmptyItems().OrderByDescending(static i => i.Key).FirstOrDefault(),OrderByDescending_FirstOrDefault_EmptyItemOptimized());
        Assert.Equal(EmptyItems().OrderByDescending(static i => i.Key).FirstOrDefault(),OrderByDescending_FirstOrDefault_EmptyItemNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Item? OrderByDescending_FirstOrDefault_ItemOptimized() => Outers().OrderByDescending(static i => i.Key).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Item? OrderByDescending_FirstOrDefault_ItemNoOptimized() => Outers().OrderByDescending(static i => i.Key).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Item? OrderByDescending_FirstOrDefault_EmptyItemOptimized() => EmptyItems().OrderByDescending(static i => i.Key).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static Item? OrderByDescending_FirstOrDefault_EmptyItemNoOptimized() => EmptyItems().OrderByDescending(static i => i.Key).FirstOrDefault();

    [Fact,Trait("Category","Unit")]
    public static void ReferenceFirst() {
        Assert.Equal(Words().OrderBy(static w => w.Length).First(),ReferenceFirstOptimized());
        Assert.Equal(Words().OrderBy(static w => w.Length).First(),ReferenceFirstNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static string ReferenceFirstOptimized() => Words().OrderBy(static w => w.Length).First();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static string ReferenceFirstNoOptimized() => Words().OrderBy(static w => w.Length).First();
}

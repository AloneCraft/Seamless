using Seamless;
using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Diagnostics;

public class InterceptorSmokeTests {
    [Fact]public void Array_GroupBy_keySelector__Select_selector() {
        var intercept = new int[3].GroupBy(p=>p/2).Select(static p => new { p });
    }
    [Fact]
    public void ListWhere_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };
        foreach(var a in numbers.Where(n=>n>2)){

        }
        var filtered = numbers.Where(n => n>2).ToArray();

        Assert.Equal(new[] { 3,4,5 },filtered);
    }

    [Fact]
    public void ListWhere_Index_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = numbers.Where((value,index) => index>=3).ToArray();

        Assert.Equal(new[] { 4,5 },filtered);
    }

    [Fact]
    public void QuerySyntax_Where_Helper_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };
        var f0 = new List<int> { 1,2,3,4,5 }.Where(value => value>2).Select(value => value).ToArray();
        var f1 = numbers.Where(value => value>2).Select(value => value).ToArray();
        //var filtered = from value in numbers
        //               where value>2
        //               select value;

        Assert.Equal(new[] { 3,4,5 },(from value in numbers
            where value>2
            select value).ToArray());
    }

    [Fact]
    public void QuerySyntax_Where_With_Projection_Smoke() {
        var filtered = (from value in global::Test.Seamless.Diagnostics.QuerySyntaxSmokeSource.Numbers
                        where value>2
                        select value*10).ToArray();

        Assert.Equal(new[] { 30,40,50 },filtered);
    }

    [Fact]
    public void QuerySyntax_Where_With_Projection_Generated_Source_Smoke() {
        var section = GeneratedInterceptorSection(nameof(QuerySyntax_Where_With_Projection_Smoke));

        Assert.Contains("this global::System.Collections.Generic.IEnumerable<global::System.Int32> source",section);
        Assert.Contains("foreach(var item in source)",section);
        Assert.Contains("result.Add(item)",section);
        Assert.Contains("return result.ToArray()",section);
        Assert.DoesNotContain("global::Test.Seamless.Diagnostics.QuerySyntaxSmokeSource.Numbers",section);
        Assert.DoesNotContain("global::Seamless.Linq.Enumerable.Select",section);
        Assert.DoesNotContain("global::Seamless.Linq.Enumerable.Where",section);
    }

    [Fact]
    [Log]
    public void Where_Select_LoopFusion_Log_Smoke() {
        var numbers = new[] { 1,2,3,4,5 };
        var expected = SystemEnumerable.Select(
            SystemEnumerable.Where(numbers,static value => value%2==0),
            static value => value*10);

        IEnumerable<int>? actual = null;
        var log = CaptureConsole(() => actual = numbers
            .Where(static value => value%2==0)
            .Select(static value => value*10));

        Assert.Equal(expected,actual);
        Assert.Contains("変換前:",log);
        Assert.Contains("変換後:",log);
        Assert.Contains(".Where(",log);
        Assert.Contains(".Select(",log);
        Assert.Contains("foreach(var __seamlessItem in __seamlessSource)",log);
        Assert.Contains("if(!((__seamlessItem)%2==0))",log);
        Assert.Contains("var __seamlessValue0 = (__seamlessItem)*10;",log);
        Assert.Equal(1,CountOccurrences(log,"foreach(var __seamlessItem in __seamlessSource)"));
    }

    [Fact]
    public void ListWhere_Interceptor_String_Smoke() {
        var values = new List<string> { "a","bb","ccc" };

        var filtered = values.Where(value => value.Length>1).ToArray();

        Assert.Equal(new[] { "bb","ccc" },filtered);
    }

    [Fact]
    public void EnumerableWhere_StaticCall_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = Enumerable.Where(numbers,n => (n%2)==0).ToArray();

        Assert.Equal(new[] { 2,4 },filtered);
    }

    [Fact]
    public void EnumerableWhere_StaticCall_Index_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = Enumerable.Where(numbers,(value,index) => index>=3).ToArray();

        Assert.Equal(new[] { 4,5 },filtered);
    }

    [Fact]
    public void ListWhere_MethodGroup_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = numbers.Where(IsGreaterThanTwo).ToArray();

        Assert.Equal(new[] { 3,4,5 },filtered);

        static bool IsGreaterThanTwo(int value) => value>2;
    }

    private static string GeneratedInterceptorSection(string containingMethodName) {
        var source = GeneratedInterceptorSource();
        var markerIndex = source.IndexOf(containingMethodName,StringComparison.Ordinal);
        Assert.True(markerIndex>=0,$"Generated interceptor source did not contain '{containingMethodName}'.");

        var nextSummaryIndex = source.IndexOf("        /// <summary>",markerIndex+containingMethodName.Length,StringComparison.Ordinal);
        return nextSummaryIndex<0 ? source[markerIndex..] : source[markerIndex..nextSummaryIndex];
    }

    private static string GeneratedInterceptorSource() {
        var generatedRoot = GeneratedSourceRoot();
        Assert.True(Directory.Exists(generatedRoot),$"Generated source directory does not exist: {generatedRoot}");

        var matches = Directory
            .EnumerateFiles(generatedRoot,"*.g.cs",SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Where(static text => text.Contains("namespace Intercept.",StringComparison.Ordinal)
                || text.Contains("namespace Intercept {",StringComparison.Ordinal))
            .Where(static text => text.Contains(nameof(QuerySyntax_Where_With_Projection_Smoke),StringComparison.Ordinal))
            .ToArray();

        Assert.Single(matches);
        return matches[0];
    }

    private static string CaptureConsole(Action action) {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try {
            action();
        }
        finally {
            Console.SetOut(originalOut);
        }
        return writer.ToString();
    }

    private static int CountOccurrences(string text,string value) {
        var count = 0;
        var index = 0;
        while((index=text.IndexOf(value,index,StringComparison.Ordinal))>=0) {
            count++;
            index+=value.Length;
        }
        return count;
    }

    private static string GeneratedSourceRoot() {
        var assemblyDirectory = Path.GetDirectoryName(typeof(InterceptorSmokeTests).Assembly.Location)
            ?? throw new InvalidOperationException("Could not resolve test assembly directory.");
        var targetFrameworkDirectory = new DirectoryInfo(assemblyDirectory);
        var configurationDirectory = targetFrameworkDirectory.Parent
            ?? throw new InvalidOperationException("Could not resolve configuration directory.");
        var projectDirectory = configurationDirectory.Parent?.Parent
            ?? throw new InvalidOperationException("Could not resolve project directory.");

        return Path.Combine(
            projectDirectory.FullName,
            "obj",
            configurationDirectory.Name,
            targetFrameworkDirectory.Name,
            "generated");
    }
}

internal static class QuerySyntaxSmokeSource {
    public static readonly int[] Numbers = [1,2,3,4,5];
}

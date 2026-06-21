using System.Text.RegularExpressions;
using Tsavorite.SourceGenerator;

namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class FragmentIndentationTests {
    private const string GroupJoinSource = """
        using System.Collections.Generic;
        using System.Linq;
        using Tsavorite.SourceGenerator;

        namespace Sample;

        public sealed class Consumer {
            public void Consume<T>(T value) { }
        }

        public static class ConsumerExtensions {
            public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;

            public static void Consume<T>(this IEnumerable<T> source, Consumer consumer) {
                foreach(var item in source) {
                    consumer.Consume(item);
                }
            }
        }

        public sealed class Container {
            private readonly Consumer consumer = new();

            private IEnumerable<int> srcCount(int count) => Enumerable.Range(1, count);
            private IEnumerable<int> src2Count(int count) => Enumerable.Range(1, count);

            [LinqIntercept(true)]
            private void GroupJoin_inner_Impl() =>
                this.srcCount(3).AsX<int>().GroupJoin(this.src2Count(3), x => x, x => x, (outer, inner) => outer).Consume(this.consumer);

            public void Run() => this.GroupJoin_inner_Impl();
        }
        """;

    [Fact]
    public void GroupJoin_KeySelectorAssignment_KeepsTryGetNextBlockIndent() {
        var generated=GeneratedSource(GroupJoinSource);
        var lines=generated.Replace("\r\n","\n").Split('\n');

        AssertAssignmentAligned(
            lines,
            @"^\s*__k\d+=__ov\d+;$",
            @"^\s*var __ov\d+=",
            @"^\s*if\(__lk\d+\.TryGetValue");

        AssertAssignmentAligned(
            lines,
            @"^\s*__k\d+=__it\d+;$",
            @"^\s*int __k\d+=default!;",
            @"^\s*if\(!__lk\d+\.TryGetValue");
    }

    [Fact]
    public void FragmentRawStrings_DoNotDirectlyEmbedFuncAssignStatements() {
        var fragmentsRoot=FindRepositoryPath("FLinq","SourceGenerator","Linq","Fragments");
        var offenders=Directory.EnumerateFiles(fragmentsRoot,"*.cs",SearchOption.AllDirectories)
            .Where(static path=>!path.EndsWith($"{Path.DirectorySeparatorChar}Pipeline.cs",StringComparison.Ordinal))
            .SelectMany(static path=>File.ReadLines(path)
                .Select((line,index)=>(Path:path,Line:index+1,Text:line))
                .Where(static entry=>Regex.IsMatch(entry.Text,@"\{\{\s*this\.FuncAssign(?:Current|Static|Inline)?\(")))
            .Select(static entry=>$"{Path.GetRelativePath(FindRepositoryRoot(),entry.Path)}:{entry.Line}: {entry.Text.Trim()}")
            .ToArray();

        Assert.True(offenders.Length==0,"Direct FuncAssign raw string embeds remain:\n"+string.Join("\n",offenders));
    }

    private static void AssertAssignmentAligned(string[] lines,string assignmentPattern,string beforePattern,string afterPattern) {
        var assignmentIndex=Array.FindIndex(lines,line=>Regex.IsMatch(line,assignmentPattern));

        Assert.True(assignmentIndex>=0,$"Assignment line was not generated: {assignmentPattern}");

        var assignmentIndent=LeadingWhitespace(lines[assignmentIndex]);
        var beforeLine=lines.Take(assignmentIndex).Last(line=>Regex.IsMatch(line,beforePattern));
        var afterLine=lines.Skip(assignmentIndex+1).First(line=>Regex.IsMatch(line,afterPattern));

        Assert.True(
            assignmentIndent.Length>=LeadingWhitespace(beforeLine).Length,
            $"Assignment indentation was shallower than the preceding line: {assignmentPattern}");
        Assert.True(
            assignmentIndent.Length>=LeadingWhitespace(afterLine).Length,
            $"Assignment indentation was shallower than the following line: {assignmentPattern}");
    }

    private static string GeneratedSource(string source) {
        var comp=GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        var driver=GeneratorTestHelpers.CreateDriver().RunGenerators(comp);
        var result=driver.GetRunResult();

        return string.Concat(result.Results
            .SelectMany(static r=>r.GeneratedSources)
            .Select(static s=>s.SourceText.ToString()));
    }

    private static string LeadingWhitespace(string line) {
        var length=line.TakeWhile(char.IsWhiteSpace).Count();
        return line[..length];
    }

    private static string FindRepositoryPath(params string[] segments) {
        var root=FindRepositoryRoot();
        return Path.Combine([root,..segments]);
    }

    private static string FindRepositoryRoot() {
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory=directory.Parent) {
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))
                && Directory.Exists(Path.Combine(directory.FullName,"FLinq"))) {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}

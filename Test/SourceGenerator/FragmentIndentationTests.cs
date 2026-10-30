using Seamless.FLinq;

using System.Text.RegularExpressions;
namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class FragmentIndentationTests(ITestOutputHelper output) {
    private const string GroupJoinSource = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;

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

            // 結果選択子が内側のグループを使わない形は探索表自体を省くので、探索表を作る形で字下げを確かめる。
            [LinqIntercept(true)]
            private void GroupJoin_inner_Impl() =>
                this.srcCount(3).AsX<int>().GroupJoin(this.src2Count(3), x => x, x => x, (outer, inner) => outer+inner.Count(), EqualityComparer<int>.Default).Consume(this.consumer);

            public void Run() => this.GroupJoin_inner_Impl();
        }
        """;

    [Fact]
    public void GroupJoin_KeySelectorAssignment_KeepsTryGetNextBlockIndent() {
        var generated = GeneratedSource(GroupJoinSource);
        var lines = generated.Replace("\r\n","\n").Split('\n');

        this.AssertAssignmentAligned(
            lines,
            @"^\s*int\s+\w+=\w+;$",
            @"^\s*var\s+\w+=",
            @"^\s*if\(.*\.GetGroup");

        this.AssertAssignmentAligned(
            lines,
            @"^\s*int\s+\w+=\w+;$",
            @"^\s*var\s+\w+=",
            @"^\s*\w+\.Add");
    }

    [Fact]
    public void FragmentRawStrings_DoNotDirectlyEmbedFuncAssignStatements() {
        var fragmentsRoot = FindRepositoryPath("Seamless.FLinq.Generator","SourceGenerator","Linq","Fragments");
        var offenders = Directory.EnumerateFiles(fragmentsRoot,"*.cs",SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith($"{Path.DirectorySeparatorChar}Pipeline.cs",StringComparison.Ordinal))
            .SelectMany(static path => File.ReadLines(path)
                .Select((line,index) => (Path: path,Line: index+1,Text: line))
                .Where(static entry => Regex.IsMatch(entry.Text,@"\{\{\s*this\.FuncAssign(?:Current|Static|Inline)?\(")))
            .Select(static entry => $"{Path.GetRelativePath(FindRepositoryRoot(),entry.Path)}:{entry.Line}: {entry.Text.Trim()}")
            .ToArray();

        GeneratedSourceAssert.True(
            offenders.Length==0,
            "Direct FuncAssign raw string embeds remain:\n"+string.Join("\n",offenders),
            string.Empty,
            output);
    }

    private void AssertAssignmentAligned(string[] lines,string assignmentPattern,string beforePattern,string afterPattern) {
        var generated = string.Join(Environment.NewLine,lines);
        var afterIndex=Array.FindIndex(lines,line=>Regex.IsMatch(line,afterPattern));
        var assignmentIndex=afterIndex<0 ? -1 : Enumerable.Range(0,afterIndex)
            .Where(index=>Regex.IsMatch(lines[index],assignmentPattern)).DefaultIfEmpty(-1).Last();

        GeneratedSourceAssert.True(
            assignmentIndex>=0,
            $"Assignment line was not generated: {assignmentPattern}",
            generated,
            output);

        var assignmentIndent = LeadingWhitespace(lines[assignmentIndex]);
        var beforeLine = lines.Take(assignmentIndex).LastOrDefault(line => Regex.IsMatch(line,beforePattern));
        var afterLine = lines.Skip(assignmentIndex+1).FirstOrDefault(line => Regex.IsMatch(line,afterPattern));

        GeneratedSourceAssert.True(
            beforeLine is not null,
            $"Preceding line was not generated: {beforePattern}",
            generated,
            output);
        GeneratedSourceAssert.True(
            afterLine is not null,
            $"Following line was not generated: {afterPattern}",
            generated,
            output);

        GeneratedSourceAssert.True(
            assignmentIndent.Length>=LeadingWhitespace(beforeLine!).Length,
            $"Assignment indentation was shallower than the preceding line: {assignmentPattern}",
            generated,
            output);
        GeneratedSourceAssert.True(
            assignmentIndent.Length>=LeadingWhitespace(afterLine!).Length,
            $"Assignment indentation was shallower than the following line: {assignmentPattern}",
            generated,
            output);
    }

    private static string GeneratedSource(string source) {
        var comp = GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        var driver = GeneratorTestHelpers.CreateDriver().RunGenerators(comp);
        var result = driver.GetRunResult();

        return string.Concat(result.Results
            .SelectMany(static r => r.GeneratedSources)
            .Select(static s => s.SourceText.ToString()));
    }

    private static string LeadingWhitespace(string line) {
        var length = line.TakeWhile(char.IsWhiteSpace).Count();
        return line[..length];
    }

    private static string FindRepositoryPath(params string[] segments) {
        var root = FindRepositoryRoot();
        return Path.Combine([root,.. segments]);
    }

    private static string FindRepositoryRoot() {
        for(var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory=directory.Parent) {
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))
                &&Directory.Exists(Path.Combine(directory.FullName,"Seamless.FLinq.Generator"))) {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}

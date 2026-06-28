using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Test.System.Linq.Intercept;

public sealed class DynamicLinqRunnerReport(ITestOutputHelper output) {
    [Fact,Trait("Category","DynamicLinq")]
    public void RandomSmokeReport() {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var runnerProject = Path.Combine(repoRoot,"DynamicLinq.Runner","DynamicLinq.Runner.csproj");
        Assert.True(File.Exists(runnerProject),$"DynamicLinq.Runner project was not found: {runnerProject}");

        var configuredArgs = Environment.GetEnvironmentVariable("Tsavorite_DYNAMIC_LINQ_ARGS");
        var runnerArgs = string.IsNullOrWhiteSpace(configuredArgs)
            ? "--configuration Debug --random-only --random-count 10 --types long,string --iterations 1"
            : configuredArgs;

        var result = RunDotnet(repoRoot,["run","--no-restore","--project",runnerProject,"--",.. SplitArgs(runnerArgs)]);
        var report = DynamicLinqReport.Parse(result.Output);
        var reportText = BuildReport(runnerArgs,report,result.Output);
        foreach(var line in reportText.Replace("\r\n","\n").Split('\n')) {
            output.WriteLine(line);
        }
        if(!string.IsNullOrWhiteSpace(report.Artifact) && Directory.Exists(report.Artifact)) {
            File.WriteAllText(Path.Combine(report.Artifact,"report.txt"),reportText,Encoding.UTF8);
        }

        Assert.True(result.ExitCode==0,$"DynamicLinq.Runner failed with exit code {result.ExitCode}.");
        Assert.True(report.Failed==0,$"Dynamic LINQ cases failed: {report.Failed}.");
    }

    private static string BuildReport(string runnerArgs,DynamicLinqReport report,string rawOutput) {
        var builder = new StringBuilder();
        builder.AppendLine("Dynamic LINQ Runner Report");
        builder.AppendLine($"args: {runnerArgs}");
        builder.AppendLine($"artifact: {report.Artifact}");
        builder.AppendLine($"total={report.Total}, ok={report.Ok}, skipVerify={report.SkipVerify}, failed={report.Failed}");
        builder.AppendLine();

        builder.AppendLine("Slowest FLinq/System ratio");
        AppendRows(builder,report.Rows
            .Where(static row => row.Status!="SKIP_VERIFY" && row.SystemNs>0)
            .OrderByDescending(static row => row.Ratio)
            .ThenByDescending(static row => row.FLinqNs)
            .Take(10));

        builder.AppendLine();
        builder.AppendLine("Most logical work");
        AppendRows(builder,report.Rows
            .OrderByDescending(static row => row.Logical)
            .ThenByDescending(static row => row.Physical)
            .Take(10));

        if(report.Failed>0) {
            builder.AppendLine();
            builder.AppendLine("Failed cases");
            AppendRows(builder,report.Rows.Where(static row => row.Status!="OK" && row.Status!="SKIP_VERIFY"));
        }

        if(report.Rows.Count==0) {
            builder.AppendLine();
            builder.AppendLine("No CSV rows were parsed. Raw output tail:");
            foreach(var line in rawOutput.Replace("\r\n","\n").Split('\n').TakeLast(40)) {
                builder.AppendLine(line);
            }
        }

        return builder.ToString();
    }

    private static void AppendRows(StringBuilder builder,IEnumerable<DynamicLinqRow> rows) {
        builder.AppendLine("case                                   type    status       sys(ns) flinq(ns) noopt(ns) ratio  phys logical delegate(log/phys) noopt(log/phys) hash(build/probe)");
        builder.AppendLine("-------------------------------------  ------  -----------  ------- --------- --------- ------ ----- ------- ------------------ --------------- -----------------");
        foreach(var row in rows) {
            builder.AppendLine(
                $"{Trim(row.Name,37),-37}  {row.Type,-6}  {row.Status,-11}  {row.SystemNs,7} {row.FLinqNs,9} {row.NoOptimizedNs,9} {row.Ratio,6:0.00} {row.Physical,5} {row.Logical,7} {row.LogicalDelegate,8}/{row.PhysicalDelegate,-8} {row.NoOptimizedLogicalDelegate,6}/{row.NoOptimizedPhysicalDelegate,-6} {row.HashBuild,5}/{row.HashProbe,-5}");
        }
    }

    private static string Trim(string value,int maxLength) =>
        value.Length<=maxLength ? value : value[..(maxLength-1)]+"~";

    private static string FindRepoRoot(string start) {
        var dir = new DirectoryInfo(start);
        while(dir is not null) {
            if(File.Exists(Path.Combine(dir.FullName,"Seamless.FLinq","Seamless.FLinq.csproj"))) {
                return dir.FullName;
            }
            dir=dir.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static ProcessResult RunDotnet(string workingDirectory,IReadOnlyList<string> args) {
        var startInfo = new ProcessStartInfo("dotnet") {
            WorkingDirectory=workingDirectory,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            UseShellExecute=false,
        };
        foreach(var arg in args) {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
        var output = new StringBuilder();
        output.Append(process.StandardOutput.ReadToEnd());
        output.Append(process.StandardError.ReadToEnd());
        if(!process.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds)) {
            process.Kill(entireProcessTree:true);
            throw new TimeoutException("DynamicLinq.Runner did not finish within 10 minutes.");
        }
        return new ProcessResult(process.ExitCode,output.ToString());
    }

    private static string[] SplitArgs(string args) {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for(var index = 0; index<args.Length; index++) {
            var c = args[index];
            if(c=='"') {
                inQuotes=!inQuotes;
                continue;
            }
            if(char.IsWhiteSpace(c) && !inQuotes) {
                if(current.Length>0) {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(c);
        }
        if(current.Length>0) {
            result.Add(current.ToString());
        }
        return result.ToArray();
    }

    private sealed record ProcessResult(int ExitCode,string Output);

    private sealed record DynamicLinqReport(string Artifact,IReadOnlyList<DynamicLinqRow> Rows) {
        public int Total => this.Rows.Count;
        public int Ok => this.Rows.Count(static row => row.Status=="OK");
        public int SkipVerify => this.Rows.Count(static row => row.Status=="SKIP_VERIFY");
        public int Failed => this.Rows.Count(static row => row.Status!="OK" && row.Status!="SKIP_VERIFY");

        public static DynamicLinqReport Parse(string output) {
            var artifact = string.Empty;
            var rows = new List<DynamicLinqRow>();
            foreach(var line in output.Replace("\r\n","\n").Split('\n')) {
                if(line.StartsWith("artifact=",StringComparison.Ordinal)) {
                    artifact=line["artifact=".Length..].Trim();
                    continue;
                }
                if(line.StartsWith("Artifacts kept:",StringComparison.Ordinal)) {
                    artifact=line["Artifacts kept:".Length..].Trim();
                    continue;
                }
                if(line.Length==0 || line.StartsWith("name,type,",StringComparison.Ordinal)) {
                    continue;
                }
                var parts = line.Split(',');
                if((parts.Length!=27 && parts.Length!=30) || !int.TryParse(parts[2],NumberStyles.Integer,CultureInfo.InvariantCulture,out _)) {
                    continue;
                }
                rows.Add(DynamicLinqRow.Parse(parts));
            }
            return new DynamicLinqReport(artifact,rows);
        }
    }

    private sealed record DynamicLinqRow(
        string Name,
        string Type,
        int Length,
        bool Ordered,
        long SystemNs,
        long FLinqNs,
        long NoOptimizedNs,
        long Physical,
        long Logical,
        long Where,
        long Select,
        long SelectManyOuter,
        long SelectManyInner,
        long JoinOuter,
        long JoinInner,
        long GroupJoinOuter,
        long GroupJoinInner,
        long LeftJoinOuter,
        long LeftJoinInner,
        long RightJoinOuter,
        long RightJoinInner,
        long HashBuild,
        long HashProbe,
        long BufferAdd,
        long TryGetNonEnumeratedCount,
        long LogicalDelegate,
        long PhysicalDelegate,
        long NoOptimizedLogicalDelegate,
        long NoOptimizedPhysicalDelegate,
        string Status) {
        public double Ratio => this.SystemNs<=0 ? 0.0 : (double)this.FLinqNs/this.SystemNs;

        public static DynamicLinqRow Parse(string[] parts) =>
            parts.Length==30
                ? new(
                    parts[0],
                    parts[1],
                    int.Parse(parts[2],CultureInfo.InvariantCulture),
                    bool.Parse(parts[3]),
                    long.Parse(parts[4],CultureInfo.InvariantCulture),
                    long.Parse(parts[5],CultureInfo.InvariantCulture),
                    long.Parse(parts[6],CultureInfo.InvariantCulture),
                    long.Parse(parts[7],CultureInfo.InvariantCulture),
                    long.Parse(parts[8],CultureInfo.InvariantCulture),
                    long.Parse(parts[9],CultureInfo.InvariantCulture),
                    long.Parse(parts[10],CultureInfo.InvariantCulture),
                    long.Parse(parts[11],CultureInfo.InvariantCulture),
                    long.Parse(parts[12],CultureInfo.InvariantCulture),
                    long.Parse(parts[13],CultureInfo.InvariantCulture),
                    long.Parse(parts[14],CultureInfo.InvariantCulture),
                    long.Parse(parts[15],CultureInfo.InvariantCulture),
                    long.Parse(parts[16],CultureInfo.InvariantCulture),
                    long.Parse(parts[17],CultureInfo.InvariantCulture),
                    long.Parse(parts[18],CultureInfo.InvariantCulture),
                    long.Parse(parts[19],CultureInfo.InvariantCulture),
                    long.Parse(parts[20],CultureInfo.InvariantCulture),
                    long.Parse(parts[21],CultureInfo.InvariantCulture),
                    long.Parse(parts[22],CultureInfo.InvariantCulture),
                    long.Parse(parts[23],CultureInfo.InvariantCulture),
                    long.Parse(parts[24],CultureInfo.InvariantCulture),
                    long.Parse(parts[25],CultureInfo.InvariantCulture),
                    long.Parse(parts[26],CultureInfo.InvariantCulture),
                    long.Parse(parts[27],CultureInfo.InvariantCulture),
                    long.Parse(parts[28],CultureInfo.InvariantCulture),
                    parts[29])
                : new(
                    parts[0],
                    parts[1],
                    int.Parse(parts[2],CultureInfo.InvariantCulture),
                    bool.Parse(parts[3]),
                    long.Parse(parts[4],CultureInfo.InvariantCulture),
                    long.Parse(parts[5],CultureInfo.InvariantCulture),
                    0,
                    long.Parse(parts[6],CultureInfo.InvariantCulture),
                    long.Parse(parts[7],CultureInfo.InvariantCulture),
                    long.Parse(parts[8],CultureInfo.InvariantCulture),
                    long.Parse(parts[9],CultureInfo.InvariantCulture),
                    long.Parse(parts[10],CultureInfo.InvariantCulture),
                    long.Parse(parts[11],CultureInfo.InvariantCulture),
                    long.Parse(parts[12],CultureInfo.InvariantCulture),
                    long.Parse(parts[13],CultureInfo.InvariantCulture),
                    long.Parse(parts[14],CultureInfo.InvariantCulture),
                    long.Parse(parts[15],CultureInfo.InvariantCulture),
                    long.Parse(parts[16],CultureInfo.InvariantCulture),
                    long.Parse(parts[17],CultureInfo.InvariantCulture),
                    long.Parse(parts[18],CultureInfo.InvariantCulture),
                    long.Parse(parts[19],CultureInfo.InvariantCulture),
                    long.Parse(parts[20],CultureInfo.InvariantCulture),
                    long.Parse(parts[21],CultureInfo.InvariantCulture),
                    long.Parse(parts[22],CultureInfo.InvariantCulture),
                    long.Parse(parts[23],CultureInfo.InvariantCulture),
                    long.Parse(parts[24],CultureInfo.InvariantCulture),
                    long.Parse(parts[25],CultureInfo.InvariantCulture),
                    0,
                    0,
                    parts[26]);
    }
}

param(
    [string]$ProjectPath = ".\Test.System.Linq.Enumerable.Linq\Test.System.Linq.Enumerable.Linq.csproj",
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0",
    [int]$WarmupCount = 1,
    [int]$IterationCount = 1,
    [string]$Filter = "*",
    [string]$OutputCsv = ".\artifacts\system-linq-workload.csv"
)

$ErrorActionPreference = "Stop"

function Resolve-RepoPath([string]$Path) {
    if([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

$projectFullPath = Resolve-RepoPath $ProjectPath
$outputFullPath = Resolve-RepoPath $OutputCsv
$artifactsRoot = Join-Path (Get-Location) "artifacts"
$runnerRoot = Join-Path $artifactsRoot "system-linq-workload-runner"
$runnerProject = Join-Path $runnerRoot "SystemLinqWorkloadRunner.csproj"
$runnerProgram = Join-Path $runnerRoot "Program.cs"

New-Item -ItemType Directory -Force -Path $runnerRoot | Out-Null
New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($outputFullPath)) | Out-Null

dotnet build $projectFullPath -c $Configuration --no-restore | Out-Host
if($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$projectDirectory = Split-Path -Parent $projectFullPath
$assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($projectFullPath)
$assemblyPath = Join-Path $projectDirectory "bin\$Configuration\$Framework\$assemblyName.dll"
if(-not (Test-Path $assemblyPath)) {
    throw "Benchmark test assembly was not found: $assemblyPath"
}

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$Framework</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
</Project>
"@ | Set-Content -Path $runnerProject -Encoding UTF8

@'
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;

var options = Options.Parse(args);
var assemblyDirectory = Path.GetDirectoryName(options.AssemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
{
    var candidate = Path.Combine(assemblyDirectory, assemblyName.Name + ".dll");
    return File.Exists(candidate)
        ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate)
        : null;
};

var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(options.AssemblyPath);
var filterRegex = WildcardToRegex(options.Filter);
var rows = new List<ResultRow>();

foreach(var type in assembly.GetTypes().Where(static item => !item.IsAbstract).OrderBy(static item => item.FullName, StringComparer.Ordinal))
{
    foreach(var method in type
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(IsBenchmarkMethod)
        .OrderBy(static item => item.Name, StringComparer.Ordinal))
    {
        var displayName = type.FullName + "." + method.Name;
        if(!filterRegex.IsMatch(displayName))
        {
            continue;
        }

        var row = Measure(type, method, options);
        rows.Add(row);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{row.Type}.{row.Method}: {row.ElapsedMs:F3} ms {row.Status}"));
    }
}

WriteCsv(options.OutputPath, rows);
return rows.Any(static row => row.Status != "OK") ? 1 : 0;

static ResultRow Measure(Type type, MethodInfo method, Options options)
{
    object? instance = null;
    try
    {
        instance = Activator.CreateInstance(type);
        if(instance is null)
        {
            return new ResultRow(type.FullName ?? type.Name, method.Name, double.NaN, options.WarmupCount, options.IterationCount, "Failed", "Activator returned null.");
        }

        for(var index = 0; index < options.WarmupCount; index++)
        {
            method.Invoke(instance, null);
        }

        var stopwatch = Stopwatch.StartNew();
        for(var index = 0; index < options.IterationCount; index++)
        {
            method.Invoke(instance, null);
        }
        stopwatch.Stop();

        return new ResultRow(
            type.FullName ?? type.Name,
            method.Name,
            stopwatch.Elapsed.TotalMilliseconds / Math.Max(1, options.IterationCount),
            options.WarmupCount,
            options.IterationCount,
            "OK",
            string.Empty);
    }
    catch(TargetInvocationException ex)
    {
        return new ResultRow(type.FullName ?? type.Name, method.Name, double.NaN, options.WarmupCount, options.IterationCount, "Failed", ex.InnerException?.ToString() ?? ex.ToString());
    }
    catch(Exception ex)
    {
        return new ResultRow(type.FullName ?? type.Name, method.Name, double.NaN, options.WarmupCount, options.IterationCount, "Failed", ex.ToString());
    }
    finally
    {
        if(instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

static bool IsBenchmarkMethod(MethodInfo method) =>
    method.GetCustomAttributes(inherit: false).Any(static attribute =>
        attribute.GetType().FullName == "BenchmarkDotNet.Attributes.BenchmarkAttribute");

static Regex WildcardToRegex(string pattern)
{
    var escaped = Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal);
    return new Regex("^" + escaped + "$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
}

static void WriteCsv(string path, IReadOnlyList<ResultRow> rows)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    writer.WriteLine("Type,Method,ElapsedMs,WarmupCount,IterationCount,Status,Error");
    foreach(var row in rows)
    {
        writer.Write(Escape(row.Type));
        writer.Write(',');
        writer.Write(Escape(row.Method));
        writer.Write(',');
        writer.Write(double.IsFinite(row.ElapsedMs) ? row.ElapsedMs.ToString("F6", CultureInfo.InvariantCulture) : string.Empty);
        writer.Write(',');
        writer.Write(row.WarmupCount.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(row.IterationCount.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(Escape(row.Status));
        writer.Write(',');
        writer.WriteLine(Escape(row.Error));
    }
}

static string Escape(string value) =>
    value.Contains('"', StringComparison.Ordinal) || value.Contains(',', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal)
        ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
        : value;

internal sealed record ResultRow(string Type, string Method, double ElapsedMs, int WarmupCount, int IterationCount, string Status, string Error);

internal sealed record Options(string AssemblyPath, string OutputPath, int WarmupCount, int IterationCount, string Filter)
{
    public static Options Parse(string[] args)
    {
        string? assemblyPath = null;
        string? outputPath = null;
        var warmupCount = 1;
        var iterationCount = 1;
        var filter = "*";

        for(var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            string ReadValue()
            {
                if(index + 1 >= args.Length)
                {
                    throw new ArgumentException("Missing value for " + current);
                }
                index++;
                return args[index];
            }

            switch(current)
            {
                case "--assembly":
                    assemblyPath = ReadValue();
                    break;
                case "--output":
                    outputPath = ReadValue();
                    break;
                case "--warmup":
                    warmupCount = int.Parse(ReadValue(), CultureInfo.InvariantCulture);
                    break;
                case "--iterations":
                    iterationCount = int.Parse(ReadValue(), CultureInfo.InvariantCulture);
                    break;
                case "--filter":
                    filter = ReadValue();
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + current);
            }
        }

        if(string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("Missing --assembly.");
        }
        if(string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Missing --output.");
        }
        if(warmupCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warmupCount));
        }
        if(iterationCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterationCount));
        }

        return new Options(Path.GetFullPath(assemblyPath), Path.GetFullPath(outputPath), warmupCount, iterationCount, filter);
    }
}
'@ | Set-Content -Path $runnerProgram -Encoding UTF8

dotnet build $runnerProject -c $Configuration | Out-Host
if($LASTEXITCODE -ne 0) {
    throw "runner build failed with exit code $LASTEXITCODE"
}

dotnet run --project $runnerProject -c $Configuration --no-build -- --assembly $assemblyPath --output $outputFullPath --warmup $WarmupCount --iterations $IterationCount --filter $Filter
if($LASTEXITCODE -ne 0) {
    throw "measurement runner failed with exit code $LASTEXITCODE"
}

Write-Host "Wrote $outputFullPath"

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UI;

public sealed record BenchmarkStoredResult(
    string Project,
    string Method,
    string TypeFullName,
    int ElementCount,
    string Status,
    string Mean,
    double? MeanNanoseconds,
    long? AllocatedBytes,
    DateTimeOffset MeasuredAt,
    DateTimeOffset? SourceCsvAt,
    string SourcePath){
    public string ArgumentKey { get; init; } = string.Empty;
    public IReadOnlyList<BenchmarkArgument> Arguments { get; init; } = [];

    public ProjectMetrics ToProjectMetrics() =>
        string.Equals(this.Status,"Completed",StringComparison.OrdinalIgnoreCase)&&this.MeanNanoseconds is not null
            ? ProjectMetrics.FromMeasurement(this.MeanNanoseconds,this.AllocatedBytes)
            : ProjectMetrics.Missing;
}

public sealed record BenchmarkCsvScanResult(
    IReadOnlyList<BenchmarkRunOption> RunOptions,
    IReadOnlyList<BenchmarkStoredResult> Results);

public sealed class BenchmarkResultStore{
    private static readonly JsonSerializerOptions JsonOptions = new(){
        WriteIndented=true,
    };

    private readonly string path;
    private readonly Dictionary<string,BenchmarkStoredResult> results = new(StringComparer.OrdinalIgnoreCase);

    public BenchmarkResultStore(string benchmarkRoot){
        this.path=Path.Combine(benchmarkRoot,"benchmark-results.json");
        this.Load();
    }

    public IReadOnlyList<BenchmarkStoredResult> Results => this.results.Values.ToArray();

    public void Upsert(BenchmarkResultItem item,DateTimeOffset measuredAt,DateTimeOffset? sourceCsvAt,string sourcePath){
        if(item.ElementCount<=0){
            return;
        }

        this.Upsert(new BenchmarkStoredResult(
            item.Project,
            item.Method,
            item.TypeFullName,
            item.ElementCount,
            item.Status,
            item.Mean,
            item.MeanNanoseconds,
            item.AllocatedBytesValue,
            measuredAt,
            sourceCsvAt,
            sourcePath){
            ArgumentKey=item.ArgumentKey,
            Arguments=item.Arguments,
        });
    }

    public void Upsert(BenchmarkStoredResult result){
        if(string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)&&result.MeanNanoseconds is null){
            return;
        }

        this.results[CreateKey(result.Project,result.TypeFullName,result.Method,result.ArgumentKey,result.ElementCount,result.MeasuredAt)]=result;
    }

    public void UpsertMany(IEnumerable<BenchmarkStoredResult> items){
        foreach(var item in items){
            this.Upsert(item);
        }
    }

    public BenchmarkStoredResult? GetLatest(string method,string project,string typeFullName = "",string argumentKey = ""){
        return this.results.Values
            .Where(result => string.Equals(result.Method,method,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase)
                &&IsSameType(result.TypeFullName,typeFullName)
                &&IsSameArgument(result.ArgumentKey,argumentKey))
            .OrderByDescending(static result => result.MeasuredAt)
            .ThenByDescending(static result => result.ElementCount)
            .FirstOrDefault();
    }

    public BenchmarkStoredResult? GetPrevious(string method,string project,string typeFullName,int elementCount,DateTimeOffset? before,string argumentKey = ""){
        return this.results.Values
            .Where(result => string.Equals(result.Method,method,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase)
                &&IsSameType(result.TypeFullName,typeFullName)
                &&IsSameArgument(result.ArgumentKey,argumentKey)
                &&result.ElementCount==elementCount
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null
                &&(before is null||result.MeasuredAt<before.Value))
            .OrderByDescending(static result => result.MeasuredAt)
            .FirstOrDefault();
    }

    public BenchmarkStoredResult? Get(string method,string project,int elementCount,string typeFullName = "",string argumentKey = ""){
        return this.results.Values
            .Where(result => string.Equals(result.Method,method,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase)
                &&IsSameType(result.TypeFullName,typeFullName)
                &&IsSameArgument(result.ArgumentKey,argumentKey)
                &&result.ElementCount==elementCount)
            .OrderByDescending(static result => result.MeasuredAt)
            .FirstOrDefault();
    }

    public DateTimeOffset? GetLatestMeasuredAt(string method,string project) =>
        this.GetLatest(method,project)?.MeasuredAt;

    public IReadOnlyList<BenchmarkComparisonRow> BuildLatestComparisonRows(){
        var rows = this.results.Values
            .Where(static result => string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .GroupBy(static result => result.Method+"|"+result.TypeFullName+"|"+result.ArgumentKey,StringComparer.OrdinalIgnoreCase)
            .Select(group => {
                var metrics = group
                    .GroupBy(static result => result.Project,StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        static item => item.Key,
                        static item => item
                            .OrderByDescending(static result => result.MeasuredAt)
                            .ThenByDescending(static result => result.ElementCount)
                            .First()
                            .ToProjectMetrics(),
                        StringComparer.OrdinalIgnoreCase);
                var result = group.First();
                return CreateReportRows(result.Method,result.TypeFullName,result.ArgumentKey,result.Arguments,metrics);
            })
            .SelectMany(static item => item)
            .ToArray();
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows);
    }

    public void Save(){
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)??".");
        var snapshot = new BenchmarkResultStoreSnapshot(this.results.Values
            .OrderBy(static result => result.Method,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static result => result.TypeFullName,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static result => result.Project,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static result => result.ElementCount)
            .ThenBy(static result => result.MeasuredAt)
            .ToArray());
        AtomicWrite(this.path,snapshot);
    }

    private void Load(){
        if(!File.Exists(this.path)){
            return;
        }

        var snapshot = JsonSerializer.Deserialize<BenchmarkResultStoreSnapshot>(File.ReadAllText(this.path,Encoding.UTF8),JsonOptions);
        if(snapshot is null){
            return;
        }

        foreach(var result in snapshot.Results){
            this.Upsert(result);
        }
    }

    private static IEnumerable<ReportRow> CreateReportRows(
        string method,
        string typeFullName,
        string argumentKey,
        IReadOnlyList<BenchmarkArgument> arguments,
        IReadOnlyDictionary<string,ProjectMetrics> metrics){
        foreach(var project in BenchmarkComparisonFormatter.ProjectOrder){
            if(!metrics.TryGetValue(project,out var metric)||!metric.HasMeasuredValue){
                continue;
            }

            yield return new ReportRow(
                project,
                method,
                typeFullName,
                method,
                "Completed",
                metric.TimeNanoseconds?.ToString(CultureInfo.InvariantCulture)??string.Empty,
                string.Empty,
                string.Empty,
                metric.AllocatedBytes?.ToString(CultureInfo.InvariantCulture)??string.Empty,
                string.Empty,
                string.Empty,
                metric.TimeNanoseconds??0,
                0,
                0,
                metric.AllocatedBytes??0,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                argumentKey,
                arguments);
        }
    }

    private static string CreateKey(string project,string typeFullName,string method,string argumentKey,int elementCount,DateTimeOffset measuredAt) =>
        string.Join("|",project,typeFullName,method,argumentKey,elementCount.ToString(CultureInfo.InvariantCulture),measuredAt.ToString("O",CultureInfo.InvariantCulture));

    private static bool IsSameArgument(string left,string right) =>
        string.IsNullOrWhiteSpace(right)||string.Equals(left,right,StringComparison.OrdinalIgnoreCase);

    private static bool IsSameType(string left,string right) =>
        string.IsNullOrWhiteSpace(right)
            ? true
            : !string.IsNullOrWhiteSpace(left)
                &&(string.Equals(left,right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(ShortTypeName(left),right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(left,ShortTypeName(right),StringComparison.OrdinalIgnoreCase));

    private static string ShortTypeName(string typeFullName){
        if(string.IsNullOrWhiteSpace(typeFullName)){
            return string.Empty;
        }

        var index = typeFullName.LastIndexOf('.');
        return index<0||index==typeFullName.Length-1
            ? typeFullName
            : typeFullName[(index+1)..];
    }

    private static void AtomicWrite<T>(string path,T value){
        var tmpPath = path+".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)??".");
        File.WriteAllText(tmpPath,JsonSerializer.Serialize(value,JsonOptions),Encoding.UTF8);
        File.Move(tmpPath,path,overwrite:true);
    }

    private sealed record BenchmarkResultStoreSnapshot(IReadOnlyList<BenchmarkStoredResult> Results);
}

public sealed class BenchmarkCsvDiscoveryService{
    private readonly IBenchmarkCsvService csvService;

    public BenchmarkCsvDiscoveryService(IBenchmarkCsvService csvService){
        this.csvService=csvService;
    }

    public BenchmarkCsvScanResult Scan(string benchmarkRoot,BenchmarkExecutionProfile executionProfile,BenchmarkSourceTab sourceTab){
        var profileRoot = Path.Combine(benchmarkRoot,BenchmarkExecutionProfileNames.ToFolderName(executionProfile));
        if(!Directory.Exists(profileRoot)){
            return new BenchmarkCsvScanResult([],[]);
        }

        var results = new List<BenchmarkStoredResult>();
        var options = new List<BenchmarkRunOption>();
        var sourceDefinition = BenchmarkSourceTabs.Get(sourceTab);
        foreach(var runRoot in Directory.EnumerateDirectories(profileRoot).Where(static path => IsRunNumberFolder(path))){
            var state = new BenchmarkStateManager(profileRoot).Load(runRoot);
            var sourceRoot = Path.Combine(runRoot,sourceDefinition.Id);
            var csvPaths = Directory.Exists(sourceRoot)
                ? Directory.EnumerateFiles(sourceRoot,"N*.csv",SearchOption.TopDirectoryOnly)
                .Where(static path => TryExtractElementCount(path)>0)
                .OrderBy(static path => TryExtractElementCount(path))
                .ToArray()
                : [];
            if(state is null&&csvPaths.Length==0){
                continue;
            }

            var runNumber = state?.RunNumber>0
                ? state.RunNumber
                : TryParseRunNumber(runRoot);
            var createdAt = state?.CreatedAt??Directory.GetCreationTimeUtc(runRoot);
            var updatedAt = state?.UpdatedAt??Directory.GetLastWriteTimeUtc(runRoot);
            var elementCounts = csvPaths
                .Select(static path => TryExtractElementCount(path))
                .Concat(state?.Jobs.Select(static job => job.ElementCount)??[])
                .Where(static elementCount => elementCount>0)
                .Distinct()
                .Order()
                .ToArray();
            var completedJobs = state?.Jobs.Count(static job => job.Status==JobStatus.Completed)??0;
            var totalJobs = state?.Jobs.Count??0;
            foreach(var path in csvPaths){
                var elementCount = TryExtractElementCount(path);
                IReadOnlyList<BenchmarkComparisonRow> rows;
                try{
                    rows=this.csvService.ReadComparisonCsv(path);
                }catch(InvalidDataException){
                    continue;
                }catch(IOException){
                    continue;
                }

                foreach(var row in rows){
                    foreach(var project in sourceDefinition.ProjectOrder){
                        var metrics = row.GetProject(project);
                        if(!metrics.HasMeasuredValue){
                            continue;
                        }

                        results.Add(new BenchmarkStoredResult(
                            project,
                            row.MethodName,
                            row.TypeFullName,
                            elementCount,
                            "Completed",
                            metrics.TimeNanoseconds?.ToString(CultureInfo.InvariantCulture)??string.Empty,
                            metrics.TimeNanoseconds,
                            metrics.AllocatedBytes,
                            updatedAt,
                            updatedAt,
                            path){
                            ArgumentKey=row.ArgumentKey,
                            Arguments=row.Arguments,
                        });
                    }
                }
            }

            options.Add(new BenchmarkRunOption(
                runNumber,
                FormatRunDisplay(runNumber,createdAt,completedJobs,totalJobs),
                runRoot,
                state?.ExecutionProfile??executionProfile,
                createdAt,
                updatedAt,
                completedJobs,
                totalJobs,
                elementCounts));
        }

        return new BenchmarkCsvScanResult(
            options.OrderByDescending(static option => option.RunNumber).ToArray(),
            results);
    }

    public static DateTimeOffset? TryExtractTimestamp(string path){
        return BenchmarkCsvTimestampParser.TryExtractTimestamp(path);
    }

    public static int TryExtractElementCount(string path){
        var text = Path.GetFileNameWithoutExtension(path);
        var match = Regex.Match(text,@"(?:^|[-_])N(?<n>\d+)(?:$|[-_])",RegexOptions.IgnoreCase);
        return match.Success&&int.TryParse(match.Groups["n"].Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)
            ? value
            : 0;
    }

    private static bool IsRunNumberFolder(string path) =>
        TryParseRunNumber(path)>0;

    private static int TryParseRunNumber(string runRoot){
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(runRoot));
        return name.Length==6&&int.TryParse(name,NumberStyles.None,CultureInfo.InvariantCulture,out var number)
            ? number
            : 0;
    }

    private static string FormatRunDisplay(int runNumber,DateTimeOffset createdAt,int completedJobs,int totalJobs) =>
        runNumber.ToString("D6",CultureInfo.InvariantCulture)+" "
        +createdAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm",CultureInfo.InvariantCulture)+" "
        +completedJobs.ToString(CultureInfo.InvariantCulture)+"/"+totalJobs.ToString(CultureInfo.InvariantCulture);

    private static DateTimeOffset? TryParseTimestamp(string text){
        foreach(var pattern in TimestampPatterns()){
            var match = Regex.Match(text,pattern.Regex,RegexOptions.IgnoreCase);
            if(!match.Success){
                continue;
            }

            var value = match.Groups["value"].Value;
            if(DateTime.TryParseExact(value,pattern.Format,CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out var date)){
                return new DateTimeOffset(date);
            }
        }

        return null;
    }

    private static IEnumerable<(string Regex,string Format)> TimestampPatterns(){
        yield return (@"(?<value>\d{8}-\d{6})","yyyyMMdd-HHmmss");
        yield return (@"(?<value>\d{14})","yyyyMMddHHmmss");
        yield return (@"(?<value>\d{4}-\d{2}-\d{2}[-_ ]\d{2}[-_]\d{2}[-_]\d{2})","yyyy-MM-dd HH-mm-ss");
        yield return (@"(?<value>\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})","yyyy-MM-dd_HH-mm-ss");
        yield return (@"(?<value>\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2})","yyyy-MM-dd-HH-mm-ss");
    }
}

public sealed class BenchmarkUiStateStore{
    private static readonly JsonSerializerOptions JsonOptions = new(){
        WriteIndented=true,
    };

    private readonly object fileGate = new();
    private readonly string path;

    public BenchmarkUiStateStore(string benchmarkRoot){
        this.path=Path.Combine(benchmarkRoot,"benchmark-ui-state.json");
    }

    public BenchmarkUiState Load(){
        if(!File.Exists(this.path)){
            return new BenchmarkUiState(null,null,new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase),new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase));
        }

        var state = JsonSerializer.Deserialize<BenchmarkUiStateSnapshot>(File.ReadAllText(this.path,Encoding.UTF8),JsonOptions);
        return new BenchmarkUiState(
            state?.SelectedRunNumber,
            state?.SelectedResultElementCount,
            new Dictionary<string,bool>(state?.ProjectCellSelections??new Dictionary<string,bool>(),StringComparer.OrdinalIgnoreCase),
            new Dictionary<string,bool>(state?.RowSelections??new Dictionary<string,bool>(),StringComparer.OrdinalIgnoreCase));
    }

    public void Save(BenchmarkUiState state){
        lock(this.fileGate){
            Directory.CreateDirectory(Path.GetDirectoryName(this.path)??".");
            var snapshot = new BenchmarkUiStateSnapshot(
                state.SelectedRunNumber,
                state.SelectedResultElementCount,
                null,
                state.ProjectCellSelections.OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase).ToDictionary(static item => item.Key,static item => item.Value,StringComparer.OrdinalIgnoreCase),
                state.RowSelections.OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase).ToDictionary(static item => item.Key,static item => item.Value,StringComparer.OrdinalIgnoreCase));
            AtomicWrite(this.path,JsonSerializer.Serialize(snapshot,JsonOptions));
        }
    }

    public static string CreateCellKey(string method,string project) =>
        method+"|"+project;

    private static void AtomicWrite(string path,string text){
        var tmpPath = path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            File.WriteAllText(tmpPath,text,Encoding.UTF8);
            File.Move(tmpPath,path,overwrite:true);
        }finally{
            TryDelete(tmpPath);
        }
    }

    private static void TryDelete(string path){
        try{
            if(File.Exists(path)){
                File.Delete(path);
            }
        }catch(IOException){
        }catch(UnauthorizedAccessException){
        }
    }

    private sealed record BenchmarkUiStateSnapshot(
        int? SelectedRunNumber,
        int? SelectedResultElementCount,
        DateTimeOffset? SelectedCsvTimestamp,
        Dictionary<string,bool> ProjectCellSelections,
        Dictionary<string,bool> RowSelections);
}

public sealed record BenchmarkUiState(
    int? SelectedRunNumber,
    int? SelectedResultElementCount,
    Dictionary<string,bool> ProjectCellSelections,
    Dictionary<string,bool> RowSelections);

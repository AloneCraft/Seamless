using Benchmark.Infrastructure;
using Benchmark.Scalable.Core;
using Seamless.Execution;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.OutputEncoding=System.Text.Encoding.UTF8;
try{
    var options=new Dictionary<string,string>(StringComparer.Ordinal);
    for(var index=1;index<args.Length;index+=2){if(index+1>=args.Length){throw new ArgumentException("Each option requires a value.");}options.Add(args[index],args[index+1]);}
    string Read(string key,string fallback="")=>options.GetValueOrDefault("--"+key,fallback);
    string Required(string key){var value=Read(key);ArgumentException.ThrowIfNullOrWhiteSpace(value,key);return value;}
    var directory=new DirectoryInfo(AppContext.BaseDirectory);
    while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){directory=directory.Parent;}
    var path=Read("database",BenchmarkDatabase.Resolve(directory?.FullName??Environment.CurrentDirectory));
    if(args.Length==0){Console.WriteLine("explain --request FILE | run --request FILE [--database FILE] | migrate --source FILE --writers-stopped true | history | baseline --measurement ID | compare --before ID --after ID [--experiment true] | profile --id NAME --training ID --validation ID --output FILE");return 0;}
    if(args[0]=="explain"){
        var json=new JsonSerializerOptions{PropertyNameCaseInsensitive=true,Converters={new JsonStringEnumConverter()}};
        var request=JsonSerializer.Deserialize<ScalableRequest>(await File.ReadAllTextAsync(Required("request")),json)??throw new InvalidDataException("Request required.");
        Console.WriteLine(JsonSerializer.Serialize(request.Explain(),new JsonSerializerOptions(json){WriteIndented=true}));return 0;
    }
    if(args[0]=="migrate"){
        Console.WriteLine(BenchmarkDatabase.ImportDynamic(Required("source"),path,bool.Parse(Read("writers-stopped","false"))));return 0;
    }
    var repository=new BenchmarkRepository(new(path));
    switch(args[0]){
        case "run":{
            var json=new JsonSerializerOptions{PropertyNameCaseInsensitive=true,Converters={new JsonStringEnumConverter()}};
            var request=JsonSerializer.Deserialize<ScalableRequest>(await File.ReadAllTextAsync(Required("request")),json)??throw new InvalidDataException("Request required.");
            using var cancellation=new CancellationTokenSource();
            Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;cancellation.Cancel();};
            var result=new ScalableRunner(repository).Run(request,p=>{if(p.Completed==0||p.Completed==p.Count){Console.Error.WriteLine(JsonSerializer.Serialize(p));}},cancellation.Token);
            Console.WriteLine(JsonSerializer.Serialize(result,BenchmarkRepository.JSON));break;
        }
        case "history":
            foreach(var run in repository.GetRuns(null,int.Parse(Read("offset","0")))){Console.WriteLine(JsonSerializer.Serialize(new{Run=run,Measurements=repository.GetMeasurements(run.ID)},BenchmarkRepository.JSON));}break;
        case "baseline":repository.SetBaseline(Required("measurement"));break;
        case "compare":{
            var mode=ComparisonMode.Regression;if(bool.Parse(Read("experiment","false"))){mode=ComparisonMode.Experiment;}
            Console.WriteLine(JsonSerializer.Serialize(repository.Compare(Required("before"),Required("after"),mode,double.Parse(Read("tolerance","0.05"),System.Globalization.CultureInfo.InvariantCulture))));break;
        }
        case "profile":
            repository.PublishProfile(Required("id"),Required("training"),Required("validation"));
            File.WriteAllText(Required("output"),repository.GetProfiles().ToJSON());break;
        default:throw new ArgumentException("Unknown command.");
    }
    return 0;
}catch(Exception exception){Console.Error.WriteLine(exception);return 1;}

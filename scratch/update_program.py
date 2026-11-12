with open('Benchmark/Benchmark.Dynamic.Worker/Program.cs', 'rb') as f:
    content = f.read().decode('utf-8')

content = content.replace('\r\n', '\n')

target = """        if(args.Length>0&&args[0]=="--fixed"){
            return Task.FromResult(Fixed(args));
        }"""

replacement = """        if(args.Length>0&&args[0]=="--fixed"){
            return Task.FromResult(Fixed(args));
        }
        if(args.Length>0&&args[0]=="--realistic"){
            return Task.FromResult(Realistic(args));
        }"""

assert target in content, "target not found"
content = content.replace(target, replacement, 1)

target2 = "    private static int Fixed(string[] args){"
realistic_code = """    private static int Realistic(string[] args){
        try{
            string Read(string name,string fallback){var index=Array.IndexOf(args,name);if(index<0){return fallback;}return args[index+1];}
            var database=Read("--database",Benchmark.Infrastructure.BenchmarkDatabase.Resolve(Environment.CurrentDirectory));
            var sourceKindStr=Read("--source-kind","Array");
            var sourceKind=Enum.Parse<Benchmark.Dynamic.Core.Realistic.RealisticSourceKind>(sourceKindStr,true);
            var distStr=Read("--distribution","UniformWithDuplicates");
            var dist=Enum.Parse<Benchmark.Dynamic.Core.Realistic.RealisticDistribution>(distStr,true);
            var result=new RealisticCaseRunner(new(new(database))).Run(
                sourceKind,
                int.Parse(Read("--count","256")),
                int.Parse(Read("--seed","42")),
                int.Parse(Read("--flags","15")),
                Read("--case","*")=="*"?null:Read("--case","*"),
                dist);
            foreach(var item in result){Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(item));}
            return result.All(r=>r.Passed)?0:1;
        }catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
    }

"""

assert target2 in content, "target2 not found"
content = content.replace(target2, realistic_code + target2, 1)

with open('Benchmark/Benchmark.Dynamic.Worker/Program.cs', 'wb') as f:
    f.write(content.replace('\n', '\r\n').encode('utf-8'))

print("Successfully updated Program.cs")

using Benchmark.Dynamic.Core;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

internal static class ChainCorpusProgress{
    internal static void Record(int depth,int stride,long ordinal,IReadOnlyList<string> operators,string terminal,WorkerExecutionStatus status,bool unloaded){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_RESULT_ROOT");if(string.IsNullOrWhiteSpace(root)){return;}
        Directory.CreateDirectory(root);
        var entry=new{Ordinal=ordinal,Operators=operators,Terminal=terminal,Status=status.ToString(),AssemblyUnloaded=unloaded};
        File.AppendAllText(Path.Combine(root,$"chain-corpus-depth-{depth}-stride-{stride}.jsonl"),JsonSerializer.Serialize(entry)+"\n");
    }
}

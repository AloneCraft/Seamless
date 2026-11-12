$db = "Work/UI/runs.db"
# Query SQLite if possible, or read from measurement engine directly
# Actually let's run a quick one-line script with dotnet to check allocations of System, Ideal, FLinq on Q05!
@"
using System;
using System.Linq;
using Benchmark.Dynamic.Core.Realistic;

var trades = RealisticDataGenerator.GenerateTrades(256, Seed256.Parse("42"), RealisticDistribution.UniformWithDuplicates);

// Warmup
for(int i=0; i<10; i++) {
    var a = GC.GetAllocatedBytesForCurrentThread();
    var r1 = trades.OrderByDescending(t => t.Price).ThenBy(t => t.Quantity).Take(10).ToArray();
    var b = GC.GetAllocatedBytesForCurrentThread();
}
"@

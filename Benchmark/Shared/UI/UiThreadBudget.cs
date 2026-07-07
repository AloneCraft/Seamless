using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;
internal static class UiThreadBudget{
    private sealed class Entry{
        public long Count;
        public double TotalMs;
        public double MaxMs;
    }

    private static readonly ConcurrentDictionary<string,Entry> entries = new(StringComparer.Ordinal);

    public static void Record(string name,double elapsedMs){
        var entry = entries.GetOrAdd(name,static _ => new Entry());
        lock(entry){
            entry.Count++;
            entry.TotalMs+=elapsedMs;
            if(elapsedMs>entry.MaxMs){
                entry.MaxMs=elapsedMs;
            }
        }
    }

    public static void Measure(string name,Action action){
        var start = Stopwatch.GetTimestamp();
        try{
            action();
        }finally{
            Record(name,Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    public static string DrainReport(){
        var items = entries.ToArray();
        entries.Clear();
        if(items.Length==0){
            return string.Empty;
        }

        return string.Join(" | ",items
            .OrderByDescending(static item => item.Value.TotalMs)
            .Take(8)
            .Select(static item => $"{item.Key}={item.Value.TotalMs:0}ms x{item.Value.Count} max={item.Value.MaxMs:0}"));
    }
}

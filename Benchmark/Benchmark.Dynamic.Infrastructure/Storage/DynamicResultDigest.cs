using Benchmark.Infrastructure;
using System.Text.Json;

namespace Benchmark.Dynamic.Infrastructure;

public static class DynamicResultDigest{
    public static string Compute(string? resultJSON){
        if(string.IsNullOrWhiteSpace(resultJSON)){return "";}
        using var document=JsonDocument.Parse(resultJSON);
        var root=document.RootElement;
        if(root.TryGetProperty("results",out var results)&&results.TryGetProperty("system",out var result)){
            return Fingerprint.Of(Fingerprint.CanonicalJSON(result.GetRawText()));
        }
        if(root.TryGetProperty("exceptions",out var exceptions)&&exceptions.TryGetProperty("system",out var exception)){
            return Fingerprint.OfJSON(new{Type=exception.GetProperty("type").GetString(),Message=exception.GetProperty("message").GetString()});
        }
        return "";
    }
}

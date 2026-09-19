using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Seamless.Database.SqlServer;

public static partial class SqlServerBatchParser {
    public static IReadOnlyList<string> Split(string script) {
        ArgumentNullException.ThrowIfNull(script);
        var batches=new List<string>();
        var builder=new StringBuilder();
        using var reader=new StringReader(script);
        while(reader.ReadLine() is { } line) {
            var match=GoLine().Match(line);
            if(!match.Success) {
                builder.AppendLine(line);
                continue;
            }
            var batch=builder.ToString().Trim();
            builder.Clear();
            if(batch.Length==0) {
                continue;
            }
            var count=match.Groups[1].Success
                ?int.Parse(match.Groups[1].Value,global::System.Globalization.CultureInfo.InvariantCulture)
                :1;
            if(count<=0) {
                throw new FormatException("A GO batch count must be greater than zero.");
            }
            for(var index=0;index<count;index++) {
                batches.Add(batch);
            }
        }
        var finalBatch=builder.ToString().Trim();
        if(finalBatch.Length>0) {
            batches.Add(finalBatch);
        }
        return batches.AsReadOnly();
    }

    [GeneratedRegex(@"^\s*GO(?:\s+(\d+))?\s*(?:--.*)?$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex GoLine();
}

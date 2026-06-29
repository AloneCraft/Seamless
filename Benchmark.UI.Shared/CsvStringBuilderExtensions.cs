using System.Text;

namespace Benchmark;

internal static class CsvStringBuilderExtensions{
    public static StringBuilder AppendCsv(this StringBuilder builder,string value){
        if(value.Contains('"',StringComparison.Ordinal)
            ||value.Contains(',',StringComparison.Ordinal)
            ||value.Contains('\r',StringComparison.Ordinal)
            ||value.Contains('\n',StringComparison.Ordinal)){
            builder.Append('"');
            builder.Append(value.Replace("\"","\"\"",StringComparison.Ordinal));
            builder.Append('"');
            return builder;
        }

        builder.Append(value);
        return builder;
    }
}

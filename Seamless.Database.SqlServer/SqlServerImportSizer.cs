using System.Text;

namespace Seamless.Database.SqlServer;

public static class SqlServerImportSizer {
    private const long MinimumPageSize = 1L<<12;
    private const long MaximumPageSize = 1L<<30;
    private const long MinimumMemorySize = MinimumPageSize*2;
    private const long RowOverheadReserve = 1L<<12;

    public static async Task<SqlServerImportSizing> MeasureAsync(
        string connectionString,
        IEnumerable<GeneratedTableBinding> tables,
        CancellationToken cancellationToken=default) {
        var measured=await MeasureTablesAsync(connectionString,tables,cancellationToken).ConfigureAwait(false);
        var maximumRowBytes=measured.Count==0?0:measured.Values.Max(static sizing=>sizing.MaximumRowBytes);
        var maximumTableRows=measured.Count==0?0:measured.Values.Max(static sizing=>sizing.MaximumTableRows);
        return Calculate(maximumRowBytes,maximumTableRows);
    }

    public static async Task<IReadOnlyDictionary<string,SqlServerImportSizing>> MeasureTablesAsync(
        string connectionString,
        IEnumerable<GeneratedTableBinding> tables,
        CancellationToken cancellationToken=default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(tables);
        await using var connection=new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var measured=new Dictionary<string,SqlServerImportSizing>(StringComparer.Ordinal);
        foreach(var table in tables) {
            await using var command=new SqlCommand(CreateMeasureQuery(table),connection) {
                CommandTimeout=0,
            };
            await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var maximumTableRows=0L;
            var maximumRowBytes=0L;
            if(!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
                measured.Add(table.ObjectName,Calculate(0,0));
                continue;
            }
            maximumTableRows=reader.GetInt64(0);
            maximumRowBytes=reader.GetInt64(1);
            measured.Add(table.ObjectName,Calculate(maximumRowBytes,maximumTableRows));
        }
        return measured;
    }

    public static SqlServerImportSizing Calculate(long maximumRowBytes,long maximumTableRows) {
        if(maximumRowBytes<0) {
            throw new ArgumentOutOfRangeException(nameof(maximumRowBytes));
        }
        if(maximumTableRows<0) {
            throw new ArgumentOutOfRangeException(nameof(maximumTableRows));
        }
        if(maximumRowBytes>MaximumPageSize-RowOverheadReserve) {
            throw new NotSupportedException(
                $"A source row requires {maximumRowBytes} bytes, exceeding the 1 GiB Tsavorite page limit.");
        }
        var pageSize=RoundUpPowerOfTwo(Math.Max(MinimumPageSize,maximumRowBytes+RowOverheadReserve));
        var indexSize=RoundUpPowerOfTwo(Math.Max(64,maximumTableRows));
        var memorySize=Math.Max(MinimumMemorySize,pageSize*2);
        return new(maximumRowBytes,maximumTableRows,indexSize,pageSize,pageSize,memorySize);
    }

    private static string CreateMeasureQuery(GeneratedTableBinding table) {
        var builder=new StringBuilder("SELECT COUNT_BIG(*),COALESCE(MAX(CONVERT(bigint,0)");
        foreach(var column in table.Columns) {
            if(column.IsSynthetic){builder.Append("+CONVERT(bigint,8)");continue;}
            builder.Append("+COALESCE(CONVERT(bigint,DATALENGTH(")
                .Append(Quote(column.SourceName))
                .Append(")),0)");
        }
        builder.Append("),0) FROM ")
            .Append(Quote(table.SourceSchema))
            .Append('.')
            .Append(Quote(table.SourceTable))
            .Append(';');
        return builder.ToString();
    }

    private static long RoundUpPowerOfTwo(long value) {
        if(value<=1) {
            return 1;
        }
        var result=1L;
        while(result<value) {
            if(result>MaximumPageSize&&value<=MaximumPageSize) {
                throw new InvalidOperationException("Could not calculate a power-of-two size.");
            }
            result=checked(result<<1);
        }
        return result;
    }

    private static string Quote(string value)=>$"[{value.Replace("]","]]",StringComparison.Ordinal)}]";
}

using System.Text;

namespace Seamless.Database.SqlServer;

public static class SqlServerImportSizer {
    private const long MinimumPageSize = 1L<<12;
    private const long MaximumPageSize = 1L<<30;
    private const long MinimumMemorySize = MinimumPageSize*2;
    private const long RowOverheadReserve = 1L<<12;
    // 符号化の型ごとの頭(型の識別など)。
    private const long EntryHeaderReserve = 64;

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
            // 保存の1件はキーと行の両方を符号化して持つ。キーの無い表は行の全部がキーなので、行の上限の2倍を1件の上限にする。
            maximumRowBytes=checked(reader.GetInt64(1)*2+EntryHeaderReserve);
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
        // 行を保存の形(canonical など)に符号化したときの長さの上限を、列ごとに足す(Seamless.Execution の CodecRegistry.Write)。
        // バイト列は1バイトごとに印の1バイトが付くので2倍、文字列は1文字2バイト(varchar は1バイトで1文字になりうるので2倍)、
        // 固定長の値は印と中身で最大17バイト(decimal・Guid)。どの列も「2×DATALENGTH+17」を超えない。
        foreach(var column in table.Columns) {
            if(column.IsSynthetic){builder.Append("+CONVERT(bigint,17)");continue;}
            builder.Append("+2*COALESCE(CONVERT(bigint,DATALENGTH(")
                .Append(Quote(column.SourceName))
                .Append(")),0)+17");
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

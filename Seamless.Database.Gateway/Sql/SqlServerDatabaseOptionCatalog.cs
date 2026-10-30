using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Sql;

internal static class SqlServerDatabaseOptionCatalog{
    internal static readonly ColumnMetadata[] Columns=[
        new("is_in_standby",typeof(bool),"bit",false,false,18),
        new("is_auto_create_stats_incremental_on",typeof(bool),"bit",false,false,19),
        new("is_ansi_null_default_on",typeof(bool),"bit",false,false,20),
        new("is_ansi_nulls_on",typeof(bool),"bit",false,false,21),
        new("is_ansi_padding_on",typeof(bool),"bit",false,false,22),
        new("is_ansi_warnings_on",typeof(bool),"bit",false,false,23),
        new("is_arithabort_on",typeof(bool),"bit",false,false,24),
        new("is_auto_create_stats_on",typeof(bool),"bit",false,false,25),
        new("is_auto_shrink_on",typeof(bool),"bit",false,false,26),
        new("is_auto_update_stats_on",typeof(bool),"bit",false,false,27),
        new("is_cursor_close_on_commit_on",typeof(bool),"bit",false,false,28),
        new("is_concat_null_yields_null_on",typeof(bool),"bit",false,false,29),
        new("is_numeric_roundabort_on",typeof(bool),"bit",false,false,30),
        new("is_quoted_identifier_on",typeof(bool),"bit",false,false,31),
        new("is_recursive_triggers_on",typeof(bool),"bit",false,false,32),
        new("is_local_cursor_default",typeof(bool),"bit",false,false,33),
        new("page_verify_option",typeof(byte),"tinyint",false,false,34),
        new("snapshot_isolation_state",typeof(byte),"tinyint",false,false,35),
        new("is_db_chaining_on",typeof(bool),"bit",false,false,36),
        new("is_auto_update_stats_async_on",typeof(bool),"bit",false,false,37),
        new("is_date_correlation_on",typeof(bool),"bit",false,false,38),
        new("is_broker_enabled",typeof(bool),"bit",false,false,39),
        new("is_trustworthy_on",typeof(bool),"bit",false,false,40),
        new("is_parameterization_forced",typeof(bool),"bit",false,false,41),
        new("is_auto_close_on",typeof(bool),"bit",false,false,42)
    ];

    internal static object?[] Expand(object?[] row){
        if(row.Length!=18){throw new ArgumentException("The sys.databases row must contain 18 base columns.",nameof(row));}
        return [..row,false,false,true,true,true,true,true,false,false,false,false,true,false,true,false,false,(byte)0,(byte)0,false,false,false,false,false,false,false,false,false];
    }

    internal static QueryResult Mirroring()=>new([
        new("database_id",typeof(int),"int",false,false,0),
        new("mirroring_redo_queue_type",typeof(string),"nvarchar",true,false,1,60),
        new("mirroring_redo_queue",typeof(long),"bigint",true,false,2),
        new("mirroring_connection_timeout",typeof(int),"int",true,false,3),
        new("mirroring_role",typeof(byte),"tinyint",true,false,4),
        new("mirroring_state",typeof(byte),"tinyint",true,false,5)
    ],Array.Empty<object?[]>(),0);
}

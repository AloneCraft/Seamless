using System;
using Microsoft.Data.Sqlite;

using var conn = new SqliteConnection(Data Source=Work/UI/runs.db);
conn.Open();
using var cmd = conn.CreateCommand();
cmd.CommandText = SELECT WorkloadID, Implementation, MedianNanoseconds, MedianAllocatedBytes FROM Measurements WHERE WorkloadID LIKE '%Q05%' ORDER BY RowId DESC LIMIT 10;;
using var reader = cmd.ExecuteReader();
while (reader.Read()) {
    Console.WriteLine(${reader.GetString(0)} | {reader.GetString(1)} | {reader.GetDouble(2):F1} ns | {reader.GetInt64(3)} B);
}
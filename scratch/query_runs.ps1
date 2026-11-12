Add-Type -Path 'G:\GitHub\Seamless\Benchmark\Benchmark.Dynamic.UI\bin\Debug\net10.0-windows\Microsoft.Data.Sqlite.dll'
$conn = New-Object Microsoft.Data.Sqlite.SqliteConnection('Data Source=Work\UI\runs.db')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT WorkloadID, Implementation, MedianNanoseconds, MedianAllocatedBytes FROM Measurements WHERE WorkloadID LIKE '%Q05%' ORDER BY RowId DESC LIMIT 15"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    $w = $reader.GetString(0)
    $i = $reader.GetString(1)
    $ns = $reader.GetDouble(2)
    $b = $reader.GetInt64(3)
    Write-Output "$w | $i | $ns ns | $b B"
}
$conn.Close()

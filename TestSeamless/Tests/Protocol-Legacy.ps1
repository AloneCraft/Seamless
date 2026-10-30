$ErrorActionPreference='Stop'
$phase='configuration'
$connection=$null
try {
    $connectionString=$env:SEAMLESS_PROTOCOL_CONNECTION_STRING
    if([string]::IsNullOrWhiteSpace($connectionString)){throw 'Connection string environment variable is required.'}
    $phase='connect'
    $connection=New-Object System.Data.SqlClient.SqlConnection $connectionString
    $connection.Open()
    $phase='parameters'
    $command=$connection.CreateCommand()
    $command.CommandText='SELECT Value FROM dbo.Rows WHERE Id=@id'
    [void]$command.Parameters.Add('@id',[System.Data.SqlDbType]::Int)
    $command.Parameters['@id'].Value=1
    $command.Prepare()
    if($command.ExecuteScalar() -ne 10){throw 'Unexpected prepared result.'}
    $command.Dispose()
    $phase='rollback'
    $transaction=$connection.BeginTransaction()
    $command=$connection.CreateCommand();$command.Transaction=$transaction
    $command.CommandText='INSERT INTO dbo.Rows (Id,Value) VALUES (10001,77)'
    [void]$command.ExecuteNonQuery();$command.Dispose();$transaction.Rollback();$transaction.Dispose()
    $command=$connection.CreateCommand();$command.CommandText='SELECT Value FROM dbo.Rows WHERE Id=10001'
    if($null -ne $command.ExecuteScalar()){throw 'Rollback did not discard the row.'};$command.Dispose()
    $phase='commit'
    $transaction=$connection.BeginTransaction()
    $command=$connection.CreateCommand();$command.Transaction=$transaction
    $command.CommandText='INSERT INTO dbo.Rows (Id,Value) VALUES (10002,88)'
    [void]$command.ExecuteNonQuery();$command.Dispose();$transaction.Commit();$transaction.Dispose()
    $command=$connection.CreateCommand();$command.CommandText='SELECT Value FROM dbo.Rows WHERE Id=10002'
    if($command.ExecuteScalar() -ne 88){throw 'Commit did not persist the row.'};$command.Dispose()
    Write-Output 'System.Data.SqlClient: connection, parameters, commit, and rollback passed.'
    exit 0
} catch {
    [Console]::Error.WriteLine('System.Data.SqlClient failed at '+$phase+' ('+$_.Exception.GetType().FullName+').')
    exit 1
} finally {
    if($null -ne $connection){$connection.Dispose()}
}

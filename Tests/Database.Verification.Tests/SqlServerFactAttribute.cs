namespace Database.Verification.Tests;

public sealed class SqlServerFactAttribute:Xunit.FactAttribute{
    public SqlServerFactAttribute(){
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION"))){
            this.Skip="SEAMLESS_SQLSERVER_TEST_CONNECTIONが未設定のため、実SQL Server連携は未検証です。";
        }
    }
}

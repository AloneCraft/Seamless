using Seamless.Database;

// 取引・読み込みの入口を生成させるため、生成器の目印のある名前空間へ置く。
namespace Database.Verification.Tests.TransferAdapterFixture;

internal static partial class DatabaseGenerator;

public readonly record struct Row(int Id);

public sealed partial class Fixture():DatabaseContainer(DatabaseContainerOptions.InMemory){
    [ImportedDatabaseObject("dbo","Items")]
    public Table<Row,Row>Items=>this.Table<Row,Row>(row=>row);
}

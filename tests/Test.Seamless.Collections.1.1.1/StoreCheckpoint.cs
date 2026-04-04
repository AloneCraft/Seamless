using Seamless.Collections;
namespace Test.Seamless.Collections111;
public class StoreCheckpoint {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest111_"+Guid.NewGuid().ToString("N"));
    [Fact, Trait("Category",Unit)]
    public async Task SaveAsync_CheckpointDir未設定_InvalidOperationExceptionをスロー() {
        using var store = Store.Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveAsync());
    }
}

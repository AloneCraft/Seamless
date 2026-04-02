using Seamless.Collections;
namespace Test.Seamless.SessionFunctions;

[Collection("Sequential")]
public class Add {
    [Fact, Trait("Category","Coverage")]
    public void AddSessionFunctions_RMWで重複拒否できる() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.Equal(TryAddResult.Success,dict.TryAdd(1,"v1"));
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(1,"v2"));
    }

    [Fact, Trait("Category","Coverage")]
    public void AddSessionFunctions_Upsertで実セッション経由のWriterをカバーする() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict[200]="new";
        dict[200]="updated";
    }
}

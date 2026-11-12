namespace Database.Verification.Tests;

public sealed class RowBagVerifier{
    [Fact]public async Task CompareAsync(){
        var tempRoot=Environment.GetEnvironmentVariable("SEAMLESS_COMPARISON_TEMP_ROOT")??Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??(Directory.Exists(@"F:\Seamless")?@"F:\Seamless\temp":Path.GetTempPath());
        var root=Path.Combine(tempRoot,"SeamlessVerificationTests");
        static async IAsyncEnumerable<object?[]>Rows(int count,bool reverse,bool change){
            for(var i=0;i<count;i++){
                var index=i;if(reverse){index=count-1-i;}
                var value=(index%51).ToString();if(change&&index==22){value="different";}
                yield return [index%51,"行"+value,null];
            }
            await Task.CompletedTask;
        }
        var verifier=new global::Database.Verification.RowBagVerifier();
        Assert.True((await verifier.CompareAsync(Rows(1000,false,false),Rows(1000,true,false),root,4096)).Equal);
        Assert.False((await verifier.CompareAsync(Rows(1000,false,false),Rows(999,true,false),root,4096)).Equal);
        Assert.False((await verifier.CompareAsync(Rows(1000,false,false),Rows(1000,true,true),root,4096)).Equal);
        Assert.True((await verifier.CompareOrderedAsync(Rows(1000,false,false),Rows(1000,false,false))).Equal);
        Assert.False((await verifier.CompareOrderedAsync(Rows(1000,false,false),Rows(1000,true,false))).Equal);
        Assert.False((await verifier.CompareOrderedAsync(Rows(1000,false,false),Rows(999,false,false))).Equal);
    }
}

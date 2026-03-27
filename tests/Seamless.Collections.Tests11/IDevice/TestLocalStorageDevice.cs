using Tsavorite.core;

using Xunit;
namespace Seamless.Collections.Tests;
public class TestLocalStorageDevice{
    [Fact]public void Test0(){
        var folder=Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
        using var LogDevice = new LocalStorageDevice(Path.Combine(folder, "log.data"),deleteOnClose:true,useIoCompletionPort:true);
        using var ObjectLogDevice = new LocalStorageDevice(Path.Combine(folder + "_obj", "log.data"),deleteOnClose:true,useIoCompletionPort:true);
        using var Store=new DictionaryStore<int,string>(LogDevice,ObjectLogDevice);
    }
}

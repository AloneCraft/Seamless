using Xunit;
namespace Seamless.Collections.Tests;

public class ScanIteratorFunctions {
    [Fact, Trait("Category","Coverage")]
    public void OnException_例外を無視する() {
        var iter = new Collections.KeyIterator<int>();
        iter.OnException(new Exception("test"),0);
    }
}

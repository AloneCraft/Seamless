using Seamless.Collections;
namespace Test.Seamless.ScanIteratorFunctions;

public partial class ScanIteratorFunctions {
    public partial void OnException_例外を無視する() {
        var iter = new KeyIterator<int>();
        iter.OnException(new Exception("test"),0);
    }
}

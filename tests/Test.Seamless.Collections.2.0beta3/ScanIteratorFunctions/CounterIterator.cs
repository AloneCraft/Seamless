namespace Test.Seamless.ScanIteratorFunctions;

public partial class CounterIterator {
    public partial void OnException_例外を無視する() {
        var iter = new global::Seamless.ScanIteratorFunctions.CounterIterator();
        iter.OnException(new Exception("test"),0);
    }
}

namespace Seamless.ScanIteratorFunctions;
/// <summary>
/// IteratorLookup(ref CounterIterator) で使う構造体。スキャンしたレコード数を数えるだけ。
/// refで渡しているのでstructで問題なさそうだが実際にはclassで無いとだめ。実装の問題か？
/// </summary>
public partial class CounterIteratorBase{
    public long Counter;
    public bool OnStart(long beginAddress,long endAddress) {
        return true;
    }
    public void OnException(Exception exception,long numberOfRecords) { }
    public void OnStop(bool completed,long numberOfRecords) {
        this.Counter=numberOfRecords;
    }
}

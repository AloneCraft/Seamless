namespace Test.Seamless.Collections;


public partial class MemoryOnlyDevice {
    [Fact, Trait("Category",Unit)]
    public partial void ctor_正常に生成できる();
    [Fact, Trait("Category",Unit)]
    public partial void ReadAsync_IOExceptionを投げる();   
    [Fact, Trait("Category",Unit)]
    public partial void WriteAsync_IOExceptionを投げる();
    [Fact, Trait("Category",Unit)]
    public partial void RemoveSegmentAsync_例外を投げない();
    [Fact,Trait("Category",Unit)]
    public partial void Dispose_安全に呼び出せる();
}

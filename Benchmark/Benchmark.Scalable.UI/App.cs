namespace Benchmark.Scalable.UI;
public sealed class App:Application{
    protected override Window CreateWindow(IActivationState? 状態)=>new(new MainPage()){Title="Seamless Scalable Benchmark"};
}

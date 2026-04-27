using BenchmarkDotNet.Running;
using BenchmarkExe.Benchmarks;

var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);

if(BenchmarkOutputLayout.ShouldSplitByCategory(args)) {
	foreach(var category in BenchmarkOutputLayout.KnownCategories) {
		switcher.Run(BenchmarkOutputLayout.WithCategoryFilter(args,category),BenchmarkOutputLayout.CreateRuntimeConfig(category));
	}
}
else {
	var category = BenchmarkOutputLayout.TryGetCategoryFromArgs(args)??BenchmarkOutputLayout.AllCategory;
#if DEBUG
	switcher.Run(args,BenchmarkOutputLayout.CreateRuntimeConfig(category));
#else
	switcher.Run(args,BenchmarkOutputLayout.CreateRuntimeConfig(category));
#endif
}

using Seamless.UI.Storage;

namespace UI;

internal sealed class RunnerViewModelDependencies {
    public RunnerViewModelDependencies(UiRunStore uiRunStore,string benchmarkRoot) {
        this.UiRunStore=uiRunStore;
        this.BenchmarkRoot=benchmarkRoot;
    }

    public UiRunStore UiRunStore { get; }
    public string BenchmarkRoot { get; }
    public ICatalogService? CatalogService { get; init; }
    public ICpuTopologyService? CpuTopologyService { get; init; }
    public IRunService? RunService { get; init; }
    public IComparisonService? ComparisonService { get; init; }
    public IUiDispatcher? UiDispatcher { get; init; }
    public IUiPreferences? UiPreferences { get; init; }
    public IRunOrchestrator? RunOrchestrator { get; init; }
    public IResultPipelineApplier? ResultPipelineApplier { get; init; }
    public ISelectionController? SelectionController { get; init; }
    public IChartController? ChartController { get; init; }
    public ISortController? SortController { get; init; }
    public ICpuTopologyController? CpuTopologyController { get; init; }
    public IRunStatePersistence? RunStatePersistence { get; init; }
    public IRunVisualScheduler? RunVisualScheduler { get; init; }

    internal ICatalogService CreateCatalogService() =>
        this.CatalogService??new CatalogService();

    internal ICpuTopologyService CreateCpuTopologyService() =>
        this.CpuTopologyService??new CpuTopologyService();

    internal IComparisonService CreateComparisonService() =>
        this.ComparisonService??new ComparisonService(new ResultService());

    internal IRunService CreateRunService(IComparisonService comparisonService) =>
        this.RunService??new RunService(
            new QueueService(),
            new WorkerProcessService(),
            comparisonService);

    internal IUiDispatcher CreateUiDispatcher() =>
        this.UiDispatcher??new MauiUiDispatcher();

    internal IUiPreferences CreateUiPreferences() =>
        this.UiPreferences??new MauiUiPreferences();

    internal IRunOrchestrator CreateRunOrchestrator(IRunOrchestratorContext context) =>
        this.RunOrchestrator??new RunOrchestrator(context);

    internal IResultPipelineApplier CreateResultPipelineApplier(IResultPipelineApplierContext context) =>
        this.ResultPipelineApplier??new ResultPipelineApplier(context,this.RunVisualScheduler??new SystemRunVisualScheduler());

    internal ISelectionController CreateSelectionController(ISelectionControllerContext context) =>
        this.SelectionController??new SelectionController(context);

    internal IChartController CreateChartController(IChartControllerContext context) =>
        this.ChartController??new ChartController(context);

    internal ISortController CreateSortController(ISortControllerContext context) =>
        this.SortController??new DefaultSortController(context);

    internal ICpuTopologyController CreateCpuTopologyController(ICpuTopologyControllerContext context,ICpuTopologyService cpuTopologyService) =>
        this.CpuTopologyController??new DefaultCpuTopologyController(context,cpuTopologyService);

    internal IRunStatePersistence CreateRunStatePersistence(IRunStatePersistenceContext context) =>
        this.RunStatePersistence??new RunStatePersistence(context);
}



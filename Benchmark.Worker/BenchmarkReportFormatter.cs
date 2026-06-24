using System.Globalization;
using System.Text;

namespace Benchmark.Runner.Uno;

public static class BenchmarkReportFormatter{
    public static string BuildCsv(IReadOnlyList<ReportRow> rows){
        var writer = new StringBuilder();
        writer.AppendLine("Project,Method,TypeFullName,FullFunctionName,Status,Mean,Error,StdDev,AllocatedBytes,PeakRssBytes,EstimatedPeakRssBytes,LogicalCpu,Core,CpuDisplay,StartedAt,FinishedAt,ArtifactFolder,ExitCode");
        foreach(var row in rows){
            writer.AppendCsv(row.Project).Append(',');
            writer.AppendCsv(row.Method).Append(',');
            writer.AppendCsv(row.TypeFullName).Append(',');
            writer.AppendCsv(row.FullFunctionName).Append(',');
            writer.AppendCsv(row.Status).Append(',');
            writer.AppendCsv(row.Mean).Append(',');
            writer.AppendCsv(row.Error).Append(',');
            writer.AppendCsv(row.StdDev).Append(',');
            writer.AppendCsv(row.AllocatedBytes).Append(',');
            writer.AppendCsv(row.PeakRssBytes).Append(',');
            writer.AppendCsv(row.EstimatedPeakRssBytes).Append(',');
            writer.AppendCsv(row.LogicalCpu).Append(',');
            writer.AppendCsv(row.Core).Append(',');
            writer.AppendCsv(row.CpuDisplay).Append(',');
            writer.AppendCsv(row.StartedAt).Append(',');
            writer.AppendCsv(row.FinishedAt).Append(',');
            writer.AppendCsv(row.ArtifactFolder).Append(',');
            writer.AppendCsv(row.ExitCode?.ToString(CultureInfo.InvariantCulture)??string.Empty).AppendLine();
        }

        return writer.ToString();
    }

    public static string BuildCpuTimelineHtml(IReadOnlyList<ReportRow> rows,DateTimeOffset generatedAt){
        var items = rows
            .Where(static row => GetReportSeries(row.Project) is not null)
            .Select(static row => new TimelineItem(row,ParseDateTimeOffset(row.StartedAt),ParseDateTimeOffset(row.FinishedAt)))
            .Where(static item => item.StartedAt is not null&&item.FinishedAt is not null&&item.FinishedAt>=item.StartedAt)
            .ToArray();
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"en\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>Benchmark CPU Timeline</title>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    body{font-family:Segoe UI,Arial,sans-serif;margin:20px;color:#202020;background:#fffefa;}");
        writer.AppendLine("    h1{font-size:22px;margin:0 0 8px;}");
        writer.AppendLine("    p{margin:0 0 16px;color:#555;}");
        writer.AppendLine("    .legend{display:flex;gap:18px;margin:0 0 14px;font-size:13px;}");
        writer.AppendLine("    .legend span{display:inline-flex;align-items:center;gap:6px;}");
        writer.AppendLine("    .swatch{width:12px;height:12px;border-radius:2px;display:inline-block;}");
        writer.AppendLine("    .run-0{background:#3a6ea5;}");
        writer.AppendLine("    .run-1{background:#b94b48;}");
        writer.AppendLine("    .run-2{background:#4f8b57;}");
        writer.AppendLine("    .run-3{background:#8a63a8;}");
        writer.AppendLine("    .run-4{background:#b7792f;}");
        writer.AppendLine("    .run-5{background:#2f7f86;}");
        writer.AppendLine("    .run-6{background:#6c6f78;}");
        writer.AppendLine("    .run-7{background:#8f5a42;}");
        writer.AppendLine("    .timeline{display:grid;grid-template-columns:150px minmax(900px,1fr);gap:0 12px;font-size:12px;overflow-x:auto;}");
        writer.AppendLine("    .axis{grid-column:2;position:relative;height:26px;border-left:1px solid #d5d1c4;border-right:1px solid #d5d1c4;}");
        writer.AppendLine("    .axis.bottom{border-top:1px solid #d5d1c4;margin-top:4px;}");
        writer.AppendLine("    .axis.top{border-bottom:1px solid #d5d1c4;margin-bottom:4px;}");
        writer.AppendLine("    .tick{position:absolute;top:0;bottom:0;border-left:1px solid #d5d1c4;}");
        writer.AppendLine("    .tick span{position:absolute;left:4px;color:#555;white-space:nowrap;}");
        writer.AppendLine("    .axis.top .tick span{top:2px;}");
        writer.AppendLine("    .axis.bottom .tick span{bottom:2px;}");
        writer.AppendLine("    .cpu{padding:10px 0;border-top:1px solid #e4dfd2;white-space:nowrap;font-weight:600;}");
        writer.AppendLine("    .lane{position:relative;height:34px;border-top:1px solid #e4dfd2;background:repeating-linear-gradient(90deg,#fbfaf4 0,#fbfaf4 calc(10% - 1px),#eee8dc calc(10% - 1px),#eee8dc 10%);}");
        writer.AppendLine("    .bar{position:absolute;top:6px;height:22px;border-radius:3px;color:#fff;overflow:hidden;white-space:nowrap;text-overflow:ellipsis;padding:3px 5px;box-sizing:border-box;font-size:11px;}");
        writer.AppendLine("    .bar.failed{outline:2px solid #7a1f1f;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>Benchmark CPU Timeline</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(generatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        writer.AppendLine("  <div class=\"legend\"><span><i class=\"swatch run-0\"></i>1st on core</span><span><i class=\"swatch run-1\"></i>2nd</span><span><i class=\"swatch run-2\"></i>3rd</span><span><i class=\"swatch run-3\"></i>4th</span><span><i class=\"swatch run-4\"></i>5th</span><span><i class=\"swatch run-5\"></i>6th</span><span><i class=\"swatch run-6\"></i>7th</span><span><i class=\"swatch run-7\"></i>8th+</span></div>");

        if(items.Length==0){
            writer.AppendLine("  <p>No timed CPU rows were available.</p>");
            writer.AppendLine("</body>");
            writer.AppendLine("</html>");
            return writer.ToString();
        }

        var start = items.Min(static item => item.StartedAt!.Value);
        var end = items.Max(static item => item.FinishedAt!.Value);
        var totalSeconds = Math.Max(0.001,(end-start).TotalSeconds);
        writer.AppendLine("  <div class=\"timeline\">");
        writer.AppendLine("    <div></div>");
        AppendTimelineAxis(writer,totalSeconds,"top",TimelineAxisLabelStyle.Seconds);
        foreach(var group in items
            .GroupBy(static item => item.Row.LogicalCpu,StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => ParseIntOrMax(group.Key))){
            var first = group.OrderBy(static item => item.Row.CpuDisplay,StringComparer.OrdinalIgnoreCase).First().Row;
            writer.Append("    <div class=\"cpu\">").Append(EscapeHtml(first.CpuDisplay)).Append(" / ").Append(EscapeHtml(first.Core)).AppendLine("</div>");
            writer.AppendLine("    <div class=\"lane\">");
            var runIndex = 0;
            foreach(var item in group.OrderBy(static item => item.StartedAt)){
                var left = (item.StartedAt!.Value-start).TotalSeconds/totalSeconds*100;
                var width = Math.Max(0.2,(item.FinishedAt!.Value-item.StartedAt.Value).TotalSeconds/totalSeconds*100);
                var runClass = "run-"+Math.Min(runIndex,7).ToString(CultureInfo.InvariantCulture);
                writer.Append("      <div class=\"bar ").Append(runClass);
                if(!string.Equals(item.Row.Status,"Completed",StringComparison.OrdinalIgnoreCase)){
                    writer.Append(" failed");
                }
                writer.Append("\" style=\"left:")
                    .Append(left.ToString("0.###",CultureInfo.InvariantCulture))
                    .Append("%;width:")
                    .Append(width.ToString("0.###",CultureInfo.InvariantCulture))
                    .Append("%\" title=\"")
                    .Append(EscapeHtml(item.Row.FullFunctionName))
                    .Append(" | ")
                    .Append(EscapeHtml(FormatElapsed(item.StartedAt.Value-start)))
                    .Append(" - ")
                    .Append(EscapeHtml(FormatElapsed(item.FinishedAt.Value-start)))
                    .Append(" | ")
                    .Append((runIndex+1).ToString(CultureInfo.InvariantCulture))
                    .Append(" on core | ")
                    .Append(EscapeHtml(item.Row.Status))
                    .Append("\">")
                    .Append(EscapeHtml(item.Row.FullFunctionName))
                    .AppendLine("</div>");
                runIndex++;
            }
            writer.AppendLine("    </div>");
        }
        writer.AppendLine("    <div></div>");
        AppendTimelineAxis(writer,totalSeconds,"bottom",TimelineAxisLabelStyle.Clock);
        writer.AppendLine("  </div>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        return writer.ToString();
    }

    public static string BuildHtml(IReadOnlyList<ReportRow> rows,DateTimeOffset generatedAt){
        var comparisonRows = CreateComparisonRows(rows);
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"en\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>Benchmark Comparison</title>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    body{font-family:Segoe UI,Arial,sans-serif;margin:20px;color:#202020;background:#fffefa;}");
        writer.AppendLine("    h1{font-size:22px;margin:0 0 8px;}");
        writer.AppendLine("    p{margin:0 0 16px;color:#555;}");
        writer.AppendLine("    table{border-collapse:collapse;width:100%;font-size:13px;}");
        writer.AppendLine("    th,td{border:1px solid #d5d1c4;padding:5px 7px;text-align:left;white-space:nowrap;}");
        writer.AppendLine("    th{background:#f0ede4;font-weight:600;}");
        writer.AppendLine("    td.num{text-align:right;}");
        writer.AppendLine("    tbody tr:nth-child(even) td{background:#faf8f0;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>Benchmark Comparison</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(generatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        writer.AppendLine("  <table>");
        writer.AppendLine("    <thead><tr><th>Method</th><th>linq</th><th>zlinq</th><th>flinq</th><th>persistedlinq</th><th>persistedflinq</th><th>tsavoritelinq</th><th>tsavoriteflinq</th></tr></thead>");
        writer.AppendLine("    <tbody>");
        foreach(var row in comparisonRows){
            writer.Append("      <tr><td>").Append(EscapeHtml(row.Method)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.Linq.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.ZLinq.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.FLinq.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.PersistedLinq.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.PersistedFLinq.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.TsavoriteLinq.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.TsavoriteFLinq.Mean)).AppendLine("</td></tr>");
        }
        writer.AppendLine("    </tbody>");
        writer.AppendLine("  </table>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        return writer.ToString();
    }

    public static string BuildChartsHtml(IReadOnlyList<ReportRow> rows,DateTimeOffset generatedAt){
        var comparisonRows = CreateComparisonRows(rows);
        var comparisonJson = BuildComparisonRowsJson(comparisonRows);
        var flinqSlowJson = BuildFLinqSlowRowsJson(comparisonRows);
        var runRowsJson = BuildReportRowsJson(rows);
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"ja\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>ベンチマークチャート</title>");
        writer.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/apexcharts\"></script>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    :root{color-scheme:light;--bg:#fffefa;--ink:#202020;--muted:#5b625f;--line:#d5d1c4;--panel:#fbfaf4;--head:#f0ede4;--linq:#3a6ea5;--zlinq:#5b8c5a;--flinq:#c06c3e;--persistedlinq:#8a63a8;--persistedflinq:#b7792f;--tsavoritelinq:#2f7f86;--tsavoriteflinq:#b94b48;}");
        writer.AppendLine("    body{font-family:Segoe UI,Arial,sans-serif;margin:20px;color:var(--ink);background:var(--bg);}");
        writer.AppendLine("    h1{font-size:22px;margin:0 0 8px;}p{margin:0 0 16px;color:var(--muted);}");
        writer.AppendLine("    .tabs{display:flex;gap:6px;flex-wrap:wrap;margin:0 0 12px;border-bottom:1px solid var(--line);}");
        writer.AppendLine("    .tab{border:1px solid var(--line);border-bottom:0;background:var(--head);padding:7px 12px;cursor:pointer;font:inherit;color:var(--ink);}");
        writer.AppendLine("    .tab.active{background:#fff;border-top:3px solid #3a6ea5;padding-top:5px;font-weight:600;}");
        writer.AppendLine("    .toolbar{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin:0 0 14px;font-size:13px;}");
        writer.AppendLine("    select{font:inherit;padding:4px 8px;border:1px solid #c8c2b4;background:#fff;color:var(--ink);}");
        writer.AppendLine("    #chart{min-height:440px;border:1px solid var(--line);background:#fff;padding:10px;}");
        writer.AppendLine("    .note{font-size:12px;color:var(--muted);margin:10px 0 0;}");
        writer.AppendLine("    .fallback{overflow:auto;border:1px solid var(--line);background:#fff;}");
        writer.AppendLine("    table{border-collapse:collapse;width:100%;font-size:12px;}th,td{border:1px solid var(--line);padding:5px 7px;text-align:left;vertical-align:top;}th{background:var(--head);font-weight:600;white-space:nowrap;}td.num{text-align:right;white-space:nowrap;}tbody tr:nth-child(even) td{background:var(--panel);}");
        writer.AppendLine("    .metric-grid{display:grid;grid-template-columns:repeat(4,minmax(150px,1fr));gap:10px;margin:0 0 14px;}.metric{border:1px solid var(--line);background:#fff;padding:10px;}.metric strong{display:block;font-size:20px;}.metric span{color:var(--muted);font-size:12px;}");
        writer.AppendLine("    .sankey{width:100%;min-height:520px;}.node{fill:#fff;stroke:var(--line);stroke-width:1;}.node-label{font-size:12px;fill:var(--ink);}.flow{fill:none;stroke-opacity:.45;}.flow:hover{stroke-opacity:.8;}.axis-label{font-size:11px;fill:var(--muted);}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>ベンチマークチャート</h1>");
        writer.AppendLine($"  <p>生成日時: {EscapeHtml(generatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        writer.AppendLine("  <div class=\"tabs\" id=\"tabs\"></div>");
        writer.AppendLine("  <div class=\"toolbar\"><label for=\"chartMode\">チャート</label><select id=\"chartMode\"></select></div>");
        writer.AppendLine("  <div class=\"metric-grid\" id=\"metrics\"></div>");
        writer.AppendLine("  <div id=\"chart\"></div>");
        writer.AppendLine("  <p class=\"note\" id=\"note\"></p>");
        writer.AppendLine("  <script>");
        writer.Append("    const comparisonRows=").Append(comparisonJson).AppendLine(";");
        writer.Append("    const flinqSlowRows=").Append(flinqSlowJson).AppendLine(";");
        writer.Append("    const runRows=").Append(runRowsJson).AppendLine(";");
        writer.AppendLine("    const seriesKeys=['linq','zlinq','flinq','persistedlinq','persistedflinq','tsavoritelinq','tsavoriteflinq'];");
        writer.AppendLine("    const names={linq:'linq',zlinq:'zlinq',flinq:'flinq',persistedlinq:'persistedlinq',persistedflinq:'persistedflinq',tsavoritelinq:'tsavoritelinq',tsavoriteflinq:'tsavoriteflinq'};");
        writer.AppendLine("    const colors={linq:'#3a6ea5',zlinq:'#5b8c5a',flinq:'#c06c3e',persistedlinq:'#8a63a8',persistedflinq:'#b7792f',tsavoritelinq:'#2f7f86',tsavoriteflinq:'#b94b48'};");
        writer.AppendLine("    const methodPalette=['#3a6ea5','#b94b48','#4f8b57','#8a63a8','#b7792f','#2f7f86','#6c6f78','#8f5a42','#607d3b','#7d5b4f','#416f8f','#a85b7a','#738a3b','#8b6f3a','#4f7780','#9a6750'];");
        writer.AppendLine("    const tabDefinitions={");
        writer.AppendLine("      overview:[['winnerHeatmap','Winner heatmap'],['pareto','Pareto'],['treemap','Treemap'],['summaryTable','Summary table']],");
        writer.AppendLine("      compare:[['verticalBenchmarkStack','Benchmark stack'],['groupedBar','処理時間'],['relativeBar','LINQ基準'],['scatter','LINQ vs FLinq scatter'],['stackedShare','処理時間比率']],");
        writer.AppendLine("      memory:[['allocatedShare','メモリアロケート比率'],['allocatedBar','メモリアロケート'],['peakRssBar','実測ピークRSS'],['memorySummaryTable','メモリ表']],");
        writer.AppendLine("      stability:[['errorBar','Error and StdDev'],['cvRanking','Variation ranking']],");
        writer.AppendLine("      cpu:[['cpuTimeline','CPU timeline'],['cpuHeatmap','CPU x Project heatmap']],");
        writer.AppendLine("      flow:[['sankey','Sankey'],['alluvial','Alluvial-like flow'],['sunburstTable','Hierarchy table']]");
        writer.AppendLine("    };");
        writer.AppendLine("    const tabLabels={overview:'Overview',compare:'処理時間比率',memory:'メモリアロケート比率',stability:'Stability',cpu:'CPU',flow:'Flow'};");
        writer.AppendLine("    let activeTab='compare';let chart=null;");
        writer.AppendLine("    tabDefinitions.overview=[['winnerHeatmap','勝者ヒートマップ'],['pareto','パレート'],['treemap','ツリーマップ'],['summaryTable','集計表']];");
        writer.AppendLine("    tabDefinitions.compare=[['verticalBenchmarkStack','縦積み上げ合計'],['flinqSlowRanking','FLinq遅延ランキング'],['groupedBar','処理時間'],['relativeBar','LINQ基準'],['scatter','LINQ vs FLinq散布図'],['stackedShare','処理時間比率']];");
        writer.AppendLine("    tabDefinitions.memory=[['allocatedShare','割り当て比率'],['allocatedBar','割り当て量'],['peakRssBar','実測ピークRSS'],['memorySummaryTable','メモリ表']];");
        writer.AppendLine("    tabDefinitions.stability=[['errorBar','誤差と標準偏差'],['cvRanking','ばらつきランキング']];");
        writer.AppendLine("    tabDefinitions.cpu=[['cpuTimeline','CPUタイムライン'],['cpuHeatmap','CPU x 系列ヒートマップ']];");
        writer.AppendLine("    tabDefinitions.flow=[['sankey','サンキー'],['alluvial','フロー'],['sunburstTable','階層表']];");
        writer.AppendLine("    Object.assign(tabLabels,{overview:'概要',compare:'処理時間比較',memory:'メモリ',stability:'安定性',cpu:'CPU',flow:'フロー'});");
        writer.AppendLine("    function value(row,key){return Number(row[key]||0);}");
        writer.AppendLine("    function memoryValue(row,key){return Number(row[key+'AllocatedBytes']||0);}");
        writer.AppendLine("    function peakRssValue(row,key){return Number(row[key+'PeakRssBytes']||0);}");
        writer.AppendLine("    function estimatedPeakRssValue(row,key){return Number(row[key+'EstimatedPeakRssBytes']||0);}");
        writer.AppendLine("    function fmt(value){return Number(value).toLocaleString(undefined,{maximumFractionDigits:3})+' ns';}");
        writer.AppendLine("    function fmtBytes(value){const n=Number(value||0);if(!n)return '-';const units=['B','KiB','MiB','GiB','TiB'];let v=n,i=0;while(v>=1024&&i<units.length-1){v/=1024;i++;}return v.toLocaleString(undefined,{maximumFractionDigits:i?2:0})+' '+units[i];}");
        writer.AppendLine("    function ratio(value,base){return base>0?value/base:null;}");
        writer.AppendLine("    function categoryOf(method){const p=String(method||'').split('_')[0];return p||'Other';}");
        writer.AppendLine("    function completedRows(){return runRows.filter(row=>row.status==='Completed'&&row.meanValue>0);}");
        writer.AppendLine("    function clearChart(){if(chart){chart.destroy();chart=null;}document.querySelector('#chart').innerHTML='';}");
        writer.AppendLine("    function renderMetrics(){const completed=completedRows();const methods=new Set(comparisonRows.map(row=>row.method));const failed=runRows.filter(row=>row.status!=='Completed').length;const winners=Object.fromEntries(seriesKeys.map(key=>[key,0]));comparisonRows.forEach(row=>{const winner=winnerOf(row);if(winner)winners[winner]++;});document.querySelector('#metrics').innerHTML=[['ベンチマーク数',methods.size],['完了タスク',completed.length],['失敗タスク',failed],['最多勝者',Object.entries(winners).sort((a,b)=>b[1]-a[1])[0]?.join(' ')||'-']].map(item=>`<div class=\"metric\"><strong>${item[1]}</strong><span>${item[0]}</span></div>`).join('');}");
        writer.AppendLine("    function winnerOf(row){let best=null;seriesKeys.forEach(key=>{const v=value(row,key);if(v>0&&(!best||v<best.value)){best={key,value:v};}});return best?.key||null;}");
        writer.AppendLine("    function colorForMethod(method,index){let hash=0;for(let i=0;i<String(method).length;i++){hash=(hash*31+String(method).charCodeAt(i))>>>0;}return methodPalette[(hash+index)%methodPalette.length];}");
        writer.AppendLine("    function makeChart(options){clearChart();chart=new ApexCharts(document.querySelector('#chart'),options);chart.render();}");
        writer.AppendLine("    function baseBarOptions(series,categories,stacked=false,stackType=null,formatter=fmt,labels=false){return {series,categories,colors:series.map(s=>colors[s.name]||'#6c6f78'),chart:{type:'bar',height:Math.max(440,categories.length*28+150),stacked,stackType,toolbar:{show:true}},plotOptions:{bar:{horizontal:true}},xaxis:{categories},dataLabels:{enabled:labels,formatter:(value,opts)=>formatter(opts.w.config.series[opts.seriesIndex].data[opts.dataPointIndex])},legend:{position:'top'},tooltip:{y:{formatter}}};}");
        writer.AppendLine("    function renderGroupedBar(){const cats=comparisonRows.map(row=>row.method);makeChart(baseBarOptions(seriesKeys.map(key=>({name:key,data:comparisonRows.map(row=>value(row,key))})),cats));note('Absolute Mean comparison. Smaller is faster.');}");
        writer.AppendLine("    function renderRelativeBar(){const cats=comparisonRows.map(row=>row.method);makeChart(baseBarOptions(seriesKeys.map(key=>({name:key,data:comparisonRows.map(row=>{const r=ratio(value(row,key),value(row,'linq'));return r===null?0:Number(r.toFixed(3));})})),cats));note('LINQ is 1.0. Values below 1.0 are faster than LINQ.');}");
        writer.AppendLine("    function renderStackedShare(){const cats=comparisonRows.map(row=>row.method);makeChart(baseBarOptions(seriesKeys.map(key=>({name:key,data:comparisonRows.map(row=>value(row,key))})),cats,true,'100%',fmt,true));note('100% share of processing time. Labels and tooltips show actual Mean values.');}");
        writer.AppendLine("    function renderVerticalBenchmarkStack(){const items=comparisonRows.map(row=>({row,total:seriesKeys.reduce((sum,key)=>sum+value(row,key),0)})).filter(item=>item.total>0).sort((a,b)=>b.total-a.total||String(a.row.method).localeCompare(String(b.row.method)));const series=items.map(item=>({name:item.row.method,data:seriesKeys.map(key=>value(item.row,key))}));makeChart({series,chart:{type:'bar',height:Math.max(560,Math.min(1600,items.length*18+360)),stacked:true,toolbar:{show:true}},plotOptions:{bar:{horizontal:false,columnWidth:'62%'}},xaxis:{categories:seriesKeys.map(key=>names[key]||key),title:{text:'Implementation'}},yaxis:{title:{text:'Total Mean ns'},labels:{formatter:value=>Number(value).toLocaleString(undefined,{maximumFractionDigits:0})}},dataLabels:{enabled:false},legend:{position:'right',fontSize:'11px',showForSingleSeries:true},tooltip:{y:{formatter:fmt}},fill:{opacity:1}});note('Vertical stacked Mean by implementation. Each stacked segment is one benchmark method, ordered by total Mean across all implementations.');}");
        writer.AppendLine("    function renderVerticalBenchmarkStack(){const items=comparisonRows.map(row=>({row,total:seriesKeys.reduce((sum,key)=>sum+value(row,key),0)})).filter(item=>item.total>0).sort((a,b)=>b.total-a.total||String(a.row.method).localeCompare(String(b.row.method)));const totals=Object.fromEntries(seriesKeys.map(key=>[key,items.reduce((sum,item)=>sum+value(item.row,key),0)]));const orderedKeys=[...seriesKeys].sort((a,b)=>totals[a]-totals[b]||String(names[a]).localeCompare(String(names[b])));const series=items.map((item,index)=>({name:item.row.method,data:orderedKeys.map(key=>value(item.row,key)),color:colorForMethod(item.row.method,index)}));makeChart({series,colors:series.map(s=>s.color),chart:{type:'bar',height:Math.max(560,Math.min(1800,items.length*18+360)),stacked:true,toolbar:{show:true}},plotOptions:{bar:{horizontal:false,columnWidth:'62%'}},xaxis:{categories:orderedKeys.map(key=>names[key]||key),title:{text:'系列（合計Meanが速い順）'}},yaxis:{title:{text:'合計Mean ns'},labels:{formatter:value=>Number(value).toLocaleString(undefined,{maximumFractionDigits:0})}},dataLabels:{enabled:false},legend:{position:'right',fontSize:'11px',showForSingleSeries:true},tooltip:{custom:({seriesIndex,dataPointIndex,w})=>{const method=w.config.series[seriesIndex].name;const key=orderedKeys[dataPointIndex];const row=items[seriesIndex].row;const v=value(row,key);const total=totals[key]||0;const share=total>0?v/total*100:0;const z=value(row,'zlinq');const f=value(row,'flinq');const ratioText=z>0&&f>0?`<br>FLinq/ZLinq: ${(f/z).toFixed(3)}x (${((f/z-1)*100).toFixed(1)}%)`:'';return `<div style=\"padding:6px\"><b>${method}</b><br>${names[key]||key}: ${fmt(v)}<br>構成比: ${share.toFixed(1)}%${ratioText}</div>`;}},fill:{opacity:1}});note('系列ごとのMean合計を縦積み上げで表示します。x軸は合計Meanが小さい順、セグメント色はベンチマーク名に固定です。');}");
        writer.AppendLine("    function renderFLinqSlowRanking(){if(!flinqSlowRows.length){tableMessage('System FLinq が System ZLinq より遅いベンチマークはありません。');note('FLinq遅延ランキングの対象はありません。');return;}const rows=[...flinqSlowRows].sort((a,b)=>b.delta-a.delta||String(a.method).localeCompare(String(b.method)));makeChart({series:[{name:'FLinq - ZLinq',data:rows.map(row=>row.delta)}],chart:{type:'bar',height:Math.max(440,rows.length*30+150),toolbar:{show:true}},plotOptions:{bar:{horizontal:true}},xaxis:{title:{text:'遅延差分 ns'},labels:{formatter:value=>Number(value).toLocaleString(undefined,{maximumFractionDigits:0})}},yaxis:{labels:{maxWidth:360}},dataLabels:{enabled:true,formatter:(value,opts)=>`${fmt(value)} / ${rows[opts.dataPointIndex].percentSlower.toFixed(1)}%遅い`},tooltip:{custom:({dataPointIndex})=>{const row=rows[dataPointIndex];return `<div style=\"padding:6px\"><b>${row.method}</b><br>ZLinq: ${fmt(row.zlinq)}<br>FLinq: ${fmt(row.flinq)}<br>差分: ${fmt(row.delta)}<br>${row.percentSlower.toFixed(1)}%遅い</div>`;}},colors:['#b94b48']});note('System FLinq が System ZLinq より遅いベンチマークを、差分時間が大きい順に表示します。');}");
        writer.AppendLine("    function renderAllocatedShare(){const cats=comparisonRows.map(row=>row.method);makeChart(baseBarOptions(seriesKeys.map(key=>({name:key,data:comparisonRows.map(row=>memoryValue(row,key))})),cats,true,'100%',fmtBytes,true));note('100% share of allocated bytes. Labels and tooltips show actual allocated bytes.');}");
        writer.AppendLine("    function renderAllocatedBar(){const cats=comparisonRows.map(row=>row.method);makeChart(baseBarOptions(seriesKeys.map(key=>({name:key,data:comparisonRows.map(row=>memoryValue(row,key))})),cats,false,null,fmtBytes));note('Absolute allocated bytes from BenchmarkDotNet MemoryDiagnoser.');}");
        writer.AppendLine("    function renderPeakRssBar(){const cats=comparisonRows.map(row=>row.method);makeChart(baseBarOptions(seriesKeys.map(key=>({name:key,data:comparisonRows.map(row=>peakRssValue(row,key))})),cats,false,null,fmtBytes));note('Measured peak RSS of the worker process during each task.');}");
        writer.AppendLine("    function renderScatter(){makeChart({series:[{name:'FLinq vs LINQ',data:comparisonRows.map(row=>({x:value(row,'linq'),y:value(row,'flinq'),method:row.method}))}],chart:{type:'scatter',height:520,zoom:{enabled:true},toolbar:{show:true}},xaxis:{title:{text:'linq Mean ns'}},yaxis:{title:{text:'flinq Mean ns'}},tooltip:{custom:({seriesIndex,dataPointIndex,w})=>{const p=w.config.series[seriesIndex].data[dataPointIndex];return `<div style=\"padding:6px\"><b>${p.method}</b><br>linq: ${fmt(p.x)}<br>flinq: ${fmt(p.y)}</div>`;}}});note('Points below the diagonal tendency are FLinq-favorable, but the guide line is not drawn yet.');}");
        writer.AppendLine("    function renderWinnerHeatmap(){makeChart({series:seriesKeys.map(key=>({name:key,data:comparisonRows.map(row=>{const min=Math.min(...seriesKeys.map(k=>value(row,k)).filter(v=>v>0));const v=value(row,key);return {x:row.method,y:v>0&&min>0?Number((v/min).toFixed(3)):0};})})),chart:{type:'heatmap',height:Math.max(440,comparisonRows.length*24+140),toolbar:{show:true}},plotOptions:{heatmap:{shadeIntensity:.55,colorScale:{ranges:[{from:0,to:0.001,color:'#ececec',name:'n/a'},{from:.001,to:1.02,color:'#4f8b57',name:'winner'},{from:1.02,to:1.2,color:'#d6b656',name:'close'},{from:1.2,to:100000,color:'#b94b48',name:'slower'}]}}},dataLabels:{enabled:false},xaxis:{labels:{rotate:-45}},tooltip:{y:{formatter:v=>v?`${v}x winner`:'n/a'}}});note('Each cell is relative to the fastest implementation for that method.');}");
        writer.AppendLine("    function renderPareto(){const items=completedRows().sort((a,b)=>b.meanValue-a.meanValue);const total=items.reduce((s,r)=>s+r.meanValue,0);let running=0;makeChart({series:[{name:'Mean ns',type:'column',data:items.map(r=>r.meanValue)},{name:'Cumulative %',type:'line',data:items.map(r=>Number(((running+=r.meanValue)/Math.max(total,1)*100).toFixed(2)))}],chart:{height:520,type:'line',toolbar:{show:true}},stroke:{width:[0,3]},xaxis:{categories:items.map(r=>r.method+' / '+r.project),labels:{rotate:-45}},yaxis:[{title:{text:'Mean ns'}},{opposite:true,max:100,title:{text:'Cumulative %'}}],dataLabels:{enabled:false}});note('Largest benchmark costs first; useful for deciding where optimization work matters.');}");
        writer.AppendLine("    function renderTreemap(){const grouped={};completedRows().forEach(row=>{const key=categoryOf(row.method)+' / '+row.project;grouped[key]=(grouped[key]||0)+row.meanValue;});makeChart({series:[{data:Object.entries(grouped).map(([x,y])=>({x,y:Number(y.toFixed(3))}))}],chart:{type:'treemap',height:520,toolbar:{show:true}},tooltip:{y:{formatter:fmt}}});note('Area is total Mean by derived method category and project.');}");
        writer.AppendLine("    function renderErrorBar(){const items=completedRows().sort((a,b)=>b.stdDevValue-a.stdDevValue).slice(0,80);makeChart(baseBarOptions([{name:'Error',data:items.map(r=>r.errorValue)},{name:'StdDev',data:items.map(r=>r.stdDevValue)}],items.map(r=>r.method+' / '+r.project)));note('Largest uncertainty values. High values can explain noisy comparisons.');}");
        writer.AppendLine("    function renderCvRanking(){const items=completedRows().map(r=>({...r,cv:r.meanValue>0?r.stdDevValue/r.meanValue:0})).sort((a,b)=>b.cv-a.cv).slice(0,80);makeChart(baseBarOptions([{name:'StdDev / Mean',data:items.map(r=>Number(r.cv.toFixed(5)))}],items.map(r=>r.method+' / '+r.project)));note('Variation coefficient ranking. Higher values are less stable.');}");
        writer.AppendLine("    function renderCpuHeatmap(){const cpus=[...new Set(runRows.map(r=>r.cpuDisplay||r.logicalCpu).filter(Boolean))].sort();makeChart({series:[...new Set(runRows.map(r=>r.project))].map(project=>({name:project,data:cpus.map(cpu=>{const values=runRows.filter(r=>r.project===project&&(r.cpuDisplay===cpu||r.logicalCpu===cpu)).map(r=>r.meanValue).filter(v=>v>0);return {x:cpu,y:values.length?Number((values.reduce((a,b)=>a+b,0)/values.length).toFixed(3)):0};})})),chart:{type:'heatmap',height:Math.max(440,cpus.length*34+160),toolbar:{show:true}},dataLabels:{enabled:false},tooltip:{y:{formatter:fmt}}});note('Average Mean by CPU lane and project. Empty/failed rows are zero.');}");
        writer.AppendLine("    function renderCpuTimeline(){");
        writer.AppendLine("      const items=runRows.filter(r=>r.startedAt&&r.finishedAt).map(r=>({...r,start:Date.parse(r.startedAt),end:Date.parse(r.finishedAt),cpu:r.cpuDisplay||r.logicalCpu||'-'})).filter(r=>Number.isFinite(r.start)&&Number.isFinite(r.end)&&r.end>=r.start);");
        writer.AppendLine("      if(!items.length){tableMessage('No timed CPU rows were available.');return;}");
        writer.AppendLine("      clearChart();");
        writer.AppendLine("      const chartEl=document.querySelector('#chart');");
        writer.AppendLine("      const start=Math.min(...items.map(r=>r.start));");
        writer.AppendLine("      const end=Math.max(...items.map(r=>r.end));");
        writer.AppendLine("      const span=Math.max(1,end-start);");
        writer.AppendLine("      const cpus=[...new Set(items.map(r=>r.cpu))].sort((a,b)=>Number.parseInt(a)-Number.parseInt(b)||String(a).localeCompare(String(b)));");
        writer.AppendLine("      const projectPalette=['#3a6ea5','#c06c3e','#5b8c5a','#8a63a8','#b7792f','#2f7f86','#6c6f78','#b94b48','#607d3b','#7d5b4f'];");
        writer.AppendLine("      const projects=[...new Set(items.map(r=>r.project))].sort((a,b)=>String(a).localeCompare(String(b)));");
        writer.AppendLine("      const projectColors=Object.fromEntries(projects.map((project,index)=>[project,projectPalette[index%projectPalette.length]]));");
        writer.AppendLine("      const laneHeight=58,barHeight=30,top=70,left=170,right=34,width=Math.max(1100,chartEl.clientWidth-30),height=top+cpus.length*laneHeight+52,plotWidth=width-left-right;");
        writer.AppendLine("      const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');");
        writer.AppendLine("      svg.setAttribute('viewBox',`0 0 ${width} ${height}`);");
        writer.AppendLine("      svg.style.width='100%';svg.style.minHeight=height+'px';");
        writer.AppendLine("      chartEl.appendChild(svg);");
        writer.AppendLine("      projects.forEach((project,index)=>{const x=left+index*150;const swatch=document.createElementNS(svg.namespaceURI,'rect');swatch.setAttribute('x',x);swatch.setAttribute('y',18);swatch.setAttribute('width',12);swatch.setAttribute('height',12);swatch.setAttribute('rx',2);swatch.setAttribute('fill',projectColors[project]);svg.appendChild(swatch);const text=document.createElementNS(svg.namespaceURI,'text');text.setAttribute('x',x+18);text.setAttribute('y',29);text.setAttribute('class','axis-label');text.textContent=project;svg.appendChild(text);});");
        writer.AppendLine("      for(let i=0;i<=10;i++){const x=left+plotWidth*i/10;const line=document.createElementNS(svg.namespaceURI,'line');line.setAttribute('x1',x);line.setAttribute('x2',x);line.setAttribute('y1',top-18);line.setAttribute('y2',height-34);line.setAttribute('stroke','#e4dfd2');svg.appendChild(line);const text=document.createElementNS(svg.namespaceURI,'text');text.setAttribute('x',x);text.setAttribute('y',height-14);text.setAttribute('text-anchor',i===0?'start':i===10?'end':'middle');text.setAttribute('class','axis-label');text.textContent=Math.round(span*i/10000)/100+'s';svg.appendChild(text);}");
        writer.AppendLine("      cpus.forEach((cpu,index)=>{const y=top+index*laneHeight;const label=document.createElementNS(svg.namespaceURI,'text');label.setAttribute('x',12);label.setAttribute('y',y+27);label.setAttribute('class','node-label');label.textContent=cpu;svg.appendChild(label);const lane=document.createElementNS(svg.namespaceURI,'rect');lane.setAttribute('x',left);lane.setAttribute('y',y+5);lane.setAttribute('width',plotWidth);lane.setAttribute('height',laneHeight-10);lane.setAttribute('fill',index%2?'#fbfaf4':'#fff');lane.setAttribute('stroke','#e4dfd2');svg.appendChild(lane);});");
        writer.AppendLine("      items.sort((a,b)=>a.start-b.start).forEach(item=>{const laneIndex=cpus.indexOf(item.cpu);const y=top+laneIndex*laneHeight+14;const x=left+(item.start-start)/span*plotWidth;const w=Math.max(2,(item.end-item.start)/span*plotWidth);const label=item.method;const range=`${Math.round((item.start-start)/1000)}-${Math.round((item.end-start)/1000)}s`;const rect=document.createElementNS(svg.namespaceURI,'rect');rect.setAttribute('x',x);rect.setAttribute('y',y);rect.setAttribute('width',w);rect.setAttribute('height',barHeight);rect.setAttribute('rx',3);rect.setAttribute('fill',projectColors[item.project]||'#6c6f78');rect.setAttribute('opacity',item.status==='Completed'?'.9':'.48');rect.appendChild(document.createElementNS(svg.namespaceURI,'title')).textContent=`${item.cpu} | ${label} | ${item.project} | ${range} | ${item.status}`;svg.appendChild(rect);const text=document.createElementNS(svg.namespaceURI,'text');const inside=w>=170;const textX=inside?x+7:Math.min(x+w+6,width-220);text.setAttribute('x',textX);text.setAttribute('y',y+19);text.setAttribute('fill',inside?'#fff':'#202020');text.setAttribute('font-size','12');text.setAttribute('font-weight','600');text.textContent=(label+'  '+range).slice(0,inside?46:64);text.appendChild(document.createElementNS(svg.namespaceURI,'title')).textContent=`${item.cpu} | ${label} | ${item.project} | ${range} | ${item.status}`;svg.appendChild(text);});");
        writer.AppendLine("      note('Each lane is one selected CPU/logical processor. Each bar shows the task start and finish time handled on that CPU.');");
        writer.AppendLine("    }");
        writer.AppendLine("    function projectKey(project){const p=String(project).toLowerCase();if(p.includes('zlinq'))return 'zlinq';if(p.includes('flinq'))return 'flinq';if(p.includes('linq'))return 'linq';return project;}");
        writer.AppendLine("    function renderSankey(){clearChart();const chartEl=document.querySelector('#chart');const winners={};comparisonRows.forEach(row=>{const cat=categoryOf(row.method);const winner=winnerOf(row)||'n/a';const key=cat+'|'+winner;winners[key]=(winners[key]||0)+1;});const cats=[...new Set(Object.keys(winners).map(k=>k.split('|')[0]))].sort();const targets=[...new Set(Object.keys(winners).map(k=>k.split('|')[1]))].sort();const width=Math.max(900,chartEl.clientWidth-30),height=Math.max(520,cats.length*28+80);const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('class','sankey');svg.setAttribute('viewBox',`0 0 ${width} ${height}`);chartEl.appendChild(svg);const leftX=120,rightX=width-180;cats.forEach((cat,i)=>drawNode(svg,leftX,40+i*28,150,20,cat));targets.forEach((target,i)=>drawNode(svg,rightX,80+i*80,130,26,target));Object.entries(winners).forEach(([key,count])=>{const [cat,target]=key;const y1=50+cats.indexOf(cat)*28;const y2=93+targets.indexOf(target)*80;const path=document.createElementNS(svg.namespaceURI,'path');path.setAttribute('d',`M ${leftX+150} ${y1} C ${width*.45} ${y1}, ${width*.55} ${y2}, ${rightX} ${y2}`);path.setAttribute('class','flow');path.setAttribute('stroke',colors[target]||'#6c6f78');path.setAttribute('stroke-width',String(Math.max(2,count*2)));path.appendChild(document.createElementNS(svg.namespaceURI,'title')).textContent=`${cat} -> ${target}: ${count}`;svg.insertBefore(path,svg.firstChild);});note('Derived flow: method category to fastest implementation. Width is method count.');}");
        writer.AppendLine("    function renderAlluvial(){renderSankey();note('Alluvial-like view using the same category-to-winner flow. Future data can add stability bands as a third axis.');}");
        writer.AppendLine("    function drawNode(svg,x,y,w,h,label){const r=document.createElementNS(svg.namespaceURI,'rect');r.setAttribute('x',x);r.setAttribute('y',y);r.setAttribute('width',w);r.setAttribute('height',h);r.setAttribute('rx','3');r.setAttribute('class','node');svg.appendChild(r);const t=document.createElementNS(svg.namespaceURI,'text');t.setAttribute('x',x+6);t.setAttribute('y',y+h*.68);t.setAttribute('class','node-label');t.textContent=label;svg.appendChild(t);}");
        writer.AppendLine("    function renderSummaryTable(){table(['Method','linq','zlinq','flinq','persistedlinq','persistedflinq','tsavoritelinq','tsavoriteflinq','Winner'],comparisonRows.map(row=>[row.method,fmt(value(row,'linq')),fmt(value(row,'zlinq')),fmt(value(row,'flinq')),fmt(value(row,'persistedlinq')),fmt(value(row,'persistedflinq')),fmt(value(row,'tsavoritelinq')),fmt(value(row,'tsavoriteflinq')),winnerOf(row)||'-']));note('Exact values behind the comparison charts.');}");
        writer.AppendLine("    function renderMemorySummaryTable(){table(['Method','Type','linq','zlinq','flinq','persistedlinq','persistedflinq','tsavoritelinq','tsavoriteflinq'],comparisonRows.flatMap(row=>[['Allocated',key=>fmtBytes(memoryValue(row,key))],['Peak RSS',key=>fmtBytes(peakRssValue(row,key))],['Estimated peak RSS',key=>fmtBytes(estimatedPeakRssValue(row,key))]].map(([label,projectValue])=>[row.method,label,projectValue('linq'),projectValue('zlinq'),projectValue('flinq'),projectValue('persistedlinq'),projectValue('persistedflinq'),projectValue('tsavoritelinq'),projectValue('tsavoriteflinq')])));note('Memory values used by the allocation and RSS charts.');}");
        writer.AppendLine("    function renderSunburstTable(){const grouped={};completedRows().forEach(row=>{const cat=categoryOf(row.method);grouped[cat]??={};grouped[cat][row.project]=(grouped[cat][row.project]||0)+row.meanValue;});table(['Category','Project','Total Mean'],Object.entries(grouped).flatMap(([cat,projects])=>Object.entries(projects).map(([project,total])=>[cat,project,fmt(total)])));note('Tabular hierarchy placeholder for a future sunburst/icicle view.');}");
        writer.AppendLine("    function table(headers,rows){clearChart();document.querySelector('#chart').innerHTML='<div class=\"fallback\"><table><thead><tr>'+headers.map(h=>`<th>${h}</th>`).join('')+'</tr></thead><tbody>'+rows.map(row=>'<tr>'+row.map(cell=>`<td>${cell}</td>`).join('')+'</tr>').join('')+'</tbody></table></div>';}");
        writer.AppendLine("    function tableMessage(text){clearChart();document.querySelector('#chart').innerHTML=`<p style=\"margin:12px\">${text}</p>`;}");
        writer.AppendLine("    function note(text){document.querySelector('#note').textContent=text;}");
        writer.AppendLine("    const renderers={verticalBenchmarkStack:renderVerticalBenchmarkStack,flinqSlowRanking:renderFLinqSlowRanking,groupedBar:renderGroupedBar,relativeBar:renderRelativeBar,scatter:renderScatter,stackedShare:renderStackedShare,allocatedShare:renderAllocatedShare,allocatedBar:renderAllocatedBar,peakRssBar:renderPeakRssBar,memorySummaryTable:renderMemorySummaryTable,winnerHeatmap:renderWinnerHeatmap,pareto:renderPareto,treemap:renderTreemap,summaryTable:renderSummaryTable,errorBar:renderErrorBar,cvRanking:renderCvRanking,cpuTimeline:renderCpuTimeline,cpuHeatmap:renderCpuHeatmap,sankey:renderSankey,alluvial:renderAlluvial,sunburstTable:renderSunburstTable};");
        writer.AppendLine("    function setTab(tab){activeTab=tab;document.querySelectorAll('.tab').forEach(button=>button.classList.toggle('active',button.dataset.tab===tab));const select=document.querySelector('#chartMode');select.innerHTML=tabDefinitions[tab].map(([id,label])=>`<option value=\"${id}\">${label}</option>`).join('');renderSelected();}");
        writer.AppendLine("    function renderSelected(){renderMetrics();const mode=document.querySelector('#chartMode').value;(renderers[mode]||renderSummaryTable)();}");
        writer.AppendLine("    document.querySelector('#tabs').innerHTML=Object.keys(tabDefinitions).map(tab=>`<button class=\"tab\" data-tab=\"${tab}\">${tabLabels[tab]||tab}</button>`).join('');");
        writer.AppendLine("    document.querySelector('#tabs').addEventListener('click',event=>{const tab=event.target.dataset.tab;if(tab)setTab(tab);});");
        writer.AppendLine("    document.querySelector('#chartMode').addEventListener('change',renderSelected);");
        writer.AppendLine("    setTab(activeTab);");
        writer.AppendLine("  </script>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        return writer.ToString();
    }

    public static string BuildDataJson(IReadOnlyList<ReportRow> rows,DateTimeOffset generatedAt){
        var comparisonRows = CreateComparisonRows(rows);
        var writer = new StringBuilder();
        writer.Append('{');
        writer.Append("\"generatedAt\":\"")
            .Append(EscapeJavaScriptString(generatedAt.ToString("O",CultureInfo.InvariantCulture)))
            .Append('"');
        writer.Append(",\"seriesKeys\":[\"linq\",\"zlinq\",\"flinq\",\"persistedlinq\",\"persistedflinq\",\"tsavoritelinq\",\"tsavoriteflinq\"]");
        writer.Append(",\"rawRows\":").Append(BuildReportRowsJson(rows));
        writer.Append(",\"comparisonRows\":").Append(BuildComparisonRowsJson(comparisonRows));
        writer.Append(",\"flinqSlowRows\":").Append(BuildFLinqSlowRowsJson(comparisonRows));
        writer.Append('}');
        return writer.ToString();
    }

    public static string BuildChartHtml(IReadOnlyList<ReportRow> rows,DateTimeOffset generatedAt){
        var comparisonRows = CreateComparisonRows(rows);
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"en\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>Benchmark Comparison Chart</title>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    body{font-family:Segoe UI,Arial,sans-serif;margin:20px;color:#202020;background:#fffefa;}");
        writer.AppendLine("    h1{font-size:22px;margin:0 0 8px;}");
        writer.AppendLine("    p{margin:0 0 16px;color:#555;}");
        writer.AppendLine("    .toolbar{display:flex;align-items:center;gap:8px;margin:0 0 14px;font-size:13px;}");
        writer.AppendLine("    select{font:inherit;padding:3px 7px;border:1px solid #c8c2b4;background:#fff;color:#202020;}");
        writer.AppendLine("    .legend{display:flex;gap:18px;margin:0 0 14px;font-size:13px;}");
        writer.AppendLine("    .legend span{display:inline-flex;align-items:center;gap:6px;}");
        writer.AppendLine("    .swatch{width:12px;height:12px;border-radius:2px;display:inline-block;}");
        writer.AppendLine("    .linq{background:#3a6ea5;}");
        writer.AppendLine("    .zlinq{background:#5b8c5a;}");
        writer.AppendLine("    .flinq{background:#c06c3e;}");
        writer.AppendLine("    .persistedlinq{background:#8a63a8;}");
        writer.AppendLine("    .persistedflinq{background:#b7792f;}");
        writer.AppendLine("    .tsavoritelinq{background:#2f7f86;}");
        writer.AppendLine("    .tsavoriteflinq{background:#b94b48;}");
        writer.AppendLine("    .chart{display:grid;grid-template-columns:minmax(180px,28%) 1fr;gap:6px 12px;font-size:13px;}");
        writer.AppendLine("    .method{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;padding:5px 0;}");
        writer.AppendLine("    .bar{height:24px;border:1px solid #d5d1c4;background:#f8f6ed;display:flex;overflow:hidden;}");
        writer.AppendLine("    .segment{height:100%;display:flex;align-items:center;justify-content:center;color:#fff;font-size:11px;min-width:0;}");
        writer.AppendLine("    .segment span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;padding:0 4px;}");
        writer.AppendLine("    .axis-label{font-size:11px;color:#555;}");
        writer.AppendLine("    .axis{grid-column:2;display:grid;grid-template-columns:repeat(5,1fr);margin-top:4px;color:#555;font-size:11px;}");
        writer.AppendLine("    .axis span:nth-child(n+2){text-align:center;}");
        writer.AppendLine("    .axis span:last-child{text-align:right;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>Benchmark Comparison Chart</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(generatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        writer.AppendLine("  <div class=\"toolbar\"><label for=\"seriesOrder\">Segment order</label><select id=\"seriesOrder\"><option value=\"default\">linq, zlinq, flinq, persistedlinq, persistedflinq, tsavoritelinq, tsavoriteflinq</option><option value=\"asc\">Smallest ratio first</option><option value=\"desc\">Largest ratio first</option><option value=\"linq,zlinq,flinq,persistedlinq,persistedflinq,tsavoritelinq,tsavoriteflinq\">linq, zlinq, flinq, persistedlinq, persistedflinq, tsavoritelinq, tsavoriteflinq</option><option value=\"linq,flinq,zlinq,persistedlinq,persistedflinq,tsavoritelinq,tsavoriteflinq\">linq, flinq, zlinq, persistedlinq, persistedflinq, tsavoritelinq, tsavoriteflinq</option><option value=\"persistedlinq,persistedflinq,tsavoritelinq,tsavoriteflinq,linq,zlinq,flinq\">persisted and tsavorite first</option></select></div>");
        writer.AppendLine("  <div class=\"legend\"><span><i class=\"swatch linq\"></i>linq</span><span><i class=\"swatch zlinq\"></i>zlinq</span><span><i class=\"swatch flinq\"></i>flinq</span><span><i class=\"swatch persistedlinq\"></i>persistedlinq</span><span><i class=\"swatch persistedflinq\"></i>persistedflinq</span><span><i class=\"swatch tsavoritelinq\"></i>tsavoritelinq</span><span><i class=\"swatch tsavoriteflinq\"></i>tsavoriteflinq</span></div>");
        writer.AppendLine("  <div class=\"chart\">");
        writer.AppendLine("    <div class=\"axis-label\"></div><div class=\"axis-label\">ratio</div>");
        foreach(var row in comparisonRows){
            var linqRatio = GetRatio(row.Linq,row);
            var zlinqRatio = GetRatio(row.ZLinq,row);
            var flinqRatio = GetRatio(row.FLinq,row);
            var persistedLinqRatio = GetRatio(row.PersistedLinq,row);
            var persistedFLinqRatio = GetRatio(row.PersistedFLinq,row);
            var tsavoriteLinqRatio = GetRatio(row.TsavoriteLinq,row);
            var tsavoriteFLinqRatio = GetRatio(row.TsavoriteFLinq,row);
            writer.Append("    <div class=\"method\" title=\"").Append(EscapeHtml(row.Method)).Append("\">").Append(EscapeHtml(row.Method)).AppendLine("</div>");
            writer.AppendLine("    <div class=\"bar\">");
            AppendSegment(writer,"linq","linq",row.Linq,linqRatio,0);
            AppendSegment(writer,"zlinq","zlinq",row.ZLinq,zlinqRatio,1);
            AppendSegment(writer,"flinq","flinq",row.FLinq,flinqRatio,2);
            AppendSegment(writer,"persistedlinq","persistedlinq",row.PersistedLinq,persistedLinqRatio,3);
            AppendSegment(writer,"persistedflinq","persistedflinq",row.PersistedFLinq,persistedFLinqRatio,4);
            AppendSegment(writer,"tsavoritelinq","tsavoritelinq",row.TsavoriteLinq,tsavoriteLinqRatio,5);
            AppendSegment(writer,"tsavoriteflinq","tsavoriteflinq",row.TsavoriteFLinq,tsavoriteFLinqRatio,6);
            writer.AppendLine("    </div>");
        }
        writer.AppendLine("    <div></div><div class=\"axis\"><span>0</span><span>0.25</span><span>0.5</span><span>0.75</span><span>1</span></div>");
        writer.AppendLine("  </div>");
        writer.AppendLine("  <script>");
        writer.AppendLine("    const select=document.getElementById('seriesOrder');");
        writer.AppendLine("    const fixedOrders={default:['linq','zlinq','flinq','persistedlinq','persistedflinq','tsavoritelinq','tsavoriteflinq']};");
        writer.AppendLine("    function applyOrder(){");
        writer.AppendLine("      const value=select.value;");
        writer.AppendLine("      document.querySelectorAll('.bar').forEach(bar=>{");
        writer.AppendLine("        const segments=[...bar.querySelectorAll('.segment')];");
        writer.AppendLine("        let ordered;");
        writer.AppendLine("        if(value==='asc'||value==='desc'){");
        writer.AppendLine("          ordered=segments.slice().sort((a,b)=>{");
        writer.AppendLine("            const diff=Number(a.dataset.value)-Number(b.dataset.value);");
        writer.AppendLine("            return value==='asc' ? diff || Number(a.dataset.original)-Number(b.dataset.original) : -diff || Number(a.dataset.original)-Number(b.dataset.original);");
        writer.AppendLine("          });");
        writer.AppendLine("        }else{");
        writer.AppendLine("          const order=(fixedOrders[value]||value.split(',')).reduce((map,key,index)=>(map[key]=index,map),{});");
        writer.AppendLine("          ordered=segments.slice().sort((a,b)=>(order[a.dataset.key]??99)-(order[b.dataset.key]??99));");
        writer.AppendLine("        }");
        writer.AppendLine("        ordered.forEach((segment,index)=>segment.style.order=String(index));");
        writer.AppendLine("      });");
        writer.AppendLine("    }");
        writer.AppendLine("    select.addEventListener('change',applyOrder);");
        writer.AppendLine("    applyOrder();");
        writer.AppendLine("  </script>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        return writer.ToString();
    }

    public static string BuildApexChartHtml(IReadOnlyList<ReportRow> rows,DateTimeOffset generatedAt){
        var comparisonRows = CreateComparisonRows(rows);
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"en\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>Benchmark Comparison Chart.js</title>");
        writer.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/apexcharts\"></script>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    body{font-family:Segoe UI,Arial,sans-serif;margin:20px;color:#202020;background:#fffefa;}");
        writer.AppendLine("    h1{font-size:22px;margin:0 0 8px;}");
        writer.AppendLine("    p{margin:0 0 16px;color:#555;}");
        writer.AppendLine("    .toolbar{display:flex;align-items:center;gap:8px;margin:0 0 14px;font-size:13px;}");
        writer.AppendLine("    select{font:inherit;padding:3px 7px;border:1px solid #c8c2b4;background:#fff;color:#202020;}");
        writer.AppendLine("    #chart{min-height:420px;}");
        writer.AppendLine("    h2{font-size:16px;margin:22px 0 8px;}");
        writer.AppendLine("    table{border-collapse:collapse;width:100%;font-size:12px;}");
        writer.AppendLine("    th,td{border:1px solid #d5d1c4;padding:5px 7px;text-align:left;vertical-align:top;}");
        writer.AppendLine("    th{background:#f0ede4;font-weight:600;white-space:nowrap;}");
        writer.AppendLine("    td.num{text-align:right;white-space:nowrap;}");
        writer.AppendLine("    td.code{font-family:Cascadia Mono,Consolas,monospace;font-size:11px;word-break:break-all;}");
        writer.AppendLine("    tbody tr:nth-child(even) td{background:#faf8f0;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>Benchmark Comparison Chart.js</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(generatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        writer.AppendLine("  <div class=\"toolbar\"><label for=\"seriesOrder\">Series order</label><select id=\"seriesOrder\"><option value=\"default\">linq, zlinq, flinq, persistedlinq, persistedflinq, tsavoritelinq, tsavoriteflinq</option><option value=\"totalAsc\">Total: fastest first</option><option value=\"totalDesc\">Total: slowest first</option><option value=\"linq,zlinq,flinq,persistedlinq,persistedflinq,tsavoritelinq,tsavoriteflinq\">linq, zlinq, flinq, persistedlinq, persistedflinq, tsavoritelinq, tsavoriteflinq</option><option value=\"linq,flinq,zlinq,persistedlinq,persistedflinq,tsavoritelinq,tsavoriteflinq\">linq, flinq, zlinq, persistedlinq, persistedflinq, tsavoritelinq, tsavoriteflinq</option><option value=\"persistedlinq,persistedflinq,tsavoritelinq,tsavoriteflinq,linq,zlinq,flinq\">persisted and tsavorite first</option></select></div>");
        writer.AppendLine("  <div id=\"chart\"></div>");
        writer.AppendLine("  <script>");
        writer.AppendLine("    const rows=[");
        for(var i=0;i<comparisonRows.Count;i++){
            var row = comparisonRows[i];
            writer.Append("      {method:\"").Append(EscapeJavaScriptString(row.Method)).Append("\",linq:")
                .Append(GetPositiveValue(row.Linq).ToString("0.###",CultureInfo.InvariantCulture))
                .Append(",zlinq:")
                .Append(GetPositiveValue(row.ZLinq).ToString("0.###",CultureInfo.InvariantCulture))
                .Append(",flinq:")
                .Append(GetPositiveValue(row.FLinq).ToString("0.###",CultureInfo.InvariantCulture))
                .Append(",persistedlinq:")
                .Append(GetPositiveValue(row.PersistedLinq).ToString("0.###",CultureInfo.InvariantCulture))
                .Append(",persistedflinq:")
                .Append(GetPositiveValue(row.PersistedFLinq).ToString("0.###",CultureInfo.InvariantCulture))
                .Append(",tsavoritelinq:")
                .Append(GetPositiveValue(row.TsavoriteLinq).ToString("0.###",CultureInfo.InvariantCulture))
                .Append(",tsavoriteflinq:")
                .Append(GetPositiveValue(row.TsavoriteFLinq).ToString("0.###",CultureInfo.InvariantCulture))
                .Append('}');
            writer.AppendLine(i+1<comparisonRows.Count ? "," : string.Empty);
        }
        writer.AppendLine("    ];");
        writer.AppendLine("    const seriesKeys=['linq','zlinq','flinq','persistedlinq','persistedflinq','tsavoritelinq','tsavoriteflinq'];");
        writer.AppendLine("    const names={linq:'linq',zlinq:'zlinq',flinq:'flinq',persistedlinq:'persistedlinq',persistedflinq:'persistedflinq',tsavoritelinq:'tsavoritelinq',tsavoriteflinq:'tsavoriteflinq'};");
        writer.AppendLine("    const colors={linq:'#3a6ea5',zlinq:'#5b8c5a',flinq:'#c06c3e',persistedlinq:'#8a63a8',persistedflinq:'#b7792f',tsavoritelinq:'#2f7f86',tsavoriteflinq:'#b94b48'};");
        writer.AppendLine("    function orderedKeys(value){");
        writer.AppendLine("      if(value==='totalAsc'||value==='totalDesc'){");
        writer.AppendLine("        const totals=seriesKeys.map(key=>({key,total:rows.reduce((sum,row)=>sum+row[key],0)}));");
        writer.AppendLine("        totals.sort((a,b)=>value==='totalAsc'?a.total-b.total:b.total-a.total);");
        writer.AppendLine("        return totals.map(item=>item.key);");
        writer.AppendLine("      }");
        writer.AppendLine("      return value==='default'?seriesKeys:value.split(',');");
        writer.AppendLine("    }");
        writer.AppendLine("    function buildOptions(order){");
        writer.AppendLine("      return {");
        writer.AppendLine("        series:order.map(key=>({name:names[key],data:rows.map(row=>row[key])})),");
        writer.AppendLine("        colors:order.map(key=>colors[key]),");
        writer.AppendLine("        chart:{type:'bar',height:Math.max(420,rows.length*30+140),stacked:true,stackType:'100%',toolbar:{show:true}},");
        writer.AppendLine("        plotOptions:{bar:{horizontal:true}},");
        writer.AppendLine("        xaxis:{categories:rows.map(row=>row.method),labels:{formatter:value=>Number(value).toFixed(0)+'%'}},");
        writer.AppendLine("        yaxis:{labels:{maxWidth:360}},");
        writer.AppendLine("        dataLabels:{enabled:true,formatter:value=>Number(value).toFixed(1)+'%'},");
        writer.AppendLine("        tooltip:{y:{formatter:value=>value+' ns'}},");
        writer.AppendLine("        legend:{position:'top'},");
        writer.AppendLine("        fill:{opacity:1}");
        writer.AppendLine("      };");
        writer.AppendLine("    }");
        writer.AppendLine("    const select=document.getElementById('seriesOrder');");
        writer.AppendLine("    const chart=new ApexCharts(document.querySelector('#chart'),buildOptions(orderedKeys(select.value)));");
        writer.AppendLine("    chart.render();");
        writer.AppendLine("    select.addEventListener('change',()=>{");
        writer.AppendLine("      const options=buildOptions(orderedKeys(select.value));");
        writer.AppendLine("      chart.updateOptions({colors:options.colors});");
        writer.AppendLine("      chart.updateSeries(options.series,true);");
        writer.AppendLine("    });");
        writer.AppendLine("  </script>");
        writer.AppendLine("  <h2>CPU run details</h2>");
        writer.AppendLine("  <table>");
        writer.AppendLine("    <thead><tr><th>CPU</th><th>Core</th><th>Type</th><th>Full function name</th><th>Status</th><th>Mean</th><th>Allocated bytes</th><th>Peak RSS bytes</th><th>Estimated peak RSS bytes</th></tr></thead>");
        writer.AppendLine("    <tbody>");
        foreach(var row in rows
            .Where(static row => GetReportSeries(row.Project) is not null)
            .OrderBy(static row => ParseIntOrMax(row.LogicalCpu))
            .ThenBy(static row => GetReportSeries(row.Project))
            .ThenBy(static row => row.FullFunctionName,StringComparer.OrdinalIgnoreCase)){
            writer.Append("      <tr><td>").Append(EscapeHtml(row.CpuDisplay)).Append("</td><td>");
            writer.Append(EscapeHtml(row.Core)).Append("</td><td>");
            writer.Append(EscapeHtml(GetReportSeriesName(row.Project))).Append("</td><td class=\"code\">");
            writer.Append(EscapeHtml(row.FullFunctionName)).Append("</td><td>");
            writer.Append(EscapeHtml(row.Status)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.AllocatedBytes)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.PeakRssBytes)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.EstimatedPeakRssBytes)).AppendLine("</td></tr>");
        }
        writer.AppendLine("    </tbody>");
        writer.AppendLine("  </table>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        return writer.ToString();
    }

    private static string BuildComparisonRowsJson(IReadOnlyList<ComparisonRow> rows){
        var writer = new StringBuilder();
        writer.Append('[');
        for(var i=0;i<rows.Count;i++){
            var row = rows[i];
            if(i>0){
                writer.Append(',');
            }

            writer.Append("{\"method\":\"").Append(EscapeJavaScriptString(row.Method)).Append('"');
            AppendJsonNumber(writer,",\"linq\":",GetPositiveValue(row.Linq));
            AppendJsonNumber(writer,",\"zlinq\":",GetPositiveValue(row.ZLinq));
            AppendJsonNumber(writer,",\"flinq\":",GetPositiveValue(row.FLinq));
            AppendJsonNumber(writer,",\"persistedlinq\":",GetPositiveValue(row.PersistedLinq));
            AppendJsonNumber(writer,",\"persistedflinq\":",GetPositiveValue(row.PersistedFLinq));
            AppendJsonNumber(writer,",\"tsavoritelinq\":",GetPositiveValue(row.TsavoriteLinq));
            AppendJsonNumber(writer,",\"tsavoriteflinq\":",GetPositiveValue(row.TsavoriteFLinq));
            AppendComparisonMemoryJson(writer,row.Linq,"linq");
            AppendComparisonMemoryJson(writer,row.ZLinq,"zlinq");
            AppendComparisonMemoryJson(writer,row.FLinq,"flinq");
            AppendComparisonMemoryJson(writer,row.PersistedLinq,"persistedlinq");
            AppendComparisonMemoryJson(writer,row.PersistedFLinq,"persistedflinq");
            AppendComparisonMemoryJson(writer,row.TsavoriteLinq,"tsavoritelinq");
            AppendComparisonMemoryJson(writer,row.TsavoriteFLinq,"tsavoriteflinq");
            writer.Append('}');
        }
        writer.Append(']');
        return writer.ToString();
    }

    private static string BuildFLinqSlowRowsJson(IReadOnlyList<ComparisonRow> rows){
        var slowRows = rows
            .Select(static row => new {
                row.Method,
                ZLinq = GetPositiveValue(row.ZLinq),
                FLinq = GetPositiveValue(row.FLinq),
            })
            .Where(static row => row.ZLinq>0&&row.FLinq>row.ZLinq)
            .Select(static row => new {
                row.Method,
                row.ZLinq,
                row.FLinq,
                Delta = row.FLinq-row.ZLinq,
                PercentSlower = (row.FLinq-row.ZLinq)/row.ZLinq*100,
            })
            .OrderByDescending(static row => row.Delta)
            .ThenBy(static row => row.Method,StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var writer = new StringBuilder();
        writer.Append('[');
        for(var i=0;i<slowRows.Length;i++){
            var row = slowRows[i];
            if(i>0){
                writer.Append(',');
            }

            writer.Append("{\"method\":\"").Append(EscapeJavaScriptString(row.Method)).Append('"');
            AppendJsonNumber(writer,",\"zlinq\":",row.ZLinq);
            AppendJsonNumber(writer,",\"flinq\":",row.FLinq);
            AppendJsonNumber(writer,",\"delta\":",row.Delta);
            AppendJsonNumber(writer,",\"percentSlower\":",row.PercentSlower);
            writer.Append('}');
        }
        writer.Append(']');
        return writer.ToString();
    }

    private static void AppendComparisonMemoryJson(StringBuilder writer,ComparisonValue value,string key){
        AppendJsonNumber(writer,",\""+key+"AllocatedBytes\":",GetPositiveMemoryValue(value.NumericAllocatedBytes));
        AppendJsonNumber(writer,",\""+key+"PeakRssBytes\":",GetPositiveMemoryValue(value.NumericPeakRssBytes));
        AppendJsonNumber(writer,",\""+key+"EstimatedPeakRssBytes\":",GetPositiveMemoryValue(value.NumericEstimatedPeakRssBytes));
    }

    private static string BuildReportRowsJson(IReadOnlyList<ReportRow> rows){
        var writer = new StringBuilder();
        writer.Append('[');
        for(var i=0;i<rows.Count;i++){
            var row = rows[i];
            if(i>0){
                writer.Append(',');
            }

            writer.Append('{');
            AppendJsonString(writer,"project",row.Project);
            AppendJsonString(writer,",method",row.Method);
            AppendJsonString(writer,",status",row.Status);
            AppendJsonString(writer,",logicalCpu",row.LogicalCpu);
            AppendJsonString(writer,",core",row.Core);
            AppendJsonString(writer,",cpuDisplay",row.CpuDisplay);
            AppendJsonString(writer,",startedAt",row.StartedAt);
            AppendJsonString(writer,",finishedAt",row.FinishedAt);
            AppendJsonNumber(writer,",\"meanValue\":",row.MeanNanoseconds);
            AppendJsonNumber(writer,",\"errorValue\":",row.ErrorNanoseconds);
            AppendJsonNumber(writer,",\"stdDevValue\":",row.StdDevNanoseconds);
            AppendJsonNumber(writer,",\"allocatedBytes\":",row.AllocatedBytesValue);
            AppendJsonNumber(writer,",\"peakRssBytes\":",row.PeakRssBytesValue);
            AppendJsonNumber(writer,",\"estimatedPeakRssBytes\":",row.EstimatedPeakRssBytesValue);
            writer.Append('}');
        }
        writer.Append(']');
        return writer.ToString();
    }

    private static void AppendJsonString(StringBuilder writer,string name,string value){
        if(name.StartsWith(",",StringComparison.Ordinal)){
            writer.Append(',');
            name=name[1..];
        }

        writer.Append('"').Append(name).Append("\":\"").Append(EscapeJavaScriptString(value)).Append('"');
    }

    private static void AppendJsonNumber(StringBuilder writer,string name,double value){
        writer.Append(name).Append(value.ToString("0.###",CultureInfo.InvariantCulture));
    }

    private static void AppendJsonNumber(StringBuilder writer,string name,long value){
        writer.Append(name).Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendSegment(StringBuilder writer,string cssClass,string label,ComparisonValue value,double ratio,int originalIndex){
        var width = ratio*100;
        var percentText = width.ToString("0.0",CultureInfo.InvariantCulture)+"%";
        writer.Append("      <div class=\"segment ").Append(cssClass).Append("\" data-key=\"").Append(cssClass).Append("\" data-value=\"");
        writer.Append(GetPositiveValue(value).ToString("0.###",CultureInfo.InvariantCulture));
        writer.Append("\" data-original=\"").Append(originalIndex.ToString(CultureInfo.InvariantCulture)).Append("\" style=\"width:");
        writer.Append(width.ToString("0.###",CultureInfo.InvariantCulture));
        writer.Append("%\" title=\"").Append(EscapeHtml(label)).Append(": ");
        writer.Append(EscapeHtml(value.Mean));
        writer.Append(" (").Append(percentText).AppendLine(")\">");
        if(width>=8){
            writer.Append("        <span>").Append(EscapeHtml(percentText)).AppendLine("</span>");
        }
        writer.AppendLine("      </div>");
    }

    private static double GetRatio(ComparisonValue value,ComparisonRow row){
        var total = GetPositiveValue(row.Linq)+GetPositiveValue(row.ZLinq)+GetPositiveValue(row.FLinq)+GetPositiveValue(row.PersistedLinq)+GetPositiveValue(row.PersistedFLinq)+GetPositiveValue(row.TsavoriteLinq)+GetPositiveValue(row.TsavoriteFLinq);
        return total>0
            ? GetPositiveValue(value)/total
            : 0;
    }

    private static double GetPositiveValue(ComparisonValue value){
        return value.NumericMean is { } number&&number>0
            ? number
            : 0;
    }

    private static double GetPositiveMemoryValue(long? value){
        return value is { } number&&number>0
            ? number
            : 0;
    }

    private static IReadOnlyList<ComparisonRow> CreateComparisonRows(IReadOnlyList<ReportRow> rows){
        var valuesByMethod = new Dictionary<string,ComparisonValue[]>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows){
            var series = GetReportSeries(row.Project);
            if(series is null){
                continue;
            }

            if(!valuesByMethod.TryGetValue(row.Method,out var values)){
                values = [ComparisonValue.Empty,ComparisonValue.Empty,ComparisonValue.Empty,ComparisonValue.Empty,ComparisonValue.Empty,ComparisonValue.Empty,ComparisonValue.Empty];
                valuesByMethod.Add(row.Method,values);
            }

            values[(int)series.Value]=CreateComparisonValue(row);
        }

        return valuesByMethod
            .OrderBy(static pair => pair.Key,StringComparer.OrdinalIgnoreCase)
            .Select(static pair => new ComparisonRow(pair.Key,pair.Value[0],pair.Value[1],pair.Value[2],pair.Value[3],pair.Value[4],pair.Value[5],pair.Value[6]))
            .ToArray();
    }

    private static ComparisonValue CreateComparisonValue(ReportRow row){
        if(!string.Equals(row.Status,"Completed",StringComparison.OrdinalIgnoreCase)){
            return ComparisonValue.Empty;
        }

        return row.MeanNanoseconds<=0
            ? ComparisonValue.Empty
            : new ComparisonValue(
                row.Mean,
                row.MeanNanoseconds,
                row.AllocatedBytesValue,
                row.PeakRssBytesValue,
                row.EstimatedPeakRssBytesValue);
    }

    private static ReportSeries? GetReportSeries(string project){
        return project switch{
            var value when string.Equals(value,"System LINQ",StringComparison.OrdinalIgnoreCase) => ReportSeries.Linq,
            var value when string.Equals(value,"System ZLinq",StringComparison.OrdinalIgnoreCase) => ReportSeries.ZLinq,
            var value when string.Equals(value,"System FLinq",StringComparison.OrdinalIgnoreCase) => ReportSeries.FLinq,
            var value when string.Equals(value,"Persisted LINQ",StringComparison.OrdinalIgnoreCase) => ReportSeries.PersistedLinq,
            var value when string.Equals(value,"Persisted FLinq",StringComparison.OrdinalIgnoreCase) => ReportSeries.PersistedFLinq,
            var value when string.Equals(value,"Tsavorite LINQ",StringComparison.OrdinalIgnoreCase) => ReportSeries.TsavoriteLinq,
            var value when string.Equals(value,"Tsavorite FLinq",StringComparison.OrdinalIgnoreCase) => ReportSeries.TsavoriteFLinq,
            _ => null,
        };
    }

    private static string GetReportSeriesName(string project){
        return GetReportSeries(project) switch{
            ReportSeries.Linq => "linq",
            ReportSeries.ZLinq => "zlinq",
            ReportSeries.FLinq => "flinq",
            ReportSeries.PersistedLinq => "persistedlinq",
            ReportSeries.PersistedFLinq => "persistedflinq",
            ReportSeries.TsavoriteLinq => "tsavoritelinq",
            ReportSeries.TsavoriteFLinq => "tsavoriteflinq",
            _ => project,
        };
    }

    private static int ParseIntOrMax(string value){
        return int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var number)
            ? number
            : int.MaxValue;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string value){
        return DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var result)
            ? result
            : null;
    }

    private static void AppendTimelineAxis(StringBuilder writer,double totalSeconds,string cssClass,TimelineAxisLabelStyle labelStyle){
        writer.Append("    <div class=\"axis ").Append(cssClass).AppendLine("\">");
        for(var i=0;i<=10;i++){
            var left = i*10;
            var seconds = totalSeconds*i/10;
            writer.Append("      <div class=\"tick\" style=\"left:")
                .Append(left.ToString(CultureInfo.InvariantCulture))
                .Append("%\"><span>")
                .Append(EscapeHtml(FormatAxisElapsed(TimeSpan.FromSeconds(seconds),labelStyle)))
                .AppendLine("</span></div>");
        }
        writer.AppendLine("    </div>");
    }

    private static string FormatAxisElapsed(TimeSpan elapsed,TimelineAxisLabelStyle labelStyle){
        return labelStyle switch{
            TimelineAxisLabelStyle.Seconds => Math.Round(elapsed.TotalSeconds).ToString("0",CultureInfo.InvariantCulture)+"s",
            TimelineAxisLabelStyle.Clock => elapsed.ToString(@"hh\:mm\:ss",CultureInfo.InvariantCulture),
            _ => FormatElapsed(elapsed),
        };
    }

    private static string FormatElapsed(TimeSpan elapsed){
        if(elapsed.TotalHours>=1){
            return elapsed.ToString(@"h\:mm\:ss",CultureInfo.InvariantCulture);
        }

        return elapsed.ToString(@"m\:ss",CultureInfo.InvariantCulture);
    }

    private static string EscapeHtml(string value){
        return value
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal);
    }

    private static string EscapeJavaScriptString(string value){
        return value
            .Replace("\\","\\\\",StringComparison.Ordinal)
            .Replace("\"","\\\"",StringComparison.Ordinal)
            .Replace("\r","\\r",StringComparison.Ordinal)
            .Replace("\n","\\n",StringComparison.Ordinal)
            .Replace("<","\\u003c",StringComparison.Ordinal)
            .Replace(">","\\u003e",StringComparison.Ordinal)
            .Replace("&","\\u0026",StringComparison.Ordinal);
    }

    private enum ReportSeries{
        Linq,
        ZLinq,
        FLinq,
        PersistedLinq,
        PersistedFLinq,
        TsavoriteLinq,
        TsavoriteFLinq,
    }

    private sealed record ComparisonRow(
        string Method,
        ComparisonValue Linq,
        ComparisonValue ZLinq,
        ComparisonValue FLinq,
        ComparisonValue PersistedLinq,
        ComparisonValue PersistedFLinq,
        ComparisonValue TsavoriteLinq,
        ComparisonValue TsavoriteFLinq);

    private sealed record ComparisonValue(
        string Mean,
        double? NumericMean,
        long? NumericAllocatedBytes,
        long? NumericPeakRssBytes,
        long? NumericEstimatedPeakRssBytes){
        public static readonly ComparisonValue Empty = new(string.Empty,null,null,null,null);
    }

    private sealed record TimelineItem(ReportRow Row,DateTimeOffset? StartedAt,DateTimeOffset? FinishedAt);

    private enum TimelineAxisLabelStyle{
        Seconds,
        Clock,
    }
}

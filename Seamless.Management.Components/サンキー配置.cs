using System.Globalization;
using Seamless.Management;

namespace Seamless.Management.Components;

internal sealed record サンキー配置ノード(ExecutionPlanNode 元,double X,double Y,double 高さ);
internal sealed record サンキー配置接続(DataFlowLink 元,double 始幅,double 終幅,FlowQuantity 始量,FlowQuantity 終量,string 帯,string 中心線);
internal sealed class サンキー配置{
    internal サンキー配置ノード[] ノード{get;private init;}=[];
    internal サンキー配置接続[] 接続{get;private init;}=[];
    internal double 幅{get;private init;}=600;
    internal double 高さ{get;private init;}=360;
    internal bool 循環か{get;private init;}
    internal bool 幅補正か{get;private init;}
    internal const int ノード幅=180;
    internal static サンキー配置 作成(QueryVisualizationData data,string unit,string basis,string scale){
        data.Validate();
        if(unit is not("Rows" or "Bytes")||basis is not("Observed" or "Estimated")||scale is not("Linear" or "Log1p")){throw new ArgumentException("サンキーの表示設定が不正です。");}
        var portMetrics=data.PortQuantities.ToDictionary(value=>value.PortId,StringComparer.Ordinal);
        var linkMetrics=data.LinkQuantities.ToDictionary(value=>value.LinkId,StringComparer.Ordinal);
        FlowQuantity Port(string id){
            if(!portMetrics.TryGetValue(id,out var value)){return FlowQuantity.Unknown();}
            return Quantity(value.EstimatedRows,value.ObservedRows,value.EstimatedBytes,value.ObservedBytes,unit,basis);
        }
        FlowQuantity Link(string id){
            if(!linkMetrics.TryGetValue(id,out var value)){return FlowQuantity.Unknown();}
            return Quantity(value.EstimatedRows,value.ObservedRows,value.EstimatedBytes,value.ObservedBytes,unit,basis);
        }
        // 比較の左右も同じ尺度を使う。未計測を総量や別のポートから補わない。
        var all=data.PortQuantities.SelectMany(value=>new[]{Quantity(value.EstimatedRows,value.ObservedRows,value.EstimatedBytes,value.ObservedBytes,unit,"Observed"),Quantity(value.EstimatedRows,value.ObservedRows,value.EstimatedBytes,value.ObservedBytes,unit,"Estimated")})
            .Concat(data.LinkQuantities.SelectMany(value=>new[]{Quantity(value.EstimatedRows,value.ObservedRows,value.EstimatedBytes,value.ObservedBytes,unit,"Observed"),Quantity(value.EstimatedRows,value.ObservedRows,value.EstimatedBytes,value.ObservedBytes,unit,"Estimated")}));
        double Transform(double value){
            if(scale!="Log1p"){return value;}
            var sum=1+value;if(sum==1){return value;}
            return Math.Log(sum)*value/(sum-1);
        }
        var maximum=all.Where(value=>value.DecimalValue is not null).Select(value=>Transform(Number(value))).DefaultIfEmpty(0).Max();var adjusted=false;
        double Width(FlowQuantity value){
            if(value.DecimalValue is null){return 3;}
            var number=Number(value);if(number==0){return 0;}
            var width=0d;if(maximum>0){width=Transform(number)/maximum*64;}
            if(width<2){adjusted=true;return 2;}return Math.Min(width,64);
        }
        var ports=data.Ports.ToDictionary(port=>port.Id,StringComparer.Ordinal);
        var used=data.Ports.Select(port=>port.Node.NodeId).ToHashSet(StringComparer.Ordinal);
        var sourceNodes=data.Plan.Nodes.Where(node=>used.Contains(node.Id)).ToArray();
        var ranks=sourceNodes.ToDictionary(node=>node.Id,_=>0,StringComparer.Ordinal);
        var incoming=sourceNodes.ToDictionary(node=>node.Id,_=>0,StringComparer.Ordinal);
        var outgoing=sourceNodes.ToDictionary(node=>node.Id,_=>new List<string>(),StringComparer.Ordinal);
        foreach(var link in data.Links){var from=ports[link.SourcePortId].Node.NodeId;var to=ports[link.TargetPortId].Node.NodeId;outgoing[from].Add(to);incoming[to]++;}
        var ready=new Queue<string>(sourceNodes.Where(node=>incoming[node.Id]==0).Select(node=>node.Id));var visited=0;
        while(ready.TryDequeue(out var id)){
            visited++;foreach(var next in outgoing[id]){ranks[next]=Math.Max(ranks[next],ranks[id]+1);if(--incoming[next]==0){ready.Enqueue(next);}}
        }
        var quantities=new Dictionary<string,FlowQuantity>(StringComparer.Ordinal);
        foreach(var port in data.Ports){quantities.Add(port.Id,Port(port.Id));}
        // 接続で観測された量はその接続の端にだけ使う。共有出力全体の量へ集約しない。
        FlowQuantity End(string port,string link){var value=quantities[port];if(value.DecimalValue is null){return Link(link);}return value;}
        var widths=data.Ports.ToDictionary(port=>port.Id,port=>Width(quantities[port.Id]),StringComparer.Ordinal);
        foreach(var link in data.Links){widths[link.SourcePortId]=Math.Max(widths[link.SourcePortId],Width(End(link.SourcePortId,link.Id)));widths[link.TargetPortId]=Math.Max(widths[link.TargetPortId],Width(End(link.TargetPortId,link.Id)));}
        var nodePorts=data.Ports.GroupBy(port=>port.Node.NodeId).ToDictionary(group=>group.Key,group=>group.OrderBy(port=>port.Index).ToArray(),StringComparer.Ordinal);
        var nodes=new List<サンキー配置ノード>();var columns=new Dictionary<int,double>();var anchors=new Dictionary<string,(double X,double Y)>(StringComparer.Ordinal);
        foreach(var node in sourceNodes){
            var list=nodePorts[node.Id];var ins=list.Where(port=>port.Direction=="Input").ToArray();var outs=list.Where(port=>port.Direction=="Output").ToArray();
            var height=Math.Max(72,Math.Max(ins.Sum(port=>Math.Max(widths[port.Id],12)+8),outs.Sum(port=>Math.Max(widths[port.Id],12)+8))+24);
            var rank=ranks[node.Id];columns.TryGetValue(rank,out var y);y+=24;var x=24+rank*300;
            nodes.Add(new(node,x,y,height));columns[rank]=y+height+30;
            void Anchors(DataFlowPort[] items,double atX){
                var offset=y+12;foreach(var port in items){var extent=Math.Max(widths[port.Id],12);anchors.Add(port.Id,(atX,offset+extent/2));offset+=extent+8;}
            }
            Anchors(ins,x);Anchors(outs,x+ノード幅);
        }
        var edges=new List<サンキー配置接続>();
        foreach(var link in data.Links){
            var a=anchors[link.SourcePortId];var b=anchors[link.TargetPortId];var left=End(link.SourcePortId,link.Id);var right=End(link.TargetPortId,link.Id);var wa=Width(left);var wb=Width(right);var middle=(a.X+b.X)/2;
            var path=$"M{F(a.X)} {F(a.Y-wa/2)} C{F(middle)} {F(a.Y-wa/2)} {F(middle)} {F(b.Y-wb/2)} {F(b.X)} {F(b.Y-wb/2)} L{F(b.X)} {F(b.Y+wb/2)} C{F(middle)} {F(b.Y+wb/2)} {F(middle)} {F(a.Y+wa/2)} {F(a.X)} {F(a.Y+wa/2)} Z";
            edges.Add(new(link,wa,wb,left,right,path,$"M{F(a.X)} {F(a.Y)} C{F(middle)} {F(a.Y)} {F(middle)} {F(b.Y)} {F(b.X)} {F(b.Y)}"));
        }
        return new(){ノード=nodes.ToArray(),接続=edges.ToArray(),幅=Math.Max(600,nodes.Select(node=>node.X+ノード幅+24).DefaultIfEmpty(0).Max()),高さ=Math.Max(360,columns.Values.DefaultIfEmpty(0).Max()),循環か=visited!=sourceNodes.Length,幅補正か=adjusted};
    }
    internal static FlowQuantity Quantity(FlowQuantity estimatedRows,FlowQuantity observedRows,FlowQuantity estimatedBytes,FlowQuantity observedBytes,string unit,string basis){
        if(unit=="Bytes"){if(basis=="Estimated"){return estimatedBytes;}return observedBytes;}
        if(basis=="Estimated"){return estimatedRows;}return observedRows;
    }
    private static double Number(FlowQuantity value)=>double.Parse(value.DecimalValue!,CultureInfo.InvariantCulture);
    private static string F(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
}

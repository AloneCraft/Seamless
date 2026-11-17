using System.Security.Cryptography;
using System.Text;

namespace Seamless.Management;

/// <summary>最終生成メソッド本文と、型解決済みの制御フロー。範囲は本文先頭からのUTF16位置。</summary>
public sealed record GeneratedExecutionCode(string Status,string? MethodKey,string? BodyHash,string? BodyText,GeneratedBasicBlock[] Blocks,GeneratedControlEdge[] Edges,GeneratedControlRegion[] Regions,string[] Diagnostics,int OmittedBlockCount=0,int OmittedOperationCount=0);
public sealed record GeneratedBasicBlock(int Id,string Kind,bool IsReachable,string ConditionKind,GeneratedOperation[] Operations,GeneratedOperation? BranchValue);
public sealed record GeneratedOperation(string Id,string Kind,int Start,int Length,string? OutputType,string? Symbol,bool IsImplicit);
public sealed record GeneratedControlEdge(int From,int? To,string Kind,string Semantics,int[] EnteringRegions,int[] LeavingRegions,int[] FinallyRegions);
public sealed record GeneratedControlRegion(int Id,string Kind,int FirstBlock,int LastBlock,int? ParentId,string[] Locals);

internal static class GeneratedExecutionCodeValidator{
    internal const int MaximumBodyCharacters=65536,MaximumBlocks=2048,MaximumOperations=8192,MaximumEdges=8192,MaximumRegions=2048;
    private static readonly UTF8Encoding StrictUtf8=new(false,true);

    internal static void Validate(GeneratedExecutionCode? code){
        if(code is null){return;}
        if(code.Status is not ("Available" or "Unknown" or "Limited")||code.Blocks is null||code.Edges is null||code.Regions is null||code.Diagnostics is null||code.OmittedBlockCount<0||code.OmittedOperationCount<0){throw new InvalidDataException("生成コードの収集状態が不正です。");}
        if(code.Blocks.Length>MaximumBlocks||code.Edges.Length>MaximumEdges||code.Regions.Length>MaximumRegions||code.Diagnostics.Length>128){throw new InvalidDataException("生成コードの構造が上限を超えています。");}
        Text(code.MethodKey,4096);
        if(code.Status=="Available"&&(string.IsNullOrEmpty(code.MethodKey)||code.BodyText is null||code.Blocks.Length==0||code.OmittedBlockCount!=0||code.OmittedOperationCount!=0)){throw new InvalidDataException("生成コードの完全性が不正です。");}
        if(code.BodyText is{} body){
            Text(body,MaximumBodyCharacters);
            var actualHash=Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(body)));
            if(!string.Equals(actualHash,code.BodyHash,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("最終生成本文のハッシュが一致しません。");}
        }else if(code.BodyHash is not null){throw new InvalidDataException("最終生成本文のハッシュに対応する本文がありません。");}
        var blocks=new HashSet<int>();
        var operations=new HashSet<string>(StringComparer.Ordinal);
        foreach(var block in code.Blocks){
            if(block is null||block.Id<0||!blocks.Add(block.Id)||block.Operations is null){throw new InvalidDataException("生成コードの基本ブロックが不正です。");}
            if(block.Operations.Length>MaximumOperations-operations.Count){throw new InvalidDataException("生成コードの操作数が上限を超えています。");}
            RequiredText(block.Kind);RequiredText(block.ConditionKind);
            foreach(var operation in block.Operations){Operation(operation,code.BodyText,operations);}
            if(block.BranchValue is{} branch){Operation(branch,code.BodyText,operations);}
        }
        if(operations.Count>MaximumOperations){throw new InvalidDataException("生成コードの操作数が上限を超えています。");}
        var regions=new Dictionary<int,GeneratedControlRegion>();
        foreach(var region in code.Regions){
            if(region is null||region.Id<0||!regions.TryAdd(region.Id,region)||region.Locals is null||region.Locals.Length>MaximumOperations||!blocks.Contains(region.FirstBlock)||!blocks.Contains(region.LastBlock)||region.FirstBlock>region.LastBlock){throw new InvalidDataException("生成コードの領域が不正です。");}
            RequiredText(region.Kind);foreach(var local in region.Locals){RequiredText(local);}
        }
        foreach(var region in code.Regions){
            var current=region;var ancestors=new HashSet<int>{region.Id};
            while(current.ParentId is{} parent){
                if(!regions.TryGetValue(parent,out var outer)||!ancestors.Add(parent)||outer.FirstBlock>current.FirstBlock||outer.LastBlock<current.LastBlock){throw new InvalidDataException("生成コードの領域の親子関係が不正です。");}
                current=outer;
            }
        }
        foreach(var edge in code.Edges){
            if(edge is null||!blocks.Contains(edge.From)||edge.To is{} destination&&!blocks.Contains(destination)||edge.EnteringRegions is null||edge.LeavingRegions is null||edge.FinallyRegions is null){throw new InvalidDataException("生成コードの制御フロー接続が不正です。");}
            RequiredText(edge.Kind);RequiredText(edge.Semantics);
            foreach(var group in new[]{edge.EnteringRegions,edge.LeavingRegions,edge.FinallyRegions}){
                if(group.Length>MaximumRegions||group.Any(id=>!regions.ContainsKey(id))){throw new InvalidDataException("生成コードの制御フロー領域が不正です。");}
            }
        }
        foreach(var diagnostic in code.Diagnostics){RequiredText(diagnostic);}
    }
    private static void Operation(GeneratedOperation operation,string? body,HashSet<string> ids){
        if(operation is null||string.IsNullOrWhiteSpace(operation.Id)||!ids.Add(operation.Id)||operation.Start< -1||operation.Length<0||operation.Start== -1&&operation.Length!=0||body is null||operation.Start>body.Length||operation.Start>=0&&operation.Length>body.Length-operation.Start){throw new InvalidDataException("生成コードの操作または本文範囲が不正です。");}
        RequiredText(operation.Id);RequiredText(operation.Kind);Text(operation.OutputType,4096);Text(operation.Symbol,4096);
    }
    private static void RequiredText(string value){if(string.IsNullOrWhiteSpace(value)){throw new InvalidDataException("生成コードの必須文字列がありません。");}Text(value,4096);}
    private static void Text(string? value,int maximum){
        if(value is null){return;}
        if(value.Length>maximum){throw new InvalidDataException("生成コードの文字列が上限を超えています。");}
        try{_=StrictUtf8.GetByteCount(value);}catch(EncoderFallbackException exception){throw new InvalidDataException("生成コードの文字列が不正です。",exception);}
    }
}

namespace Seamless.FLinq.Generator;

internal sealed record GeneratedExecutionCode(string Status,string? MethodKey,string? BodyHash,string? BodyText,GeneratedBasicBlock[] Blocks,GeneratedControlEdge[] Edges,GeneratedControlRegion[] Regions,string[] Diagnostics,int OmittedBlockCount=0,int OmittedOperationCount=0);
internal sealed record GeneratedBasicBlock(int Id,string Kind,bool IsReachable,string ConditionKind,GeneratedOperation[] Operations,GeneratedOperation? BranchValue);
internal sealed record GeneratedOperation(string Id,string Kind,int Start,int Length,string? OutputType,string? Symbol,bool IsImplicit);
internal sealed record GeneratedControlEdge(int From,int? To,string Kind,string Semantics,int[] EnteringRegions,int[] LeavingRegions,int[] FinallyRegions);
internal sealed record GeneratedControlRegion(int Id,string Kind,int FirstBlock,int LastBlock,int? ParentId,string[] Locals);
internal sealed record ExecutionPlanDraft(string OriginalJson,ExecutionPlanDocument Document){public string? ProfiledMethodName{get;init;}}
internal sealed record ExecutionProfileBinding(ExecutionPlanDocument Document,string NodeId);

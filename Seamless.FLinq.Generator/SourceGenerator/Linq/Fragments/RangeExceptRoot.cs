namespace Seamless.FLinq;

internal sealed class RangeExceptRoot(int 番号,RangeRoot source,IReadOnlyList<RangeRoot> exclusions):Enumerable(番号) {
    internal RangeRoot SourceRange => source;
    internal IReadOnlyList<RangeRoot> Exclusions => exclusions;
    public override string StructMethod => string.Empty;
    public override string ElementType => "int";
    public override string GetEnumeratorExpression {
        get {
            var args = new List<string> { source.StartExpression,source.CountExpression };
            foreach(var exclusion in exclusions) {
                args.Add(exclusion.StartExpression);
                args.Add(exclusion.CountExpression);
            }
            return RenderEnumeratorExpression(this.EnumeratorTypeName,args.ToArray());
        }
    }
    public override string GetEnumeratorPrimaryConstructorParameters {
        get {
            var parameters = new List<string> { "int start","int count" };
            for(var index = 0;index<exclusions.Count;index++) {
                parameters.Add($"int excludedStart{index}");
                parameters.Add($"int excludedCount{index}");
            }
            return string.Join(", ",parameters);
        }
    }
    public override string GetEnumeratorFields => $"private int {this.e} = -1;";
    public override string GetTryGetNextBody {
        get {
            var builder = new StringBuilder();
            builder.AppendLine($"while(++{this.e} < count) {{");
            builder.AppendLine($"    var __value=start + {this.e};");
            for(var index = 0;index<exclusions.Count;index++) {
                builder.AppendLine($"    var __excludedEnd{index}=(long)excludedStart{index} + excludedCount{index};");
                builder.AppendLine($"    if((long)__value >= excludedStart{index} && (long)__value < __excludedEnd{index}) continue;");
            }
            builder.AppendLine("    current = this.Current = __value;");
            builder.AppendLine("    return true;");
            builder.AppendLine("}");
            builder.AppendLine("current = default;");
            builder.AppendLine("return false;");
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Seamless.Linq.IValueEnumerator<int>
        {
            private int start;
            private int count;
            {{this.ExclusionFields}}
            private int {{this.e}} = -1;
            public {{this.Struct}}({{this.GetEnumeratorPrimaryConstructorParameters}}){
                this.start = start;
                this.count = count;
                {{this.ExclusionAssignments}}
            }
            public int Current{get;private set;}=default!;
            public bool TryGetNonEnumeratedCount(out int count) { count = 0; return false; }
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) { span = default; return false; }
            public bool TryCopyTo(scoped global::System.Span<int> destination,global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out int current){
                {{this.GetTryGetNextBody}}
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){}
        }
        """;
    public override string GetConsumeExecutionStatement(string consumerName) {
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var index = $"__i{番号}";
        var value = $"__value{番号}";
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine($"    var {start}={source.StartExpression};");
        builder.AppendLine($"    var {count}={source.CountExpression};");
        for(var exclusionIndex = 0;exclusionIndex<exclusions.Count;exclusionIndex++) {
            var exclusion = exclusions[exclusionIndex];
            builder.AppendLine($"    var __excludedStart{番号}_{exclusionIndex}={exclusion.StartExpression};");
            builder.AppendLine($"    var __excludedCount{番号}_{exclusionIndex}={exclusion.CountExpression};");
            builder.AppendLine($"    var __excludedEnd{番号}_{exclusionIndex}=(long)__excludedStart{番号}_{exclusionIndex} + __excludedCount{番号}_{exclusionIndex};");
        }
        if(exclusions.Count==1) {
            var excludedStart = $"__excludedStart{番号}_0";
            var excludedCount = $"__excludedCount{番号}_0";
            var excludedEnd = $"__excludedEnd{番号}_0";
            var sourceStartLong = $"__sourceStartLong{番号}";
            var prefixEnd = $"__prefixEnd{番号}";
            var suffixStart = $"__suffixStart{番号}";
            var prefixIndex = $"__prefixIndex{番号}";
            var suffixIndex = $"__suffixIndex{番号}";
            builder.AppendLine($"    if({excludedCount}>0){{");
            builder.AppendLine($"        var {sourceStartLong}=(long){start};");
            builder.AppendLine($"        var {prefixEnd}=global::System.Math.Min((long){count},global::System.Math.Max(0L,(long){excludedStart}-{sourceStartLong}));");
            builder.AppendLine($"        for(var {prefixIndex}=0;(long){prefixIndex}<{prefixEnd};{prefixIndex}++){{");
            builder.AppendLine($"            {consumerName}.Consume({start} + {prefixIndex});");
            builder.AppendLine("        }");
            builder.AppendLine($"        var {suffixStart}=global::System.Math.Max(0L,global::System.Math.Min((long){count},{excludedEnd}-{sourceStartLong}));");
            builder.AppendLine($"        for(var {suffixIndex}=(int){suffixStart};{suffixIndex}<{count};{suffixIndex}++){{");
            builder.AppendLine($"            {consumerName}.Consume({start} + {suffixIndex});");
            builder.AppendLine("        }");
            builder.AppendLine("    }else{");
            builder.AppendLine($"        for(var {index}=0;{index}<{count};{index}++){{");
            builder.AppendLine($"            var {value}={start} + {index};");
            builder.AppendLine($"            if((long){value} >= {excludedStart} && (long){value} < {excludedEnd}) continue;");
            builder.AppendLine($"            {consumerName}.Consume({value});");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
        } else {
            builder.AppendLine($"    for(var {index}=0;{index}<{count};{index}++){{");
            builder.AppendLine($"        var {value}={start} + {index};");
            for(var exclusionIndex = 0;exclusionIndex<exclusions.Count;exclusionIndex++) {
                builder.AppendLine($"        if((long){value} >= __excludedStart{番号}_{exclusionIndex} && (long){value} < __excludedEnd{番号}_{exclusionIndex}) continue;");
            }
            builder.AppendLine($"        {consumerName}.Consume({value});");
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string ExclusionFields {
        get {
            var builder = new StringBuilder();
            for(var index = 0;index<exclusions.Count;index++) {
                builder.AppendLine($"private int excludedStart{index};");
                builder.AppendLine($"private int excludedCount{index};");
            }
            return builder.ToString().TrimEnd();
        }
    }

    private string ExclusionAssignments {
        get {
            var builder = new StringBuilder();
            for(var index = 0;index<exclusions.Count;index++) {
                builder.AppendLine($"this.excludedStart{index} = excludedStart{index};");
                builder.AppendLine($"this.excludedCount{index} = excludedCount{index};");
            }
            return builder.ToString().TrimEnd();
        }
    }

    internal RangeExceptRoot AddExclusion(int number,RangeRoot exclusion) {
        var next = new List<RangeRoot>(exclusions.Count+1);
        next.AddRange(exclusions);
        if(!next.Any(existing =>
            string.Equals(NormalizeExpression(existing.StartExpression),NormalizeExpression(exclusion.StartExpression),StringComparison.Ordinal)
            &&string.Equals(NormalizeExpression(existing.CountExpression),NormalizeExpression(exclusion.CountExpression),StringComparison.Ordinal))) {
            next.Add(exclusion);
        }
        return new RangeExceptRoot(number,source,next);
    }

    private static string NormalizeExpression(string expression) =>
        new(expression.Where(static c => !char.IsWhiteSpace(c)).ToArray());
}

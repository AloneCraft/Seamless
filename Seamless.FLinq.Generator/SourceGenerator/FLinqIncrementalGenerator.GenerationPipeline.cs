namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator {
    internal sealed partial class GenerationPipeline {
        private readonly GenerationState state;

        internal GenerationPipeline(GenerationState state) {
            this.state=state;
        }

        internal GenerationState State => this.state;
        internal string[] OuterTypeParameters => this.state.Target.MethodTypeParameters;
    }
}
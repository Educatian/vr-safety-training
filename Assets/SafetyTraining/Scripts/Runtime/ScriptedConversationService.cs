using System.Threading;
using System.Threading.Tasks;

namespace SafetyTraining.Runtime
{
    public sealed class ScriptedConversationService : IConversationService
    {
        public Task<ConversationReply> ReplyAsync(
            ConversationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = $"As the {request.npcRole} for {request.siteName}, I can see this situation: " +
                       $"{request.progress} Your question was: {request.learnerMessage} " +
                       $"Use this verified guidance, then tell me what evidence supports your choice: " +
                       $"{request.safetyFacts}";
            return Task.FromResult(new ConversationReply(text, false));
        }
    }
}

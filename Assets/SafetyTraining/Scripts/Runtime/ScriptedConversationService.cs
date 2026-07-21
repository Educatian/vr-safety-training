using System;
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
            return Task.FromResult(new ConversationReply(Compose(request), false));
        }

        static string Compose(ConversationRequest request)
        {
            var message = request.learnerMessage ?? string.Empty;
            if (ContainsAny(message, "hint", "where", "find", "look", "stuck", "help"))
                return $"Walk the {request.siteName} area slowly and compare similar-looking conditions. " +
                       "Two of them are genuinely unsafe and two are controlled look-alikes. " +
                       "Focus on what protection is present or missing, then commit to an inspection.";
            if (ContainsAny(message, "control", "fix", "how", "protect", "prevent", "osha", "rule", "regulation"))
                return $"As the {request.npcRole}, here is the verified guidance for this area: " +
                       $"{request.safetyFacts}";
            if (ContainsAny(message, "report", "explain", "why", "debrief", "done", "finish", "submit"))
                return "Before you submit, connect your strongest observations to the control decision: " +
                       "what did you see, what risk does it create, and which control removes that risk? " +
                       "State that chain in your report.";
            return $"Tell me what you have observed so far in {request.siteName}, " +
                   "and I will help you compare it against the required controls. " +
                   "You can ask for a hint, a control explanation, or a debrief check.";
        }

        static bool ContainsAny(string message, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}

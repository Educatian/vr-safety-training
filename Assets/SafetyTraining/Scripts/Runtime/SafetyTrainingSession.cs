using System;

namespace SafetyTraining.Runtime
{
    public static class SafetyTrainingSession
    {
        static string sessionId = string.Empty;

        public static string SessionId
        {
            get
            {
                if (string.IsNullOrEmpty(sessionId))
                    sessionId = Guid.NewGuid().ToString("N");
                return sessionId;
            }
        }

        public static void ResetForTests()
        {
            sessionId = string.Empty;
        }
    }
}

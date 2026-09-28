using UnityEngine;

namespace SafetyTraining.Runtime
{
    [CreateAssetMenu(menuName = "Safety Training/LLM Endpoint Config")]
    public sealed class LlmEndpointConfig : ScriptableObject
    {
        [SerializeField] string endpoint = "http://localhost:11434/v1/chat/completions";
        [SerializeField] string model = "hermes3:8b";
        [SerializeField] string apiKeyEnvironmentVariable = "";
        [SerializeField, Min(5)] int timeoutSeconds = 20;
        [Tooltip("Editor/desktop only: a local file holding the API key (never committed, never used in web builds).")]
        [SerializeField] string apiKeyFile = "";
        [Tooltip("Web builds call this proxy instead (it holds the key server-side). Empty = LLM off on the web.")]
        [SerializeField] string webProxyEndpoint = "";

        public string Endpoint => endpoint;
        public string Model => model;
        public string ApiKeyEnvironmentVariable => apiKeyEnvironmentVariable;
        public int TimeoutSeconds => timeoutSeconds;
        public string ApiKeyFile => apiKeyFile;
        public string WebProxyEndpoint => webProxyEndpoint;

        public void ConfigureOpenRouter(string modelId, string keyEnvVar, string keyFile, string proxy)
        {
            endpoint = "https://openrouter.ai/api/v1/chat/completions";
            model = modelId; apiKeyEnvironmentVariable = keyEnvVar; apiKeyFile = keyFile; webProxyEndpoint = proxy;
            timeoutSeconds = 25;
        }
    }
}

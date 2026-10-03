// Cloudflare Worker: server-side OpenRouter proxy for the WebGL build (the browser never sees the key).
// Deploy:  wrangler secret put OPENROUTER_API_KEY   then   wrangler deploy
// Set ALLOWED_ORIGIN to the site hosting the WebGL build. Paste the worker URL into
// Assets/_Game/Settings/LLM_OpenRouter.asset -> Web Proxy Endpoint.
const MODEL = "anthropic/claude-haiku-4.5"; // pinned: clients cannot pick a pricier model
const MAX_TOKENS = 220;                     // short NPC replies only
const MAX_BODY = 16_000;                    // bytes

export default {
  async fetch(request, env) {
    const origin = env.ALLOWED_ORIGIN || "*";
    const cors = {
      "Access-Control-Allow-Origin": origin,
      "Access-Control-Allow-Methods": "POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type",
    };
    if (request.method === "OPTIONS") return new Response(null, { headers: cors });
    if (request.method !== "POST") return new Response("POST only", { status: 405, headers: cors });

    const raw = await request.text();
    if (raw.length > MAX_BODY) return new Response("Request too large", { status: 413, headers: cors });
    let body;
    try { body = JSON.parse(raw); } catch { return new Response("Bad JSON", { status: 400, headers: cors }); }
    if (!Array.isArray(body.messages)) return new Response("messages required", { status: 400, headers: cors });

    const upstream = await fetch("https://openrouter.ai/api/v1/chat/completions", {
      method: "POST",
      headers: {
        "Authorization": `Bearer ${env.OPENROUTER_API_KEY}`,
        "Content-Type": "application/json",
        "X-Title": "Competent Person safety training",
      },
      body: JSON.stringify({
        model: MODEL,
        messages: body.messages.slice(-12),
        temperature: Math.min(Number(body.temperature) || 0.35, 0.7),
        max_tokens: MAX_TOKENS,
      }),
    });
    return new Response(upstream.body, {
      status: upstream.status,
      headers: { ...cors, "Content-Type": "application/json" },
    });
  },
};

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ChatBot;

/// <summary>
/// AI replies through any OpenAI-compatible chat API (Groq by default, which has a free tier).
/// Keeps a short memory per channel so follow-up questions make sense.
/// </summary>
public sealed class AiChat
{
    private const int MemoryTurns = 10;
    private const string SystemPrompt =
        "You are a friendly, concise chat bot in a Discord server. Answer in at most a few short " +
        "paragraphs, use Discord markdown, and never pretend to be a human.";

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ConcurrentDictionary<ulong, List<(string Role, string Text)>> _memory = new();

    public AiChat(string apiKey, string model, string baseUrl, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
        _http.Timeout = TimeSpan.FromSeconds(60);
        _model = model;
    }

    public static AiChat? FromEnvironment()
    {
        string? key = Environment.GetEnvironmentVariable("AI_API_KEY") ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return null;
        return new AiChat(
            key,
            Environment.GetEnvironmentVariable("AI_MODEL") ?? "openai/gpt-oss-120b",
            Environment.GetEnvironmentVariable("AI_BASE_URL") ?? "https://api.groq.com/openai/v1");
    }

    public void Forget(ulong channelId) => _memory.TryRemove(channelId, out _);

    public async Task<string> AskAsync(ulong channelId, string userName, string message)
    {
        var history = _memory.GetOrAdd(channelId, _ => new List<(string, string)>());
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = SystemPrompt } };
        lock (history)
        {
            foreach (var (role, text) in history)
                messages.Add(new JsonObject { ["role"] = role, ["content"] = text });
        }
        string userText = $"{userName}: {message}";
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = userText });

        var body = new JsonObject { ["model"] = _model, ["messages"] = messages, ["max_tokens"] = 1500 };
        if (_model.StartsWith("openai/gpt-oss")) body["reasoning_effort"] = "low";

        using var response = await _http.PostAsJsonAsync("chat/completions", body);
        if (!response.IsSuccessStatusCode)
        {
            int status = (int)response.StatusCode;
            return status == 429
                ? "I'm being rate limited by the AI provider. Try again in a minute."
                : $"The AI provider returned an error ({status}).";
        }

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        string reply = json?["choices"]?[0]?["message"]?["content"]?.GetValue<string>()?.Trim() ?? "";
        if (reply.Length == 0) return "I got an empty answer. Try rephrasing?";

        lock (history)
        {
            history.Add(("user", userText));
            history.Add(("assistant", reply));
            if (history.Count > MemoryTurns * 2) history.RemoveRange(0, history.Count - MemoryTurns * 2);
        }
        return reply;
    }
}

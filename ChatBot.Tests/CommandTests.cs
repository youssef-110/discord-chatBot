using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ChatBot;
using Xunit;

public class CommandTests
{
    private readonly Commands _commands = new(seed: 42);

    [Theory]
    [InlineData("d20", 1, 20)]
    [InlineData("2d6", 2, 12)]
    [InlineData("3d8+2", 5, 26)]
    [InlineData("1d4-1", 0, 3)]
    public void Roll_totals_stay_in_range(string dice, int min, int max)
    {
        for (int i = 0; i < 200; i++)
        {
            string result = _commands.Roll(dice);
            int total = int.Parse(result.Split("**")[1]);
            Assert.InRange(total, min, max);
        }
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("0d6")]
    [InlineData("99d6")]
    [InlineData("2d1")]
    public void Roll_rejects_bad_dice(string dice) =>
        Assert.DoesNotContain("🎲", _commands.Roll(dice));

    [Fact]
    public void Choose_picks_one_of_the_options()
    {
        var options = new[] { "pizza", "tacos", "sushi" };
        for (int i = 0; i < 50; i++)
        {
            string result = _commands.Choose("pizza, tacos or sushi");
            Assert.Contains(options, o => result == $"I choose **{o}**.");
        }
        Assert.StartsWith("Give me at least two", _commands.Choose("pizza"));
    }

    [Fact]
    public void Eight_ball_needs_a_question()
    {
        Assert.Equal("Ask me a yes-or-no question.", _commands.Ask8Ball("  "));
        Assert.StartsWith("🎱", _commands.Ask8Ball("Will it rain?"));
    }

    [Fact]
    public void Long_replies_fit_in_one_discord_message()
    {
        Assert.Equal("short", Commands.FitMessage("short"));
        Assert.Equal(1991, Commands.FitMessage(new string('a', 5000)).Length);
    }

    private sealed class FakeApi : HttpMessageHandler
    {
        public readonly List<JsonObject> Requests = new();
        public HttpStatusCode Status = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject());
            var body = new JsonObject
            {
                ["choices"] = new JsonArray { new JsonObject { ["message"] = new JsonObject { ["content"] = $"reply {Requests.Count}" } } },
            }.ToJsonString();
            return new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task Ai_remembers_the_channel_and_forgets_on_request()
    {
        var api = new FakeApi();
        var ai = new AiChat("key", "openai/gpt-oss-120b", "https://example.test/v1", api);

        Assert.Equal("reply 1", await ai.AskAsync(7, "sam", "hi"));
        await ai.AskAsync(7, "sam", "and then?");
        // system + (user, assistant) from turn 1 + the new user message
        Assert.Equal(4, api.Requests[1]["messages"]!.AsArray().Count);
        Assert.Equal("low", api.Requests[1]["reasoning_effort"]!.GetValue<string>());

        await ai.AskAsync(8, "kim", "other channel");
        Assert.Equal(2, api.Requests[2]["messages"]!.AsArray().Count);

        ai.Forget(7);
        await ai.AskAsync(7, "sam", "fresh start");
        Assert.Equal(2, api.Requests[3]["messages"]!.AsArray().Count);
    }

    [Fact]
    public async Task Ai_rate_limit_gives_a_friendly_message()
    {
        var api = new FakeApi { Status = HttpStatusCode.TooManyRequests };
        var ai = new AiChat("key", "m", "https://example.test/v1", api);
        Assert.StartsWith("I'm being rate limited", await ai.AskAsync(1, "sam", "hi"));
    }
}

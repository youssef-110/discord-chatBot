using Discord;
using Discord.WebSocket;
using ChatBot;

string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Set DISCORD_TOKEN to your bot token. See README.md for setup.");
    return 1;
}

var commands = new Commands();
var ai = AiChat.FromEnvironment();
Console.WriteLine(ai is null
    ? "AI replies off (no AI_API_KEY or GROQ_API_KEY). Fun commands still work."
    : "AI replies on.");

var client = new DiscordSocketClient(new DiscordSocketConfig
{
    // MessageContent is needed to read messages that mention the bot.
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent,
});

client.Log += msg => { Console.WriteLine(msg.ToString()); return Task.CompletedTask; };

client.Ready += async () =>
{
    var slash = new[]
    {
        new SlashCommandBuilder().WithName("ping").WithDescription("Check that the bot is alive"),
        new SlashCommandBuilder().WithName("roll").WithDescription("Roll dice, like 2d6 or d20+3")
            .AddOption("dice", ApplicationCommandOptionType.String, "Dice to roll", isRequired: true),
        new SlashCommandBuilder().WithName("8ball").WithDescription("Ask the magic 8-ball")
            .AddOption("question", ApplicationCommandOptionType.String, "Your yes-or-no question", isRequired: true),
        new SlashCommandBuilder().WithName("choose").WithDescription("Pick one of several options")
            .AddOption("options", ApplicationCommandOptionType.String, "Options separated by commas", isRequired: true),
        new SlashCommandBuilder().WithName("flip").WithDescription("Flip a coin"),
        new SlashCommandBuilder().WithName("ask").WithDescription("Ask the AI a question")
            .AddOption("question", ApplicationCommandOptionType.String, "Your question", isRequired: true),
        new SlashCommandBuilder().WithName("forget").WithDescription("Clear the AI's memory of this channel"),
    };
    // Global commands can take up to an hour to appear the first time; later restarts are instant.
    await client.BulkOverwriteGlobalApplicationCommandsAsync(slash.Select(s => (ApplicationCommandProperties)s.Build()).ToArray());
    Console.WriteLine($"Ready as {client.CurrentUser}. In {client.Guilds.Count} server(s).");
};

client.SlashCommandExecuted += async command =>
{
    string Option(string name) => command.Data.Options.FirstOrDefault(o => o.Name == name)?.Value?.ToString() ?? "";
    switch (command.Data.Name)
    {
        case "ping":
            await command.RespondAsync($"Pong! {client.Latency} ms");
            break;
        case "roll":
            await command.RespondAsync(commands.Roll(Option("dice")));
            break;
        case "8ball":
            await command.RespondAsync($"> {Option("question")}\n{commands.Ask8Ball(Option("question"))}");
            break;
        case "choose":
            await command.RespondAsync(commands.Choose(Option("options")));
            break;
        case "flip":
            await command.RespondAsync(commands.CoinFlip());
            break;
        case "forget":
            ai?.Forget(command.ChannelId ?? 0);
            await command.RespondAsync("Memory for this channel cleared.", ephemeral: true);
            break;
        case "ask":
            if (ai is null) { await command.RespondAsync("AI replies are off on this bot.", ephemeral: true); break; }
            await command.DeferAsync();  // AI can take longer than Discord's 3-second reply window
            string answer = await ai.AskAsync(command.ChannelId ?? 0, command.User.GlobalName ?? command.User.Username, Option("question"));
            await command.FollowupAsync(Commands.FitMessage($"> {Option("question")}\n{answer}"));
            break;
    }
};

// Mention the bot (or DM it) to chat without a slash command.
client.MessageReceived += message =>
{
    if (message.Author.IsBot || ai is null) return Task.CompletedTask;
    bool isDm = message.Channel is IDMChannel;
    bool mentioned = message.MentionedUsers.Any(u => u.Id == client.CurrentUser.Id);
    if (!isDm && !mentioned) return Task.CompletedTask;

    // Run off the gateway thread so a slow AI call doesn't block other events.
    _ = Task.Run(async () =>
    {
        string text = message.Content.Replace($"<@{client.CurrentUser.Id}>", "").Trim();
        if (text.Length == 0) text = "Hi!";
        using (message.Channel.EnterTypingState())
        {
            string reply = await ai.AskAsync(message.Channel.Id, message.Author.GlobalName ?? message.Author.Username, text);
            await message.Channel.SendMessageAsync(Commands.FitMessage(reply), messageReference: new MessageReference(message.Id));
        }
    });
    return Task.CompletedTask;
};

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();
await Task.Delay(Timeout.Infinite);
return 0;

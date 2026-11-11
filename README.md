# Discord Chat Bot

A Discord bot in C# with slash commands and optional AI chat. Mention the bot or DM it
and it answers with an AI model through any OpenAI-compatible API (Groq's free tier by
default), remembering the last few messages in each channel.

## Commands

| Command | What it does |
|---|---|
| `/ask question` | Ask the AI. Follow-ups in the same channel keep context. |
| `@bot message` or a DM | Chat with the AI without a command |
| `/forget` | Clear the AI's memory for this channel |
| `/roll 2d6`, `/roll d20+3` | Roll dice and show each die |
| `/8ball question` | Magic 8-ball |
| `/choose pizza, tacos, sushi` | Pick one option |
| `/flip` | Flip a coin |
| `/ping` | Check the bot is alive, with gateway latency |

The fun commands work without any AI key.

## Setup

1. In the [Discord Developer Portal](https://discord.com/developers/applications), create
   an application and add a **Bot**. Copy its token.
2. On the Bot page, turn on **Message Content Intent** (needed to read messages that
   mention the bot).
3. Under OAuth2, URL Generator, tick `bot` and `applications.commands`, then the
   permissions **Send Messages** and **Read Message History**. Open the URL to add the
   bot to your server.
4. For AI replies, get a free key at [console.groq.com](https://console.groq.com).

## Run

Needs the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
export DISCORD_TOKEN=your-bot-token
export GROQ_API_KEY=your-groq-key        # optional
dotnet run --project ChatBot
```

On Windows PowerShell use `$env:DISCORD_TOKEN = "..."` instead of `export`.

Slash commands are registered globally on startup. The first time, Discord can take up
to an hour to show them; later restarts update them right away.

### Other AI providers

| Variable | Default |
|---|---|
| `AI_API_KEY` | falls back to `GROQ_API_KEY` |
| `AI_BASE_URL` | `https://api.groq.com/openai/v1` |
| `AI_MODEL` | `openai/gpt-oss-120b` |

Any OpenAI-compatible endpoint works, for example OpenRouter or a local Ollama
(`http://localhost:11434/v1`).

## Test

```bash
dotnet test
```

The tests cover dice parsing and ranges, choices, the 8-ball, message length limits,
and the AI client's per-channel memory and rate-limit handling against a fake API.

Keep your bot token secret: anyone with it can control the bot. If it leaks, reset it
in the Developer Portal.

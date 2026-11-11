using System.Text.RegularExpressions;

namespace ChatBot;

/// <summary>Command logic with no Discord types, so it is unit tested.</summary>
public sealed class Commands
{
    private readonly Random _random;

    public Commands(int seed = 0) => _random = seed == 0 ? new Random() : new Random(seed);

    private static readonly string[] EightBall =
    {
        "It is certain.", "Without a doubt.", "You may rely on it.", "Most likely.",
        "Signs point to yes.", "Reply hazy, try again.", "Ask again later.",
        "Better not tell you now.", "Don't count on it.", "My sources say no.",
        "Outlook not so good.", "Very doubtful.",
    };

    /// <summary>Rolls dice written like "2d6", "d20" or "3d8+2".</summary>
    public string Roll(string spec)
    {
        var m = Regex.Match(spec.Trim().ToLowerInvariant(), @"^(\d{0,2})d(\d{1,4})([+-]\d{1,4})?$");
        if (!m.Success) return "Write dice like `2d6`, `d20` or `3d8+2`.";
        int count = m.Groups[1].Value == "" ? 1 : int.Parse(m.Groups[1].Value);
        int sides = int.Parse(m.Groups[2].Value);
        int bonus = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        if (count is < 1 or > 50 || sides < 2) return "Use 1 to 50 dice with at least 2 sides.";

        var rolls = Enumerable.Range(0, count).Select(_ => _random.Next(1, sides + 1)).ToList();
        int total = rolls.Sum() + bonus;
        string detail = count > 1 || bonus != 0
            ? $" ({string.Join(" + ", rolls)}{(bonus != 0 ? (bonus > 0 ? $" + {bonus}" : $" - {-bonus}") : "")})"
            : "";
        return $"🎲 **{total}**{detail}";
    }

    public string Ask8Ball(string question) =>
        string.IsNullOrWhiteSpace(question)
            ? "Ask me a yes-or-no question."
            : $"🎱 {EightBall[_random.Next(EightBall.Length)]}";

    /// <summary>Picks one option from a list split by commas or " or ".</summary>
    public string Choose(string options)
    {
        var list = Regex.Split(options, @",|\s+or\s+", RegexOptions.IgnoreCase)
            .Select(o => o.Trim()).Where(o => o.Length > 0).Distinct().ToList();
        if (list.Count < 2) return "Give me at least two options, like `pizza, tacos, sushi`.";
        return $"I choose **{list[_random.Next(list.Count)]}**.";
    }

    public string CoinFlip() => _random.Next(2) == 0 ? "🪙 Heads" : "🪙 Tails";

    /// <summary>Discord messages max out at 2000 characters.</summary>
    public static string FitMessage(string text) =>
        text.Length <= 2000 ? text : text[..1990] + "…";
}

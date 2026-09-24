using System.Globalization;
using System.Text.RegularExpressions;

namespace FineManagement.Api.Services;

/// <summary>处理多人姓名对应的均分金额显示文本。</summary>
public static partial class AssessmentAmountFormatter
{
    /// <summary>多人记录显示为“总额（每人X元）”，单人记录仅显示总额。</summary>
    public static string Format(decimal amount, string personName)
    {
        var totalText = FormatNumber(amount);
        var personCount = SplitPersonNames(personName).Count;
        if (personCount <= 1) return totalText;

        var amountPerPerson = decimal.Round(amount / personCount, 2, MidpointRounding.AwayFromZero);
        return $"{totalText}（每人{FormatNumber(amountPerPerson)}元）";
    }

    public static IReadOnlyList<string> SplitPersonNames(string personName) =>
        PersonSeparatorRegex()
            .Split(personName.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToArray();

    private static string FormatNumber(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"[、,，;；/／\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex PersonSeparatorRegex();
}

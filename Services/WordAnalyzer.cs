using System.Text.RegularExpressions;
using Task2.Models;

namespace Task2.Services;

public static class WordAnalyzer
{
    public static List<(string Word, int Count)> GetTopWords(IEnumerable<Comment> comments, int topCount)
    {
        return comments
            .SelectMany(comment => SplitWords(comment.Body))
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .GroupBy(word => word)
            .Select(group => (Word: group.Key, Count: group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Word)
            .Take(topCount)
            .ToList();
    }

    private static IEnumerable<string> SplitWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Enumerable.Empty<string>();
        }

        return Regex.Split(text.ToLower(), @"[^a-zA-Z]+")
            .Where(word => !string.IsNullOrWhiteSpace(word));
    }
}

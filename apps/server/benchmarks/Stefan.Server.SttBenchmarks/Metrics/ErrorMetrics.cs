using Fastenshtein;

namespace Stefan.Server.SttBenchmarks.Metrics;

public static class ErrorMetrics
{
    public static double WordErrorRate(string reference, string hypothesis)
    {
        var refWords = NormalizeAndSplit(reference);
        var hypWords = NormalizeAndSplit(hypothesis);

        if (refWords.Length == 0)
            return hypWords.Length == 0 ? 0.0 : 1.0;

        return WordLevenshtein(refWords, hypWords) / (double)refWords.Length;
    }

    public static double CharacterErrorRate(string reference, string hypothesis)
    {
        var refNorm = reference.Trim().ToLowerInvariant();
        var hypNorm = hypothesis.Trim().ToLowerInvariant();

        if (refNorm.Length == 0)
            return hypNorm.Length == 0 ? 0.0 : 1.0;

        var lev = new Levenshtein(refNorm);
        return lev.DistanceFrom(hypNorm) / (double)refNorm.Length;
    }

    private static string[] NormalizeAndSplit(string text)
    {
        return text
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static int WordLevenshtein(string[] refWords, string[] hypWords)
    {
        var n = refWords.Length;
        var m = hypWords.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = refWords[i - 1] == hypWords[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}

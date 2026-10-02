using System.Text.Json;

namespace IntelligentProgrammingPlatform.Services.AI;

public static class StoredHintReader
{
    // Ескі JSON кеңестерін қауіпсіз оқып, жарамсыз мәтінді журналға шығармай бос тізім береді.
    public static string[] Read(string? json, ILogger logger, long feedbackId)
    {
        try
        {
            if (json == null || json.Length > 6000) throw new JsonException();
            var hints = JsonSerializer.Deserialize<string[]>(json, new JsonSerializerOptions { MaxDepth = 8 });
            if (hints == null || hints.Any(hint => string.IsNullOrWhiteSpace(hint) || hint.Length > 300))
                throw new JsonException();
            return hints.Take(3).ToArray();
        }
        catch (JsonException)
        {
            logger.LogWarning("Stored hints are invalid for feedback {FeedbackId}; hints omitted", feedbackId);
            return Array.Empty<string>();
        }
    }
}

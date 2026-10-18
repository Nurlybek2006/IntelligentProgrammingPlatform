using System.Text;

namespace IntelligentProgrammingPlatform.Services.Submissions;

// Бөлімнің аударма кілтін және өзгеріссіз шарт мәтінін бірге ұстайды.
public sealed record TaskStatementSection(string HeadingKey, string Text);

public static class TaskStatementReader
{
    // Тек бөлек тұрған белгілі тақырыптарды бөліп, қалған шарт мәтінін өзгертпей сақтайды.
    public static IReadOnlyList<TaskStatementSection> Read(string description)
    {
        var sections = new List<TaskStatementSection>();
        var heading = "Workspace_Description";
        var text = new StringBuilder();
        using var reader = new StringReader(description);
        while (reader.ReadLine() is { } line)
        {
            var next = line.Trim().TrimEnd(':').ToLowerInvariant() switch
            {
                "input" or "input format" or "кіріс пішімі" or "входные данные" => "Workspace_Input",
                "output" or "output format" or "шығыс пішімі" or "выходные данные" => "Workspace_Output",
                "constraints" or "шектеулер" or "ограничения" => "Workspace_Constraints",
                _ => null
            };
            if (next == null) { text.AppendLine(line); continue; }
            if (text.Length > 0) sections.Add(new(heading, text.ToString().TrimEnd()));
            text.Clear(); heading = next;
        }
        if (text.Length > 0) sections.Add(new(heading, text.ToString().TrimEnd()));
        return sections;
    }
}

from pathlib import Path
import re

# Fix CustomSystemFeatures
p = Path(r"c:\Solution\FactuTrust - Copy\src\Backend\FactuTrust.Application\Features\Studio\Systems\CustomSystemFeatures.cs")
text = p.read_text(encoding="utf-8")
text, n = re.subn(
    r'return Result\.Failure<CustomSystemDetailDto>\(Error\.NotFound\("CustomSystem", request\.Key\)\);',
    'return Result.Failure<CustomSystemDetailDto>(new Error("CustomSystem.NotFound", $"CustomSystem with key \'{request.Key}\' was not found."));',
    text,
    count=1,
)
print("CustomSystemFeatures replacements:", n)
p.write_text(text, encoding="utf-8", newline="\n")

# Fix SendChatMessageCommand
p2 = Path(r"c:\Solution\FactuTrust - Copy\src\Backend\FactuTrust.Application\Features\AI\Commands\SendChatMessageCommand.cs")
text2 = p2.read_text(encoding="utf-8")
pattern = re.compile(
    r"    private static IEnumerable<ChatStreamEvent> TryBuildStudioProgressEvents\(string toolData\)\r?\n"
    r"    \{\r?\n"
    r"        try\r?\n"
    r"        \{\r?\n"
    r"            using var doc = JsonDocument\.Parse\(toolData\);\r?\n"
    r"            if \(!doc\.RootElement\.TryGetProperty\(\"buildSteps\", out var steps\) \|\| steps\.ValueKind != JsonValueKind\.Array\)\r?\n"
    r"                yield break;\r?\n"
    r"            foreach \(var step in steps\.EnumerateArray\(\)\)\r?\n"
    r"                yield return ChatStreamEvent\.StudioProgressEvent\(step\.GetRawText\(\)\);\r?\n"
    r"        \}\r?\n"
    r"        catch \(JsonException\)\r?\n"
    r"        \{\r?\n"
    r"            yield break;\r?\n"
    r"        \}\r?\n"
    r"    \}",
    re.MULTILINE,
)
replacement = """    private static IEnumerable<ChatStreamEvent> TryBuildStudioProgressEvents(string toolData)
    {
        var events = new List<ChatStreamEvent>();
        try
        {
            using var doc = JsonDocument.Parse(toolData);
            if (!doc.RootElement.TryGetProperty("buildSteps", out var steps) || steps.ValueKind != JsonValueKind.Array)
                return events;
            foreach (var step in steps.EnumerateArray())
                events.Add(ChatStreamEvent.StudioProgressEvent(step.GetRawText()));
        }
        catch (JsonException)
        {
            // ignore malformed tool payloads
        }

        return events;
    }"""
text2, n2 = pattern.subn(replacement, text2, count=1)
print("SendChatMessage replacements:", n2)
if n2 == 0:
    idx = text2.find("TryBuildStudioProgressEvents")
    print(text2[idx:idx+500])
p2.write_text(text2, encoding="utf-8", newline="\n")

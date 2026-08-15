using FactuTrust.Application.Features.AI;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>Contextes d'exécution d'outils Cursor, corrélés par runId + jeton (loopback).</summary>
public sealed class CursorToolRunRegistry : ICursorToolRunRegistry
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, CursorToolRunContext> _runs = new();

    public void Register(CursorToolRunContext context) => _runs[context.RunId] = context;

    public bool TryGet(Guid runId, out CursorToolRunContext context) =>
        _runs.TryGetValue(runId, out context!);

    public void Complete(Guid runId) => _runs.TryRemove(runId, out _);

    public static string CreateToken() =>
        Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());

    public static bool TokenEquals(string expected, string? actual)
    {
        if (string.IsNullOrEmpty(actual) || expected.Length != actual.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < expected.Length; i++)
            diff |= expected[i] ^ actual[i];
        return diff == 0;
    }
}

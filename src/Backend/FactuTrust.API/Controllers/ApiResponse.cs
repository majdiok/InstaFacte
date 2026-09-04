namespace FactuTrust.API.Controllers;

/// <summary>
/// Enveloppe de réponse JSON de fait pour l'API FactuTrust.
/// Masque volontairement <see cref="FactuTrust.Application.DTOs.ApiResponse{T}"/> pour les
/// contrôleurs déclarés dans ce namespace ou ses namespaces enfants (.Projects, .Studio, .Honoraires).
/// Contrat wire : <c>success</c>, <c>data</c>, <c>message</c>, <c>error</c> (singulier).
/// </summary>
public sealed record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    /// <summary>
    /// Note : le paramètre <paramref name="code"/> est accepté pour compatibilité d'appel mais
    /// n'est pas sérialisé — ce type n'expose pas de propriété <c>Code</c>.
    /// </summary>
    public static ApiResponse<T> Fail(string error, string? code = null) => new()
    {
        Success = false,
        Error = error
    };
}

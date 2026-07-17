using System.Security.Cryptography;
using System.Text;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Hachage SHA-256 des refresh tokens avant stockage en base master.
/// Le token en clair n'est restitué qu'au client ; une fuite en lecture de la base
/// ne donne plus de tokens directement réutilisables. Pas de sel nécessaire :
/// les tokens sont 64 octets aléatoires (entropie non attaquable par dictionnaire).
/// </summary>
public static class RefreshTokenHasher
{
    public static string Hash(string refreshToken)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}

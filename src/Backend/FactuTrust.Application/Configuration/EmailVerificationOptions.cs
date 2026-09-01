namespace FactuTrust.Application.Configuration;

/// <summary>
/// Vérification d'email post-inscription (plan §1.6, décision D3 — "mode doux"). Quand
/// <see cref="Enabled"/> est <c>false</c> (valeur par défaut), le comportement historique est
/// inchangé : <c>AuthController.Register</c> continue de créer le compte avec
/// <c>EmailConfirmed = true</c> et aucune vérification n'est requise pour se connecter.
///
/// Quand <see cref="Enabled"/> est <c>true</c> : les nouveaux comptes sont créés avec
/// <c>EmailConfirmed = false</c> et un email de vérification est envoyé automatiquement à
/// l'inscription ; <c>POST /api/auth/verify-email</c> et <c>POST /api/auth/resend-verification</c>
/// deviennent utilisables. Le mode reste "doux" : la connexion n'est PAS bloquée pour un email non
/// vérifié (voir <see cref="BlockLoginIfUnverified"/>) tant que ce second flag n'est pas activé
/// séparément — ce qui permet d'activer l'envoi d'emails et d'observer le taux de vérification
/// avant de rendre la vérification obligatoire.
/// </summary>
public sealed class EmailVerificationOptions
{
    public const string SectionName = "Features:EmailVerification";

    /// <summary>Active l'envoi de l'email de vérification et les endpoints associés.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Quand <c>true</c> (et <see cref="Enabled"/> également <c>true</c>), <c>POST /api/auth/login</c>
    /// refuse la connexion tant que l'email n'est pas confirmé. Par défaut <c>false</c> : la
    /// vérification est envoyée et suivie, mais ne bloque personne (mode doux, D3).
    /// </summary>
    public bool BlockLoginIfUnverified { get; set; } = false;
}

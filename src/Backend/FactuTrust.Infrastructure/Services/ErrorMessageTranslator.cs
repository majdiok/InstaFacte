using System.Text.RegularExpressions;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Centralized service for translating error messages to French.
/// </summary>
public static class ErrorMessageTranslator
{
    /// <summary>
    /// Translates a generic error message to French.
    /// </summary>
    public static string TranslateToFrench(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var lowerMessage = message.ToLowerInvariant();

        // Database and connection errors
        if (lowerMessage.Contains("connection") && lowerMessage.Contains("timeout"))
            return "La connexion à la base de données a expiré. Veuillez réessayer.";

        if (lowerMessage.Contains("cannot open database") || lowerMessage.Contains("database") && lowerMessage.Contains("not found"))
            return "La base de données est inaccessible. Veuillez contacter le support technique.";

        if (lowerMessage.Contains("foreign key") || lowerMessage.Contains("constraint"))
            return "Une erreur de référence s'est produite. Les données sont peut-être en cours d'utilisation.";

        if (lowerMessage.Contains("duplicate key") || lowerMessage.Contains("unique constraint"))
            return "Cette valeur existe déjà dans la base de données.";

        // Role and permission errors
        if (lowerMessage.Contains("role") && lowerMessage.Contains("does not exist"))
        {
            var roleMatch = Regex.Match(message, @"Role\s+(\w+)\s+does not exist", RegexOptions.IgnoreCase);
            if (roleMatch.Success)
            {
                var roleName = roleMatch.Groups[1].Value;
                return $"Le rôle '{roleName}' n'existe pas. Veuillez contacter l'administrateur.";
            }
            return "Le rôle spécifié n'existe pas. Veuillez contacter l'administrateur.";
        }

        if (lowerMessage.Contains("permission denied") || lowerMessage.Contains("access denied"))
            return "Vous n'avez pas les permissions nécessaires pour effectuer cette action.";

        // Service and dependency errors
        if (lowerMessage.Contains("cannot resolve scoped service") || lowerMessage.Contains("service not registered"))
            return "Une erreur de configuration s'est produite. Veuillez contacter le support technique.";

        if (lowerMessage.Contains("sqlserverretryingexecutionstrategy") ||
            lowerMessage.Contains("user-initiated transactions"))
            return "Erreur technique temporaire. Veuillez réessayer ; si le problème persiste, contactez le support.";

        // Validation errors
        if (lowerMessage.Contains("invalid") && lowerMessage.Contains("password"))
            return "Le mot de passe ne respecte pas les critères requis.";

        if (lowerMessage.Contains("required") && lowerMessage.Contains("field"))
            return "Un champ obligatoire est manquant.";

        // Routing errors (e.g. CreatedAtRoute with missing route name)
        if (lowerMessage.Contains("no route matches") || lowerMessage.Contains("supplied values"))
            return "Erreur technique lors de la génération de la réponse. L'opération a peut-être réussi.";

        // Not found errors
        if (lowerMessage.Contains("not found") || lowerMessage.Contains("does not exist"))
        {
            // Try to extract entity name
            var entityMatch = Regex.Match(message, @"(\w+)\s+(not found|does not exist)", RegexOptions.IgnoreCase);
            if (entityMatch.Success)
            {
                var entityName = TranslateEntityName(entityMatch.Groups[1].Value);
                return $"{entityName} introuvable.";
            }
            return "L'élément demandé n'a pas été trouvé.";
        }

        // Already exists errors
        if (lowerMessage.Contains("already exists") || lowerMessage.Contains("already taken") || lowerMessage.Contains("duplicate"))
        {
            if (lowerMessage.Contains("username") || lowerMessage.Contains("user name"))
            {
                var valueMatch = Regex.Match(message, @"['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    return $"Le nom d'utilisateur '{valueMatch.Groups[1].Value}' est déjà utilisé.";
                return "Ce nom d'utilisateur est déjà utilisé.";
            }
            if (lowerMessage.Contains("email"))
            {
                var valueMatch = Regex.Match(message, @"['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    return $"L'adresse email '{valueMatch.Groups[1].Value}' est déjà utilisée.";
                return "Cette adresse email est déjà utilisée.";
            }
            return "Cette valeur existe déjà.";
        }

        // File and resource errors
        if (lowerMessage.Contains("file not found") || lowerMessage.Contains("could not load file"))
            return "Le fichier demandé est introuvable.";

        if (lowerMessage.Contains("unauthorized") || lowerMessage.Contains("authentication failed"))
            return "Authentification échouée. Veuillez vérifier vos identifiants.";

        // Generic errors
        if (lowerMessage.Contains("internal server error") || lowerMessage.Contains("an error occurred"))
            return "Une erreur interne s'est produite. Veuillez réessayer ou contacter le support.";

        // Return original message if no translation found
        return message;
    }

    private static string TranslateEntityName(string entityName)
    {
        return entityName.ToLowerInvariant() switch
        {
            "user" => "Utilisateur",
            "tenant" => "Entreprise",
            "invoice" => "Facture",
            "client" => "Client",
            "product" => "Produit",
            "draft" => "Brouillon",
            "role" => "Rôle",
            "subscription" => "Abonnement",
            "payment" => "Paiement",
            _ => entityName
        };
    }
}

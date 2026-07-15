namespace FactuTrust.Application.Features.Channels.Bridge;

/// <summary>
/// Résolution du dossier du pont WhatsApp (bridge.js + node_modules). Pur (le test d'existence de
/// node_modules est injecté en prédicat) donc testable sans disque.
///
/// Priorités :
/// <list type="number">
///   <item>dossier configuré explicitement (<c>Channels:BridgeDirectory</c>) — gagne toujours ;</item>
///   <item>dossier de sortie du build s'il possède node_modules (déploiement standard : `npm ci` fait) ;</item>
///   <item>dossier source (baké au build) s'il possède node_modules — cas DEV : le build ne copie pas
///     node_modules (Chromium lourd) mais la machine de build est la machine d'exécution ;</item>
///   <item>repli : dossier de sortie (l'hôte signalera « dépendances manquantes » avec la marche à suivre).</item>
/// </list>
/// </summary>
public static class BridgeDirectoryResolver
{
    public static string Resolve(
        string? configuredDirectory,
        string outputDirectory,
        string? devSourceDirectory,
        Func<string, bool> hasNodeModules)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
            return configuredDirectory;

        if (hasNodeModules(outputDirectory))
            return outputDirectory;

        if (!string.IsNullOrWhiteSpace(devSourceDirectory) && hasNodeModules(devSourceDirectory))
            return devSourceDirectory;

        return outputDirectory;
    }
}

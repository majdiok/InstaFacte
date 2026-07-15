using FactuTrust.Application.Features.Channels.Bridge;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Channels;

/// <summary>
/// Résolution du dossier du pont WhatsApp : priorité configuré &gt; sortie (node_modules) &gt; source
/// dev (node_modules) &gt; repli sortie. Pur : le test d'existence de node_modules est un prédicat
/// injecté, donc aucun accès disque.
/// </summary>
public sealed class BridgeDirectoryResolverTests
{
    private const string Configured = @"D:\custom\bridge";
    private const string Output = @"C:\app\bin\WhatsAppBridge";
    private const string DevSource = @"C:\src\FactuTrust.API\WhatsAppBridge";

    [Fact]
    public void ConfiguredDirectory_AlwaysWins_EvenWithoutNodeModules()
    {
        var result = BridgeDirectoryResolver.Resolve(Configured, Output, DevSource, _ => false);

        Assert.Equal(Configured, result);
    }

    [Fact]
    public void OutputWithNodeModules_IsPreferredOverSource()
    {
        // Déploiement standard : `npm ci` a été fait dans le dossier de sortie.
        var result = BridgeDirectoryResolver.Resolve(null, Output, DevSource, _ => true);

        Assert.Equal(Output, result);
    }

    [Fact]
    public void DevFallback_UsesSource_WhenOnlySourceHasNodeModules()
    {
        // Cas DEV qui corrige l'écran : le build ne copie pas node_modules, mais le dossier source l'a.
        var result = BridgeDirectoryResolver.Resolve(null, Output, DevSource, dir => dir == DevSource);

        Assert.Equal(DevSource, result);
    }

    [Fact]
    public void NoNodeModulesAnywhere_FallsBackToOutput()
    {
        // L'hôte affichera « dépendances manquantes » avec ce chemin.
        var result = BridgeDirectoryResolver.Resolve(null, Output, DevSource, _ => false);

        Assert.Equal(Output, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingDevSource_IsTolerated_AndFallsBackToOutput(string? devSource)
    {
        // Binaire publié sur une autre machine : le chemin source baké n'existe pas.
        var result = BridgeDirectoryResolver.Resolve(null, Output, devSource, _ => false);

        Assert.Equal(Output, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankConfiguredDirectory_IsIgnored_AutomaticResolutionApplies(string? configured)
    {
        var result = BridgeDirectoryResolver.Resolve(configured, Output, DevSource, _ => true);

        Assert.Equal(Output, result);
    }
}

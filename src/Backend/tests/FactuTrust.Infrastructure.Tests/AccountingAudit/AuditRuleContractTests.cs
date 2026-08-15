using System.Reflection;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AccountingAudit;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AccountingAudit;

/// <summary>
/// Contrat que toute règle d'audit doit respecter. Ces tests sont des garde-fous d'architecture :
/// ils échouent quand une règle nouvellement écrite dévie, avant que la déviation n'atteigne la
/// production.
/// </summary>
public sealed class AuditRuleContractTests
{
    private static IReadOnlyList<Type> AllRuleTypes() =>
        typeof(AccountingAuditRuleBase).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(IAccountingAuditRule).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();

    private static IReadOnlyList<IAccountingAuditRule> InstantiateAll() =>
        AllRuleTypes()
            .Select(t => (IAccountingAuditRule)Activator.CreateInstance(t)!)
            .ToList();

    /// <summary>
    /// <b>Invariant central du balayage multi-dossiers.</b> Une règle ne doit lire que par
    /// <c>ctx.Db</c>, seul contexte garanti pointer sur le dossier examiné. Injecter un repository
    /// ferait lire le tenant ambiant — celui du cabinet lors d'un balayage de portefeuille — donc
    /// les données d'un autre dossier. Un constructeur sans paramètre est la façon la plus simple
    /// de rendre la faute impossible.
    /// </summary>
    [Fact]
    public void No_rule_takes_a_constructor_dependency()
    {
        var offenders = AllRuleTypes()
            .Where(t => t.GetConstructors().All(c => c.GetParameters().Length > 0))
            .Select(t => t.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Ces règles injectent une dépendance et liraient le tenant ambiant au lieu du dossier " +
            $"balayé — passer par ctx.Db : {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Rule_codes_are_unique()
    {
        var duplicates = InstantiateAll()
            .GroupBy(r => r.Code, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Codes de règle en doublon : {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Rule_module_codes_exist_in_the_catalogue()
    {
        var known = AccountingAuditModuleCatalog.All.Select(m => m.Code).ToHashSet(StringComparer.Ordinal);

        var unknown = InstantiateAll()
            .Where(r => !known.Contains(r.ModuleCode))
            .Select(r => $"{r.Code} → {r.ModuleCode}")
            .ToList();

        Assert.True(
            unknown.Count == 0,
            "Ces règles pointent un module absent de AccountingAuditModuleCatalog : la pastille de " +
            $"filtre serait sans libellé dans l'écran : {string.Join(", ", unknown)}");
    }

    [Fact]
    public void Rule_severities_and_categories_are_within_range()
    {
        foreach (var rule in InstantiateAll())
        {
            Assert.InRange(rule.DefaultSeverity, (int)PreClosingSeverity.Info, (int)PreClosingSeverity.Blocking);
            Assert.True(
                Enum.IsDefined(typeof(AnomalyCategory), rule.Category),
                $"Catégorie inconnue sur la règle {rule.Code} : {rule.Category}");
        }
    }

    /// <summary>
    /// Toute règle doit être enregistrée dans le conteneur, sinon elle n'est jamais évaluée : le
    /// symptôme serait un silence, pas une erreur.
    /// </summary>
    [Fact]
    public void Every_rule_type_is_registered_in_dependency_injection()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        typeof(FactuTrust.Infrastructure.DependencyInjection)
            .GetMethod("RegisterAccountingAuditServices", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [services]);

        var registered = services
            .Where(d => d.ServiceType == typeof(IAccountingAuditRule))
            .Select(d => d.ImplementationType!)
            .ToHashSet();

        var missing = AllRuleTypes().Where(t => !registered.Contains(t)).Select(t => t.Name).ToList();

        Assert.True(
            missing.Count == 0,
            $"Règles jamais évaluées faute d'enregistrement DI : {string.Join(", ", missing)}");
    }

    /// <summary>
    /// L'empreinte sans discriminant doit rester exactement celle d'avant l'ajout du paramètre :
    /// la changer aurait auto-résolu puis recréé toutes les anomalies déjà en base.
    /// </summary>
    [Fact]
    public void Fingerprint_without_discriminator_matches_the_legacy_hash()
    {
        // Reproduit exactement l'appel historique de SingleGroup, y compris le formatage des dates :
        // DateOnly.ToString() suit la culture du serveur, l'empreinte en dépend donc aussi.
        var legacy = AuditFingerprint.Build(
            "unlettered",
            "4111",
            new DateOnly(2026, 1, 1).ToString(),
            new DateOnly(2026, 12, 31).ToString());

        Assert.Equal(legacy, FingerprintProbe.WithoutDiscriminator());
    }

    [Fact]
    public void Fingerprint_with_discriminator_separates_two_anomalies_of_the_same_rule()
    {
        var supplierA = FingerprintProbe.WithDiscriminator("FOURNISSEUR-A");
        var supplierB = FingerprintProbe.WithDiscriminator("FOURNISSEUR-B");
        var none = FingerprintProbe.WithoutDiscriminator();

        Assert.NotEqual(supplierA, supplierB);
        Assert.NotEqual(supplierA, none);
    }

    [Fact]
    public void Fingerprint_with_discriminator_is_stable_across_runs()
    {
        Assert.Equal(
            FingerprintProbe.WithDiscriminator("FOURNISSEUR-A"),
            FingerprintProbe.WithDiscriminator("FOURNISSEUR-A"));
    }

    /// <summary>Expose les fabriques protégées de <see cref="AccountingAuditRuleBase"/>.</summary>
    private sealed class FingerprintProbe : AccountingAuditRuleBase
    {
        public override string Code => "unlettered";
        public override string ModuleCode => "lettering";
        public override int Category => (int)AnomalyCategory.Lettrage;
        public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

        public override Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
            IAuditEvaluationContext ctx, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AnomalyCandidate>>(Array.Empty<AnomalyCandidate>());

        public static string WithoutDiscriminator() => Build(discriminator: null);

        public static string WithDiscriminator(string discriminator) => Build(discriminator);

        private static string Build(string? discriminator) => SingleGroup(
            "unlettered", "lettering", (int)AnomalyCategory.Lettrage, (int)PreClosingSeverity.Warning,
            "titre", "description", "impact",
            accountRef: "4111",
            amount: 0m,
            periodFrom: new DateOnly(2026, 1, 1),
            periodTo: new DateOnly(2026, 12, 31),
            lines: Array.Empty<AnomalyLineCandidate>(),
            recommendations: Array.Empty<string>(),
            deepLinkRoute: null,
            discriminator: discriminator).Fingerprint;
    }
}

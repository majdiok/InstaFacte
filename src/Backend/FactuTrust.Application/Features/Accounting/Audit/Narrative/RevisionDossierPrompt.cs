using System.Globalization;
using System.Text;

namespace FactuTrust.Application.Features.Accounting.Audit.Narrative;

/// <summary>
/// Prompts du dossier de révision.
///
/// <para>Le modèle reçoit <b>uniquement</b> des anomalies déjà détectées, jamais les données brutes
/// du dossier : il n'a rien à chercher, rien à recouper, rien à déduire. Sa tâche est d'écrire ce
/// qu'un collaborateur écrirait sur une note de travail, à partir d'un constat qui lui est
/// donné.</para>
///
/// <para>Les règles du prompt sont doublées côté code par <see cref="RevisionDossierGuard"/> : un
/// prompt bien écrit réduit les écarts, il ne les empêche pas. Ce qui protège réellement, c'est la
/// vérification en sortie.</para>
/// </summary>
public static class RevisionDossierPrompt
{
    public const string System = """
        Tu es collaborateur confirmé dans un cabinet d'expertise comptable en Tunisie. Tu rédiges le
        dossier de révision d'un exercice, à partir d'anomalies DÉJÀ détectées et vérifiées par le
        système.

        RÈGLES ABSOLUES :
        1. Tu ne détectes rien. Chaque anomalie t'est donnée : tu la mets en forme, tu ne la juges
           pas et tu n'en inventes aucune autre.
        2. Tu n'écris JAMAIS de montant, de taux, de numéro de compte, de date ni de comptage. Ces
           valeurs sont ajoutées par le système après ta rédaction. Si tu as besoin d'y faire
           référence, écris « le montant en jeu », « le compte concerné », « la période visée ».
        3. Le champ « anomalyRef » doit reprendre À L'IDENTIQUE une référence de la liste fournie.
           Une référence inventée fait rejeter l'entrée.
        4. Le champ « action » doit être exactement l'une des valeurs autorisées. Aucune autre.
        5. Tu n'affirmes jamais une fraude, une intention ni une responsabilité individuelle. Tu
           décris un constat et ce qu'il reste à vérifier.

        STYLE :
        - Note de travail professionnelle : phrases courtes, ton factuel, pas de formule de
          politesse, pas de titre, pas de liste à puces.
        - « workingNote » : deux à quatre phrases. Ce que le constat implique, et ce qu'il faut
          vérifier ou obtenir.
        - « clientQuestion » : la question à poser au client, si elle a lieu d'être. Sinon, laisse
          le champ vide.
        - « executiveSummary » : trois à cinq phrases sur l'état général du dossier et les priorités.

        Réponds UNIQUEMENT par un objet JSON conforme au schéma. Aucun texte avant ou après.
        """;

    /// <summary>
    /// Prompt utilisateur : la liste des anomalies, sans aucune donnée du dossier au-delà de ce que
    /// l'anomalie porte déjà.
    ///
    /// <para>Les montants apparaissent car ils aident le modèle à hiérarchiser sa prose — mais toute
    /// valeur qu'il recopierait est ignorée à la lecture : la note finale reprend les faits, pas le
    /// texte du modèle.</para>
    /// </summary>
    public static string BuildUserPrompt(
        string companyName,
        int fiscalYear,
        IReadOnlyList<RevisionAnomalyFacts> anomalies)
    {
        var sb = new StringBuilder();
        sb.Append("Dossier : ").AppendLine(string.IsNullOrWhiteSpace(companyName) ? "Société" : companyName);
        sb.Append("Exercice : ").Append(fiscalYear.ToString(CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("Anomalies à traiter : ").Append(anomalies.Count).AppendLine();
        sb.AppendLine();

        foreach (var anomaly in anomalies)
        {
            sb.Append("- anomalyRef: ").AppendLine(anomaly.Id.ToString("N"));
            sb.Append("  domaine: ").AppendLine(AccountingAuditModuleLabels.LabelFor(anomaly.ModuleCode));
            sb.Append("  gravité: ").AppendLine(SeverityLabel(anomaly.Severity));
            sb.Append("  constat: ").AppendLine(Flatten(anomaly.Title));
            sb.Append("  détail: ").AppendLine(Flatten(anomaly.Description));

            if (anomaly.Recommendations.Count > 0)
                sb.Append("  pistes: ").AppendLine(Flatten(string.Join(" / ", anomaly.Recommendations)));

            sb.Append("  action attendue: ").AppendLine(RevisionAction.DefaultFor(anomaly.RuleCode));
            sb.AppendLine();
        }

        sb.AppendLine("Rédige une entrée par anomalie, en reprenant exactement chaque anomalyRef.");
        return sb.ToString();
    }

    private static string SeverityLabel(int severity) => severity switch
    {
        2 => "bloquante",
        1 => "avertissement",
        _ => "information"
    };

    /// <summary>
    /// Aplatit les sauts de ligne : le prompt est structuré en lignes « clé: valeur », un retour
    /// chariot dans une description casserait ce découpage et brouillerait la lecture du modèle.
    /// </summary>
    private static string Flatten(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "—"
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}

/// <summary>
/// Libellés des domaines de contrôle, côté Application.
///
/// <para>Le catalogue de référence vit dans l'Infrastructure (<c>AccountingAuditModuleCatalog</c>),
/// que l'Application ne peut pas atteindre. Cette table sert au seul prompt : un domaine inconnu
/// retombe sur son code, ce qui reste lisible et n'empêche jamais la rédaction.</para>
/// </summary>
internal static class AccountingAuditModuleLabels
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["integrity"] = "Intégrité",
        ["reconciliation"] = "Rapprochements",
        ["balance-analysis"] = "Analyse des soldes",
        ["vat"] = "TVA",
        ["lettering"] = "Lettrage",
        ["audit-trail"] = "Piste d'audit",
        ["suspense"] = "Comptes d'attente",
        ["analytic"] = "Analytique",
        ["fiscal"] = "Fiscalité",
        ["fixed-assets"] = "Immobilisations",
        ["budget"] = "Budgétaire",
        ["liasse"] = "Liasse fiscale",
        ["bank"] = "Banque",
        ["purchases"] = "Achats",
        ["sales"] = "Ventes",
        ["payroll"] = "Paie",
        ["treasury"] = "Trésorerie",
        ["documents"] = "Documents"
    };

    public static string LabelFor(string? moduleCode) =>
        moduleCode is not null && Labels.TryGetValue(moduleCode, out var label) ? label : moduleCode ?? "—";
}

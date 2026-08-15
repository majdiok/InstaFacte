namespace FactuTrust.Application.Features.Accounting.Audit.Narrative;

/// <summary>
/// Ensemble <b>fermé</b> des actions qu'un dossier de révision peut recommander.
///
/// <para><b>Pourquoi un ensemble fermé.</b> C'est le seul champ du dossier de révision où le modèle
/// de langage exerce un choix. Le borner à huit valeurs connues rend ce choix vérifiable : toute
/// autre valeur est rejetée à la lecture, et l'entrée retombe sur l'action déterministe. Un modèle
/// ne peut donc pas inventer une consigne — « payer le fournisseur », « ignorer », « corriger la
/// déclaration déposée » — qui engagerait le cabinet.</para>
///
/// <para>Les libellés français sont figés ici et non générés : c'est ce qui garantit qu'un même
/// code produit toujours le même intitulé dans l'écran, le PDF et l'export.</para>
/// </summary>
public static class RevisionAction
{
    public const string Reverse = "extourner";
    public const string RequestInvoice = "demander_la_facture";
    public const string Reclassify = "reclasser";
    public const string Letter = "lettrer";
    public const string AttachProof = "joindre_piece";
    public const string FixDeclaration = "regulariser_declaration";
    public const string CheckContract = "verifier_contrat";
    public const string None = "aucune";

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [Reverse] = "Extourner l'écriture",
        [RequestInvoice] = "Demander la facture au tiers",
        [Reclassify] = "Reclasser l'imputation",
        [Letter] = "Lettrer les écritures",
        [AttachProof] = "Joindre la pièce justificative",
        [FixDeclaration] = "Régulariser la déclaration",
        [CheckContract] = "Vérifier le contrat ou le paramétrage",
        [None] = "Aucune action requise"
    };

    /// <summary>Toutes les valeurs admises, dans l'ordre d'écriture du schéma transmis au modèle.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Reverse, RequestInvoice, Reclassify, Letter,
        AttachProof, FixDeclaration, CheckContract, None
    ];

    public static bool IsValid(string? action) =>
        !string.IsNullOrWhiteSpace(action) && Labels.ContainsKey(action);

    public static string LabelFor(string? action) =>
        action is not null && Labels.TryGetValue(action, out var label) ? label : Labels[None];

    /// <summary>
    /// Action déterministe par défaut d'une règle. Sert de repli quand le modèle est indisponible
    /// ou que sa réponse est écartée : le dossier reste exploitable sans lui.
    /// </summary>
    public static string DefaultFor(string? ruleCode) => ruleCode switch
    {
        "drafts" or "unbalanced" or "reversal-missing" => Reverse,

        "entry-missing-attachment" or "vat-deductible-no-proof"
            or "supplier-invoice-no-proof" => RequestInvoice,

        "thirdparty-account-mismatch" or "health-thirdparty-mislink"
            or "health-orphan-accounts" or "suspense" => Reclassify,

        "unlettered" or "lettering-orphan" or "recon-bank-incomplete" => Letter,

        "vat" or "entry-vat-vs-document" or "vat-period-not-closed"
            or "withholding-missing-on-fees" or "fodec-missing" => FixDeclaration,

        "supplier-invoice-duplicate" => Reverse,

        "purchase-price-drift" => CheckContract,

        // Trésorerie : l'encaissement orphelin se rattache et se lettre ; la caisse créditrice
        // se règle en retrouvant l'écriture manquante, pas en corrigeant un solde.
        "cash-in-without-invoice" => Letter,
        "cash-negative" => Reclassify,

        // Paie : toutes ces anomalies naissent d'un paramétrage — contrat, régime, taux.
        "payroll-cnss-regime-mismatch" or "payroll-overtime-out-of-regime"
            or "payroll-below-smig" => CheckContract,
        "payroll-dependent-no-proof" => RequestInvoice,

        // Fraude douce : rien à corriger mécaniquement — il faut une pièce ou une explication.
        // Aucune de ces règles n'affirme la fraude, l'action reste une demande de justification.
        "threshold-structuring" or "supplier-created-then-paid" => RequestInvoice,
        "self-validation" or "off-hours-entry" or "backdated-entry" => None,

        _ => None
    };
}

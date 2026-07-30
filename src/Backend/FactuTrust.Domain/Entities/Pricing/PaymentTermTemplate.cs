using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Pricing;

/// <summary>
/// Condition de règlement structurée : délai, mode de calcul de l'échéance, et escompte
/// éventuel pour paiement anticipé.
///
/// <b>Coexistence avec le champ texte.</b> Les documents portent aujourd'hui une chaîne libre
/// <c>PaymentTerms</c>. Elle n'est PAS supprimée : les documents déjà émis la portent, et elle
/// reste ce qui s'imprime. Ce modèle sert à la produire et à calculer une échéance exploitable ;
/// la migration se fait document par document, sans rupture.
/// </summary>
public sealed class PaymentTermTemplate : AggregateRoot
{
    public string Name { get; private set; } = null!;

    /// <summary>Nombre de jours accordés au client.</summary>
    public int DelayDays { get; private set; }

    public PaymentDueMode DueMode { get; private set; }

    /// <summary>
    /// Jour de règlement imposé pour les modes « fin de mois le N ». Ignoré autrement.
    /// </summary>
    public int? DueDayOfMonth { get; private set; }

    /// <summary>Taux d'escompte pour paiement anticipé, en pourcentage. Nul = pas d'escompte.</summary>
    public decimal? EarlyPaymentDiscountPercent { get; private set; }

    /// <summary>Nombre de jours sous lequel l'escompte s'applique.</summary>
    public int? EarlyPaymentDays { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Condition proposée par défaut à la création d'un document.</summary>
    public bool IsDefault { get; private set; }

    private PaymentTermTemplate() { }

    public static Result<PaymentTermTemplate> Create(
        string? name,
        int delayDays,
        PaymentDueMode dueMode = PaymentDueMode.NetDays,
        int? dueDayOfMonth = null,
        decimal? earlyPaymentDiscountPercent = null,
        int? earlyPaymentDays = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<PaymentTermTemplate>(Error.Validation("Name", "Le libellé est obligatoire"));

        if (name.Trim().Length > 100)
            return Result.Failure<PaymentTermTemplate>(Error.Validation("Name", "Le libellé ne peut pas dépasser 100 caractères"));

        var validation = Validate(delayDays, dueMode, dueDayOfMonth, earlyPaymentDiscountPercent, earlyPaymentDays);
        if (validation.IsFailure)
            return Result.Failure<PaymentTermTemplate>(validation.Error);

        return Result.Success(new PaymentTermTemplate
        {
            Name = name.Trim(),
            DelayDays = delayDays,
            DueMode = dueMode,
            DueDayOfMonth = dueMode == PaymentDueMode.EndOfMonthOnDay ? dueDayOfMonth : null,
            EarlyPaymentDiscountPercent = earlyPaymentDiscountPercent,
            EarlyPaymentDays = earlyPaymentDiscountPercent.HasValue ? earlyPaymentDays : null,
            IsActive = true
        });
    }

    private static Result Validate(
        int delayDays, PaymentDueMode dueMode, int? dueDayOfMonth,
        decimal? discountPercent, int? discountDays)
    {
        if (delayDays < 0)
            return Result.Failure(Error.Validation("DelayDays", "Le délai ne peut pas être négatif"));

        if (delayDays > 365)
            return Result.Failure(Error.Validation("DelayDays", "Le délai ne peut pas dépasser 365 jours"));

        if (dueMode == PaymentDueMode.EndOfMonthOnDay && dueDayOfMonth is null or < 1 or > 31)
            return Result.Failure(Error.Validation("DueDayOfMonth", "Le jour de règlement doit être compris entre 1 et 31"));

        if (discountPercent.HasValue)
        {
            if (discountPercent is <= 0 or > 100)
                return Result.Failure(Error.Validation("EarlyPaymentDiscountPercent", "L'escompte doit être compris entre 0 % et 100 %"));

            if (discountDays is null or <= 0)
                return Result.Failure(Error.Validation("EarlyPaymentDays", "Précisez sous combien de jours l'escompte s'applique"));

            if (discountDays > delayDays)
            {
                return Result.Failure(Error.Validation("EarlyPaymentDays",
                    "Le délai d'escompte doit être inférieur au délai de règlement, sinon l'escompte est toujours acquis"));
            }
        }

        return Result.Success();
    }

    public Result Update(
        string? name,
        int delayDays,
        PaymentDueMode dueMode,
        int? dueDayOfMonth,
        decimal? earlyPaymentDiscountPercent,
        int? earlyPaymentDays)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Le libellé est obligatoire"));

        var validation = Validate(delayDays, dueMode, dueDayOfMonth, earlyPaymentDiscountPercent, earlyPaymentDays);
        if (validation.IsFailure)
            return validation;

        Name = name.Trim();
        DelayDays = delayDays;
        DueMode = dueMode;
        DueDayOfMonth = dueMode == PaymentDueMode.EndOfMonthOnDay ? dueDayOfMonth : null;
        EarlyPaymentDiscountPercent = earlyPaymentDiscountPercent;
        EarlyPaymentDays = earlyPaymentDiscountPercent.HasValue ? earlyPaymentDays : null;

        return Result.Success();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void MarkAsDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;

    /// <summary>
    /// Échéance calculée à partir de la date du document. C'est ce que le champ texte libre ne
    /// savait pas produire — et donc ce qui manquait à toute balance âgée fiable.
    /// </summary>
    public DateTime ComputeDueDate(DateTime documentDate)
    {
        var baseDate = documentDate.Date.AddDays(DelayDays);

        return DueMode switch
        {
            PaymentDueMode.NetDays => baseDate,

            PaymentDueMode.EndOfMonth => new DateTime(baseDate.Year, baseDate.Month,
                DateTime.DaysInMonth(baseDate.Year, baseDate.Month)),

            PaymentDueMode.EndOfMonthOnDay => EndOfMonthOnDay(baseDate),

            _ => baseDate
        };
    }

    /// <summary>
    /// Fin de mois puis report au jour imposé du mois suivant. Le jour est ramené au dernier jour
    /// du mois s'il n'existe pas (31 en février).
    /// </summary>
    private DateTime EndOfMonthOnDay(DateTime baseDate)
    {
        var target = new DateTime(baseDate.Year, baseDate.Month, 1).AddMonths(1);
        var day = Math.Min(DueDayOfMonth ?? 1, DateTime.DaysInMonth(target.Year, target.Month));
        return new DateTime(target.Year, target.Month, day);
    }

    /// <summary>
    /// Date limite pour bénéficier de l'escompte, ou <c>null</c> s'il n'y en a pas.
    /// </summary>
    public DateTime? ComputeEarlyPaymentDeadline(DateTime documentDate) =>
        EarlyPaymentDays.HasValue ? documentDate.Date.AddDays(EarlyPaymentDays.Value) : null;

    /// <summary>
    /// Libellé destiné au champ texte <c>PaymentTerms</c> du document. C'est le pont entre le
    /// modèle structuré et ce qui s'imprime : les documents restent lisibles à l'identique.
    /// </summary>
    public string ToDocumentLabel()
    {
        var label = DueMode switch
        {
            PaymentDueMode.NetDays when DelayDays == 0 => "Paiement comptant",
            PaymentDueMode.NetDays => $"Paiement à {DelayDays} jours",
            PaymentDueMode.EndOfMonth => $"Paiement à {DelayDays} jours fin de mois",
            PaymentDueMode.EndOfMonthOnDay => $"Paiement à {DelayDays} jours fin de mois le {DueDayOfMonth}",
            _ => $"Paiement à {DelayDays} jours"
        };

        if (EarlyPaymentDiscountPercent is { } pct && EarlyPaymentDays is { } days)
            label += $" — escompte {pct} % sous {days} jours";

        return label;
    }
}

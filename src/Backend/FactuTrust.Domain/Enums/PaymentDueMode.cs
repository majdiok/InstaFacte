namespace FactuTrust.Domain.Enums;

/// <summary>
/// Mode de calcul de l'échéance d'un règlement, à partir de la date du document.
/// </summary>
public enum PaymentDueMode
{
    /// <summary>Date du document + délai. « Paiement à 30 jours ».</summary>
    NetDays = 0,

    /// <summary>Date du document + délai, reporté au dernier jour de ce mois.</summary>
    EndOfMonth = 1,

    /// <summary>
    /// Date du document + délai, reporté à un jour imposé du mois suivant.
    /// « 30 jours fin de mois le 10 ».
    /// </summary>
    EndOfMonthOnDay = 2
}

-- Vague 0 — correctif 6 : controle PREALABLE au deploiement.
--
-- A partir de ce correctif, ValidateInvoiceCommand refuse une facture non conforme. Ce handler
-- est le point de passage de TOUS les chemins hors assistant (creation directe, conversion
-- devis, conversion BL).
--
-- Les factures DEJA validees ne repassent jamais par ce handler (invoice.Validate() exige
-- Status = Draft), elles ne sont donc pas concernees. Seuls les BROUILLONS existants peuvent
-- se retrouver bloques.
--
-- LECTURE SEULE. A executer sur chaque base tenant AVANT le deploiement. Un resultat vide
-- signifie qu'aucun brouillon existant ne sera bloque.
--
-- Regles bloquantes evaluees par InvoiceComplianceValidator.ValidateInvoiceAsync :
--   invoice-number       numero non vide          (toujours vrai : genere)
--   invoice-client       client renseigne         (toujours vrai : FK obligatoire)
--   invoice-lines        au moins une ligne
--   invoice-vat-rates    taux dans {0, 7, 13, 19}
--   invoice-calculations en-tete = somme des lignes (HT + FODEC + TVA + timbre, signe si avoir)

;WITH LineTotals AS (
    SELECT
        l.InvoiceId,
        COUNT(*)                AS LineCount,
        SUM(l.SubTotal)         AS SumHt,
        SUM(l.FodecAmount)      AS SumFodec,
        SUM(l.VatAmount)        AS SumVat,
        MAX(CASE WHEN l.VatRate NOT IN (0, 7, 13, 19) THEN 1 ELSE 0 END) AS HasInvalidVatRate
    FROM [InvoiceLines] l
    GROUP BY l.InvoiceId
)
SELECT
    i.Id,
    i.Number,
    i.IssueDate,
    i.Type                                          AS TypeCode,      -- 0 = facture, 1 = avoir
    ISNULL(lt.LineCount, 0)                         AS LineCount,
    i.SubTotal, i.FodecAmount, i.TotalVat, i.FiscalStampAmount, i.TotalAmount,
    CASE WHEN i.Type = 1 THEN -1 ELSE 1 END * ISNULL(lt.SumHt, 0)     AS RecalcHt,
    CASE WHEN i.Type = 1 THEN -1 ELSE 1 END * ISNULL(lt.SumFodec, 0)  AS RecalcFodec,
    CASE WHEN i.Type = 1 THEN -1 ELSE 1 END * ISNULL(lt.SumVat, 0)    AS RecalcVat,
    CASE WHEN i.Type = 1 THEN -1 ELSE 1 END * (ISNULL(lt.SumHt, 0) + ISNULL(lt.SumFodec, 0) + ISNULL(lt.SumVat, 0))
        + i.FiscalStampAmount                       AS RecalcTtc,
    CASE
        WHEN ISNULL(lt.LineCount, 0) = 0            THEN 'Aucune ligne'
        WHEN ISNULL(lt.HasInvalidVatRate, 0) = 1    THEN 'Taux TVA non conforme'
        ELSE 'Ecart de calcul en-tete / lignes'
    END                                             AS MotifBlocage
FROM [Invoices] i
LEFT JOIN LineTotals lt ON lt.InvoiceId = i.Id
WHERE i.Status = 0   -- Draft uniquement
  AND (
        ISNULL(lt.LineCount, 0) = 0
     OR ISNULL(lt.HasInvalidVatRate, 0) = 1
     OR ABS(CASE WHEN i.Type = 1 THEN -1 ELSE 1 END * ISNULL(lt.SumHt, 0)    - i.SubTotal)    >= 0.001
     OR ABS(CASE WHEN i.Type = 1 THEN -1 ELSE 1 END * ISNULL(lt.SumFodec, 0) - i.FodecAmount) >= 0.001
     OR ABS(CASE WHEN i.Type = 1 THEN -1 ELSE 1 END * ISNULL(lt.SumVat, 0)   - i.TotalVat)    >= 0.001
     OR ABS(
            CASE WHEN i.Type = 1 THEN -1 ELSE 1 END
              * (ISNULL(lt.SumHt, 0) + ISNULL(lt.SumFodec, 0) + ISNULL(lt.SumVat, 0))
            + i.FiscalStampAmount - i.TotalAmount
        ) >= 0.001
  )
ORDER BY i.IssueDate DESC;

-- Conduite a tenir si des lignes remontent :
--   'Aucune ligne'                  -> brouillon vide, sans valeur : supprimable ou completable.
--   'Taux TVA non conforme'         -> corriger la ligne (0 / 7 / 13 / 19).
--   'Ecart de calcul en-tete/lignes'-> rouvrir et réenregistrer le brouillon : le domaine
--                                      recalcule l'en-tete a partir des lignes.

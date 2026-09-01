-- Requalification des comptes auxiliaires salariés 425xxxxxxx (idempotent, base tenant)
--
-- Contexte
--   Avant le correctif, les comptes auxiliaires de paie étaient créés par le filet générique
--   d'auto-création de sous-comptes : comptes GÉNÉRAUX (AccountType = 0), non marqués auxiliaires,
--   sans compte d'affectation, et libellés par concaténation en cascade du libellé parent.
--
--   Deux formes de libellé dégradé coexistent :
--     a) « Personnel - rémunérations dues — 4256854545 »          (créé après le remap NCT 01)
--     b) « Personnel et comptes rattachés — 421 — 4218744456 »    (créé AVANT le remap)
--
--   La forme (b) est la plus trompeuse : le remap NCT 01 a réécrit le NUMÉRO 421… → 425… mais
--   jamais le LIBELLÉ. Un compte numéroté 4258744456 continue donc de s'annoncer « … 4218744456 »,
--   c'est-à-dire de citer un compte qui n'existe plus, sous un intitulé de niveau 2.
--
--   Les comptes créés à partir du correctif sont corrects d'emblée. Ce script rattrape l'existant.
--
-- Portée
--   Uniquement les sous-comptes stricts de 425 (425 + au moins un chiffre), non système.
--   Le compte collectif 425 lui-même n'est jamais touché.
--   AUCUNE écriture, AUCUN solde, AUCUN bulletin n'est modifié : seules des colonnes descriptives
--   de ChartOfAccounts changent (Label, IsAuxiliary, AffectationAccountNumber,
--   ParentAccountNumber, AccountType, Level).
--
-- Exécution
--   Lancer d'abord la section 1 seule et vérifier la correspondance compte ↔ salarié.
--   Rejouable sans effet supplémentaire.

SET NOCOUNT ON;

-- ── 0. Correspondance compte auxiliaire → salarié ─────────────────────────────────────────────
--    Source principale : le compte figé sur le bulletin. Repli : le tiers porté par la ligne
--    d'écriture (ThirdPartyKind = 3 = Employee). La détection est STRUCTURELLE — elle ne suppose
--    rien du libellé actuel, contrairement à la version précédente de ce runbook, qui manquait
--    justement les libellés hérités du remap.
IF OBJECT_ID('tempdb..#PayrollAux') IS NOT NULL DROP TABLE #PayrollAux;

SELECT
    a.AccountNumber,
    a.Label                                   AS LabelActuel,
    a.IsAuxiliary,
    a.AccountType,
    a.AffectationAccountNumber,
    EmployeeName = LTRIM(RTRIM(COALESCE(viaPayslip.FullName, viaEntry.FullName)))
INTO #PayrollAux
FROM ChartOfAccounts a
OUTER APPLY (
    SELECT TOP 1 e.FirstName + N' ' + e.LastName AS FullName
    FROM Payslips p
    INNER JOIN Employees e ON e.Id = p.EmployeeId
    WHERE p.EmployeeAuxiliaryAccount = a.AccountNumber
    ORDER BY p.Year DESC, p.Month DESC
) viaPayslip
OUTER APPLY (
    SELECT TOP 1 e.FirstName + N' ' + e.LastName AS FullName
    FROM JournalEntryLines l
    INNER JOIN Employees e ON e.Id = l.ThirdPartyId
    WHERE l.AccountNumber = a.AccountNumber
      AND l.ThirdPartyKind = 3
) viaEntry
WHERE a.AccountNumber LIKE N'425[0-9]%'
  AND a.IsSystem = 0;

-- ── 1. Contrôle préalable : ce qui sera modifié ───────────────────────────────────────────────
SELECT
    AccountNumber,
    LabelActuel,
    LabelCible = CASE
        WHEN EmployeeName IS NULL OR EmployeeName = N'' THEN N'(salarié introuvable — libellé conservé)'
        WHEN LabelActuel LIKE N'%[0-9][0-9][0-9][0-9][0-9][0-9][0-9]%' THEN EmployeeName
        ELSE N'(libellé déjà personnalisé — conservé)'
    END,
    IsAuxiliary,
    AccountType,
    AffectationAccountNumber
FROM #PayrollAux
ORDER BY AccountNumber;

-- ── 2. Typage auxiliaire (indépendant du libellé) ─────────────────────────────────────────────
UPDATE a
SET a.IsAuxiliary = 1,
    a.AffectationAccountNumber = N'425',
    a.ParentAccountNumber = N'425',
    a.AccountType = 3,          -- AccountType.Other : tiers auxiliaire, ni client ni fournisseur
    a.Level = LEN(a.AccountNumber),
    a.UpdatedAt = GETUTCDATE(),
    a.UpdatedBy = N'runbook:RequalifyPayrollAuxiliaryAccounts'
FROM ChartOfAccounts a
INNER JOIN #PayrollAux x ON x.AccountNumber = a.AccountNumber
WHERE a.IsAuxiliary = 0
   OR a.AffectationAccountNumber IS NULL
   OR a.AffectationAccountNumber <> N'425'
   OR a.ParentAccountNumber IS NULL
   OR a.ParentAccountNumber <> N'425'
   OR a.AccountType = 0
   OR a.Level <> LEN(a.AccountNumber);

-- ── 3. Libellé = nom du salarié ───────────────────────────────────────────────────────────────
--    Seuls les libellés portant encore une queue numérique (≥ 7 chiffres consécutifs, signature de
--    l'auto-génération) sont remplacés — quelle que soit la forme, (a) ou (b). Un libellé saisi à
--    la main (« Karim Soumi ») ne contient pas cette séquence et reste donc intact.
UPDATE a
SET a.Label = x.EmployeeName,
    a.UpdatedAt = GETUTCDATE(),
    a.UpdatedBy = N'runbook:RequalifyPayrollAuxiliaryAccounts'
FROM ChartOfAccounts a
INNER JOIN #PayrollAux x ON x.AccountNumber = a.AccountNumber
WHERE x.EmployeeName IS NOT NULL
  AND x.EmployeeName <> N''
  AND a.Label LIKE N'%[0-9][0-9][0-9][0-9][0-9][0-9][0-9]%'
  AND a.Label <> x.EmployeeName;

-- ── 4. Contrôle final ─────────────────────────────────────────────────────────────────────────
SELECT AccountNumber, Label, AccountClass, AccountType, IsAuxiliary,
       ParentAccountNumber, AffectationAccountNumber, Level
FROM ChartOfAccounts
WHERE AccountNumber LIKE N'425%'
ORDER BY AccountNumber;

-- ── 5. Reste-t-il des auxiliaires non requalifiés ? ───────────────────────────────────────────
--    Ce SELECT doit revenir vide. Sinon : salarié introuvable (compte d'un salarié supprimé) —
--    renommer le compte à la main depuis l'écran Plan comptable.
SELECT AccountNumber, Label
FROM ChartOfAccounts
WHERE AccountNumber LIKE N'425[0-9]%'
  AND IsSystem = 0
  AND (IsAuxiliary = 0 OR Label LIKE N'%[0-9][0-9][0-9][0-9][0-9][0-9][0-9]%');

DROP TABLE #PayrollAux;

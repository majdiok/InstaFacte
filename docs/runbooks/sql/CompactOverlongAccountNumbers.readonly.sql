-- Renumérotation des comptes de plus de 8 chiffres — PRÉ-CONTRÔLE ET FORENSIQUE (lecture seule)
--
-- Contexte
--   Un numéro de compte ne doit pas dépasser 8 chiffres. Les comptes auxiliaires salariés hérités
--   valent « 425 » + les 7 derniers chiffres du matricule (souvent un CIN), soit 10 chiffres :
--   4259655554, 4258744456… Le générateur a été remplacé par une allocation séquentielle
--   (425 + 4 chiffres) ; l'existant est renuméroté par ChartAccountDigitCompactionService, appliqué
--   automatiquement au bootstrap de chaque dossier.
--
--   CE SCRIPT N'ÉCRIT RIEN. Il sert à (1) savoir ce qui va bouger avant de déployer, (2) retrouver
--   après coup où est parti un ancien numéro, (3) produire la carte inverse en cas de retour arrière.
--
-- Ordre d'utilisation
--   Sections 1 et 2 AVANT le déploiement, sur chaque base tenant. Sections 3 à 5 après.
--
-- Sauvegarde
--   La renumérotation réécrit des lignes d'écritures, y compris sur des périodes validées ou
--   clôturées. Sauvegarder la base avant de déployer. Aucun montant n'est modifié — le service
--   refuse de valider sa transaction si la balance générale bouge — mais l'historique change de
--   numéro de compte, ce qui se voit sur un FEC régénéré a posteriori.

SET NOCOUNT ON;

-- ── 1. Recensement : quels comptes dépassent 8 chiffres ? ─────────────────────────────────────
--    Le filtre porte sur la LONGUEUR (condition nécessaire : plus de 8 chiffres implique plus de
--    8 caractères), la colonne NbChiffres donne la décision exacte. Les sous-comptes pointés de
--    l'overlay métier (421.1, 428.1) ont peu de chiffres et ne sont donc jamais concernés.
SELECT
    a.AccountNumber,
    a.Label,
    NbChiffres = LEN(a.AccountNumber) - LEN(REPLACE(a.AccountNumber, '.', '')),
    a.IsSystem,
    a.IsAuxiliary,
    a.AffectationAccountNumber,
    a.Level,
    NbLignesEcriture  = (SELECT COUNT(1) FROM JournalEntryLines l WHERE l.AccountNumber = a.AccountNumber),
    NbBulletins       = (SELECT COUNT(1) FROM Payslips p WHERE p.EmployeeAuxiliaryAccount = a.AccountNumber),
    NbLignesReglement = (SELECT COUNT(1) FROM PayrollPaymentLines pl WHERE pl.EmployeeAuxiliaryAccount = a.AccountNumber),
    NbLettrages       = (SELECT COUNT(1) FROM LetteringGroups g WHERE g.AccountNumber = a.AccountNumber),
    Salarie = (
        SELECT TOP 1 LTRIM(RTRIM(e.FirstName + N' ' + e.LastName))
        FROM Payslips p
        INNER JOIN Employees e ON e.Id = p.EmployeeId
        WHERE p.EmployeeAuxiliaryAccount = a.AccountNumber
        ORDER BY p.Year DESC, p.Month DESC)
FROM ChartOfAccounts a
WHERE LEN(a.AccountNumber) > 8
  AND LEN(a.AccountNumber) - LEN(REPLACE(a.AccountNumber, '.', '')) > 8
ORDER BY a.AccountNumber;

-- ── 2. Anomalies bloquantes ───────────────────────────────────────────────────────────────────
--    Le service refuse la renumérotation entière si l'une d'elles est présente. Les traiter à la
--    main AVANT de déployer, sinon le dossier reste en erreur au démarrage (HTTP 503).
--
--    a) Compte hors norme ayant des sous-comptes : les renuméroter briserait l'invariant
--       « le numéro commence par le compte parent » pour chaque enfant.
SELECT Anomalie = 'Sous-comptes', parent.AccountNumber, parent.Label, enfant.AccountNumber AS Enfant
FROM ChartOfAccounts parent
INNER JOIN ChartOfAccounts enfant
    ON enfant.AccountNumber LIKE parent.AccountNumber + N'%'
   AND enfant.AccountNumber <> parent.AccountNumber
WHERE LEN(parent.AccountNumber) - LEN(REPLACE(parent.AccountNumber, '.', '')) > 8

UNION ALL

--    b) Compte système hors norme : le catalogue NCT 01 n'en contient aucun de cette longueur.
--       Sa présence signale un plan comptable corrompu — ne pas renuméroter, faire analyser.
SELECT Anomalie = 'Compte systeme', a.AccountNumber, a.Label, NULL
FROM ChartOfAccounts a
WHERE a.IsSystem = 1
  AND LEN(a.AccountNumber) - LEN(REPLACE(a.AccountNumber, '.', '')) > 8;

-- ── 3. Balance générale, à relever avant ET après ─────────────────────────────────────────────
--    Les deux totaux doivent être identiques au millime près. Le service vérifie déjà cette
--    post-condition dans sa transaction et annule tout en cas d'écart ; ce relevé est la trace
--    écrite pour le dossier de contrôle.
SELECT
    TotalDebit  = ISNULL(SUM(DebitAmount), 0),
    TotalCredit = ISNULL(SUM(CreditAmount), 0),
    NbLignes    = COUNT(1)
FROM JournalEntryLines;

-- ── 4. Après coup : où est parti un ancien numéro ? ───────────────────────────────────────────
--    Table alimentée par la renumérotation, conservée indéfiniment. C'est la pièce justificative
--    à produire si un contrôle compare un FEC déposé avec la base actuelle.
IF OBJECT_ID(N'dbo.ChartOfAccountCompactionLogs', N'U') IS NOT NULL
    SELECT
        l.FromAccountNumber,
        l.ToAccountNumber,
        l.DigitsBefore,
        l.RootAccountNumber,
        l.EmployeeName,
        LibelleActuel = a.Label,
        l.AppliedAt,
        l.AppliedBy,
        l.BatchId
    FROM dbo.ChartOfAccountCompactionLogs l
    LEFT JOIN ChartOfAccounts a ON a.AccountNumber = l.ToAccountNumber
    ORDER BY l.AppliedAt DESC, l.FromAccountNumber;

-- ── 5. Retour arrière : carte inverse ─────────────────────────────────────────────────────────
--    La renumérotation est injective vers un vivier de numéros libres : contrairement au remap
--    NCT 01 (qui comporte des permutations 421 <-> 425 et impose une restauration), elle s'inverse
--    exactement en rejouant la correspondance dans l'autre sens.
--
--    Ce SELECT ne fait que GÉNÉRER le script de retour ; relire ce qu'il produit avant d'exécuter
--    quoi que ce soit, et le faire dans une transaction unique, base à l'arrêt.
IF OBJECT_ID(N'dbo.ChartOfAccountCompactionLogs', N'U') IS NOT NULL
    SELECT ScriptInverse =
        N'UPDATE ' + t.TableName + N' SET ' + t.ColumnName
        + N' = N''' + l.FromAccountNumber + N''' WHERE ' + t.ColumnName
        + N' = N''' + l.ToAccountNumber + N''';'
    FROM dbo.ChartOfAccountCompactionLogs l
    CROSS JOIN (VALUES
        (N'[ChartOfAccounts]',              N'[AccountNumber]'),
        (N'[ChartOfAccounts]',              N'[ParentAccountNumber]'),
        (N'[ChartOfAccounts]',              N'[AffectationAccountNumber]'),
        (N'[JournalEntryLines]',            N'[AccountNumber]'),
        (N'[LetteringGroups]',              N'[AccountNumber]'),
        (N'[Payslips]',                     N'[EmployeeAuxiliaryAccount]'),
        (N'[PayrollPaymentLines]',          N'[EmployeeAuxiliaryAccount]'),
        (N'[Employees]',                    N'[AuxiliaryAccountNumber]')
    ) AS t(TableName, ColumnName)
    ORDER BY l.FromAccountNumber, t.TableName, t.ColumnName;

-- Vérification finale, après déploiement : ce SELECT doit revenir vide.
SELECT AccountNumber, Label
FROM ChartOfAccounts
WHERE LEN(AccountNumber) - LEN(REPLACE(AccountNumber, '.', '')) > 8;

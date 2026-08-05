-- Idempotent seed: comptes SCE retenues et avantages paie avancés

IF NOT EXISTS (SELECT 1 FROM ChartOfAccounts WHERE AccountNumber = '425.1')

INSERT INTO ChartOfAccounts (Id, AccountNumber, Label, AccountClass, ParentAccountNumber, NatureType, IsSystem, IsActive, Level, AccountType, IsAuxiliary, AffectationAccountNumber, CreatedAt, UpdatedAt)

VALUES (NEWID(), '425.1', 'Personnel — prêts en cours', 4, '42', 1, 1, 1, 3, 0, 0, NULL, GETUTCDATE(), NULL);



IF NOT EXISTS (SELECT 1 FROM ChartOfAccounts WHERE AccountNumber = '427')

INSERT INTO ChartOfAccounts (Id, AccountNumber, Label, AccountClass, ParentAccountNumber, NatureType, IsSystem, IsActive, Level, AccountType, IsAuxiliary, AffectationAccountNumber, CreatedAt, UpdatedAt)

VALUES (NEWID(), '427', 'Personnel — oppositions et saisies', 4, '42', 1, 1, 1, 3, 0, 0, NULL, GETUTCDATE(), NULL);



IF NOT EXISTS (SELECT 1 FROM ChartOfAccounts WHERE AccountNumber = '428.1')

INSERT INTO ChartOfAccounts (Id, AccountNumber, Label, AccountClass, ParentAccountNumber, NatureType, IsSystem, IsActive, Level, AccountType, IsAuxiliary, AffectationAccountNumber, CreatedAt, UpdatedAt)

VALUES (NEWID(), '428.1', 'Personnel — mutuelles et caisses complémentaires', 4, '42', 1, 1, 1, 3, 0, 0, NULL, GETUTCDATE(), NULL);



IF NOT EXISTS (SELECT 1 FROM ChartOfAccounts WHERE AccountNumber = '428.2')

INSERT INTO ChartOfAccounts (Id, AccountNumber, Label, AccountClass, ParentAccountNumber, NatureType, IsSystem, IsActive, Level, AccountType, IsAuxiliary, AffectationAccountNumber, CreatedAt, UpdatedAt)

VALUES (NEWID(), '428.2', 'Personnel — tickets restaurant (part employée)', 4, '42', 1, 1, 1, 3, 0, 0, NULL, GETUTCDATE(), NULL);


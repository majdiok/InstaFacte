# Echeancier Fiscal Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ajouter une fenetre "Echeancier fiscal" inspiree de Sage 100, accessible depuis le menu des declarations, avec vue cabinet multi-societes et vue dossier deleguee, sans regression sur les declarations TVA/TEJ/comptabilite existantes.

**Architecture:** Creer un module fiscal dedie autour d'un read-model d'echeances, distinct de `VatDeclaration`, afin de ne pas surcharger la declaration mensuelle existante. La vue cabinet agrege les echeances des dossiers clients actifs via le meme pattern de fan-out deja utilise par `FirmDashboardService`, tandis que la vue dossier utilise le tenant courant. Les actions rapides s'appuient sur des commandes explicites: depot, paiement, rappel, piece jointe, modification manuelle.

**Tech Stack:** Angular 17 standalone components, PrimeNG, RxJS signals, ASP.NET Core 8, MediatR, EF Core SQL Server multi-tenant, Jasmine/Karma, Playwright, xUnit backend.

---

## 1. Analyse fonctionnelle de la capture Sage 100

### Zones visibles a reproduire

1. Navigation laterale
   - Section active: `Declarations`.
   - Sous-menu actif: `Echeancier fiscal`.
   - Sous-menu voisin: `Paiements et quittances`.
   - Le contexte utilisateur affiche societe, exercice, profil et version.

2. Entete de fenetre
   - Titre: `Echeancier fiscal`.
   - Barre d'actions: `Creer`, `Modifier`, `Supprimer`, `Actualiser`, `Imprimer`, `Exporter`, `Planifier les rappels`, `Aide`.
   - Les actions doivent etre activees/desactivees selon selection, droits, statut et contexte cabinet.

3. Cartes KPI
   - `A venir (<= 7 jours)`: nombre d'echeances et montant total.
   - `A venir (> 7 jours)`: nombre d'echeances et montant total.
   - `En retard`: nombre d'echeances et montant total.
   - `Deposees ce mois`: nombre de declarations et montant total.
   - `Total echeances`: nombre total et montant total.

4. Filtres
   - `Societe`: toutes ou une societe client.
   - `Exercice`: annee fiscale.
   - `Periode`: tous, mois, trimestre, exercice.
   - `Type d'obligation`: declaration mensuelle, acompte IS, retenue a la source, FODEC, TVA trimestre, etats financiers, IRPP acompte, autres.
   - `Statut`: tous, a venir <= 7 jours, a venir > 7 jours, en retard, deposee, validee, payee.
   - `Responsable`: tous ou collaborateur cabinet.
   - `Periode du / au`: intervalle de date d'echeance.
   - Bouton `Appliquer les filtres`.

5. Tableau principal
   - Colonnes exactes:
     - icone statut
     - `Date d'echeance`
     - `Type d'obligation`
     - `Periode / Exercice`
     - `Societe`
     - `Montant estime (TND)`
     - `Statut`
     - `Date depot`
     - `Date paiement`
     - `Responsable`
     - `Observations`
   - Badges couleur:
     - rouge: `En retard`
     - orange: `A venir (<= 7 j)`
     - bleu: `A venir (> 7 j)`
     - vert: `Deposee`
     - vert/bleu secondaire: `Validee` ou `Payee`
   - Ligne de total:
     - `Nombre d'echeances : N`
     - total montant estime.
   - Pagination et selecteur `Afficher 25 elements`.

6. Panneau detail de l'echeance selectionnee
   - Bloc `Detail de l'echeance selectionnee`:
     - Type d'obligation
     - Periode
     - Date d'echeance
     - Societe
     - Montant estime
     - Statut
     - Responsable
     - Observations
   - Bloc `Pieces jointes`:
     - liste des pieces ou message `Aucune piece jointe`.
   - Bloc `Actions rapides`:
     - `Ouvrir la declaration`
     - `Marquer comme deposee`
     - `Saisir le paiement`
     - `Joindre un document`
   - Bloc `Historique`:
     - cree le
     - modifie le
     - dernier rappel

7. Pied de fenetre
   - Bouton gauche `Fermer`.
   - Bouton primaire `Nouveau rappel`.
   - Bouton droit `Fermer`.
   - Dans l'application web, eviter les doublons inutiles: garder un `Fermer` si l'ecran est dans une route pleine page, garder deux actions seulement si une vraie fenetre modale est implementee.

### Differences necessaires dans FactuTrust

- Le code actuel a une route `/accounting/vat-declaration`, mais pas encore un echeancier fiscal.
- `VatDeclaration` represente une declaration mensuelle, pas une liste d'obligations. Il ne doit pas devenir un fourre-tout.
- Le cabinet existe en deux modes:
  - mode natif cabinet: routes `/firm/...`, acces aux dossiers clients actifs;
  - mode delegue dossier: routes metier comme `/accounting/...`, tenant client courant.
- La capture Sage montre `Societe = Toutes`: il faut donc prevoir une vraie vue cabinet agregee, pas seulement un ecran dans un dossier.

---

## 2. Decisions d'architecture

### Decision A - Deux routes, un coeur de composant

Creer:

- `/firm/fiscal-schedule`: vue cabinet multi-dossiers, visible en mode cabinet natif.
- `/accounting/fiscal-schedule`: vue dossier courant, visible en mode delegue et pour une societe non-cabinet si le module Accounting est actif.

Le composant principal doit recevoir un mode:

- `scope = 'firm'`: charge `GET /api/firm/fiscal-schedule`.
- `scope = 'tenant'`: charge `GET /api/accounting/fiscal-schedule`.

Avantage: on respecte le filtre `Societe = Toutes` sans casser le contexte delegue existant.

### Decision B - Entites fiscales separees

Ne pas ajouter les champs d'echeancier dans `VatDeclaration`.

Creer un agragat `FiscalScheduleEntry` dans la base tenant:

- il represente une obligation fiscale suivie par le cabinet;
- il peut pointer vers une declaration existante (`VatDeclaration`, TEJ, NCT, autre);
- il garde ses propres champs de suivi: responsable, depot, paiement, observations, rappels, pieces jointes.

Creer aussi:

- `FiscalScheduleHistoryEntry`
- `FiscalScheduleAttachment`
- eventuellement `FiscalReminder` si les rappels sont planifies et reutilisables.

### Decision C - Regles fiscales configurables

Les dates fiscales ne doivent pas etre figees uniquement dans le code. Les references officielles consultees indiquent que l'agenda fiscal peut dependre du type de contribuable et que le depot peut etre reporte si l'echeance tombe un dimanche ou jour ferie. Le plan doit donc prevoir:

- une premiere version avec generateur configurable par type d'obligation;
- des valeurs par defaut inspirees de l'existant et de l'agenda fiscal;
- une validation par le cabinet avant production.

### Decision D - Strategie anti-regression

Le developpement doit etre additif:

- ne pas modifier le comportement actuel de `/accounting/vat-declaration`;
- ne pas changer la semantique de `VatDeclarationStatus`;
- ne pas changer la navigation des ventes/achats en mode cabinet delegue;
- ajouter des tests autour des nouveaux liens de navigation, services, permissions et agregations.

---

## 3. Modele de donnees propose

### Enums domaine

Fichiers:

- Create: `src/Backend/FactuTrust.Domain/Enums/FiscalObligationType.cs`
- Create: `src/Backend/FactuTrust.Domain/Enums/FiscalScheduleStatus.cs`
- Create: `src/Backend/FactuTrust.Domain/Enums/FiscalScheduleSourceType.cs`
- Create: `src/Backend/FactuTrust.Domain/Enums/FiscalReminderChannel.cs`

Definitions cible:

```csharp
namespace FactuTrust.Domain.Enums;

public enum FiscalObligationType
{
    MonthlyDeclaration = 0,
    ProvisionalCorporateTaxInstallment = 1,
    WithholdingTax = 2,
    Fodec = 3,
    QuarterlyVat = 4,
    FinancialStatements = 5,
    SemiAnnualFinancialStatements = 6,
    PersonalIncomeTaxInstallment = 7,
    Other = 99
}

public enum FiscalScheduleStatus
{
    UpcomingWithin7Days = 0,
    UpcomingAfter7Days = 1,
    Overdue = 2,
    Deposited = 3,
    Paid = 4,
    Validated = 5,
    Cancelled = 9
}

public enum FiscalScheduleSourceType
{
    Manual = 0,
    VatDeclaration = 1,
    WithholdingTaxTej = 2,
    NctStatements = 3,
    FixedAssets = 4
}

public enum FiscalReminderChannel
{
    Email = 0,
    InApp = 1,
    Sms = 2
}
```

### Entite principale

Fichier:

- Create: `src/Backend/FactuTrust.Domain/Entities/FiscalScheduleEntry.cs`

Champs obligatoires:

```csharp
public sealed class FiscalScheduleEntry : AggregateRoot
{
    public FiscalObligationType ObligationType { get; private set; }
    public string ObligationLabel { get; private set; } = null!;
    public int FiscalYear { get; private set; }
    public int? PeriodMonth { get; private set; }
    public int? PeriodQuarter { get; private set; }
    public DateTime? PeriodStart { get; private set; }
    public DateTime? PeriodEnd { get; private set; }
    public DateTime DueDate { get; private set; }
    public decimal EstimatedAmount { get; private set; }
    public string Currency { get; private set; } = "TND";
    public FiscalScheduleSourceType SourceType { get; private set; }
    public Guid? SourceId { get; private set; }
    public DateTime? DepositDate { get; private set; }
    public DateTime? PaymentDate { get; private set; }
    public Guid? ResponsibleUserId { get; private set; }
    public string? ResponsibleName { get; private set; }
    public string? Observations { get; private set; }
    public DateTime? LastReminderAt { get; private set; }
    public FiscalReminderChannel? LastReminderChannel { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
```

Regles:

- `DueDate` obligatoire.
- `FiscalYear` entre 2000 et 2100.
- `EstimatedAmount >= 0`.
- `Currency` limitee a 3 caracteres, defaut `TND`.
- `DepositDate` ne doit pas etre future de plus de 1 jour sans confirmation explicite.
- `PaymentDate` ne doit pas etre anterieure a `DepositDate` sauf correction autorisee par role.
- `ResponsibleName` longueur max 200.
- `Observations` longueur max 1000.
- Le statut affiche est calcule a la lecture:
  - si `PaymentDate != null`: `Paid`
  - sinon si `DepositDate != null`: `Deposited`
  - sinon si `DueDate < today`: `Overdue`
  - sinon si `DueDate <= today + 7`: `UpcomingWithin7Days`
  - sinon: `UpcomingAfter7Days`

### Historique

Fichier:

- Create: `src/Backend/FactuTrust.Domain/Entities/FiscalScheduleHistoryEntry.cs`

Champs:

- `Id`
- `FiscalScheduleEntryId`
- `Action`
- `CreatedAt`
- `CreatedBy`
- `Summary`
- `OldValuesJson`
- `NewValuesJson`

Actions attendues:

- `Created`
- `Updated`
- `Deleted`
- `MarkedDeposited`
- `PaymentCaptured`
- `ReminderScheduled`
- `ReminderSent`
- `AttachmentAdded`
- `AttachmentRemoved`

### Pieces jointes

Fichier:

- Create: `src/Backend/FactuTrust.Domain/Entities/FiscalScheduleAttachment.cs`

Champs:

- `Id`
- `FiscalScheduleEntryId`
- `FileName`
- `ContentType`
- `SizeBytes`
- `StoragePath`
- `CreatedAt`
- `CreatedBy`

Important: ne pas stocker le binaire en base dans la premiere version. Reutiliser le service de stockage existant si disponible; sinon stocker sous `wwwroot/uploads/tenants/{tenantId}/fiscal-schedule/...` avec garde anti traversal.

---

## 4. Contrats API

### DTOs

Fichier:

- Modify: `src/Backend/FactuTrust.Application/DTOs/AccountingDtos.cs`

Ajouter:

```csharp
public sealed record FiscalScheduleFiltersDto
{
    public Guid? CompanyTenantId { get; init; }
    public int FiscalYear { get; init; }
    public int? PeriodMonth { get; init; }
    public int? PeriodQuarter { get; init; }
    public int? ObligationType { get; init; }
    public int? Status { get; init; }
    public Guid? ResponsibleUserId { get; init; }
    public DateTime? DueFrom { get; init; }
    public DateTime? DueTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record FiscalScheduleSummaryDto
{
    public int UpcomingWithin7DaysCount { get; init; }
    public decimal UpcomingWithin7DaysAmount { get; init; }
    public int UpcomingAfter7DaysCount { get; init; }
    public decimal UpcomingAfter7DaysAmount { get; init; }
    public int OverdueCount { get; init; }
    public decimal OverdueAmount { get; init; }
    public int DepositedThisMonthCount { get; init; }
    public decimal DepositedThisMonthAmount { get; init; }
    public int TotalCount { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed record FiscalScheduleEntryDto
{
    public Guid Id { get; init; }
    public Guid? CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public DateTime DueDate { get; init; }
    public int ObligationType { get; init; }
    public string ObligationTypeDisplay { get; init; } = null!;
    public string PeriodDisplay { get; init; } = null!;
    public int FiscalYear { get; init; }
    public decimal EstimatedAmount { get; init; }
    public string Currency { get; init; } = "TND";
    public int Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public DateTime? DepositDate { get; init; }
    public DateTime? PaymentDate { get; init; }
    public Guid? ResponsibleUserId { get; init; }
    public string? ResponsibleName { get; init; }
    public string? Observations { get; init; }
    public int AttachmentCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = null!;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime? LastReminderAt { get; init; }
    public string? LastReminderChannelDisplay { get; init; }
}

public sealed record FiscalScheduleListDto
{
    public FiscalScheduleSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<FiscalScheduleEntryDto> Items { get; init; } = Array.Empty<FiscalScheduleEntryDto>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public IReadOnlyList<FirmClientDossierDto> Companies { get; init; } = Array.Empty<FirmClientDossierDto>();
}
```

Ajouter aussi:

- `CreateFiscalScheduleEntryRequest`
- `UpdateFiscalScheduleEntryRequest`
- `MarkFiscalScheduleDepositedRequest`
- `CaptureFiscalSchedulePaymentRequest`
- `ScheduleFiscalReminderRequest`
- `FiscalScheduleAttachmentDto`
- `FiscalScheduleHistoryDto`

### Endpoints tenant courant

Fichier:

- Modify: `src/Backend/FactuTrust.API/Controllers/AccountingController.cs`

Endpoints:

- `GET /api/accounting/fiscal-schedule`
- `POST /api/accounting/fiscal-schedule`
- `PUT /api/accounting/fiscal-schedule/{id}`
- `DELETE /api/accounting/fiscal-schedule/{id}`
- `POST /api/accounting/fiscal-schedule/{id}/mark-deposited`
- `POST /api/accounting/fiscal-schedule/{id}/capture-payment`
- `POST /api/accounting/fiscal-schedule/{id}/reminders`
- `GET /api/accounting/fiscal-schedule/{id}/attachments`
- `POST /api/accounting/fiscal-schedule/{id}/attachments`
- `DELETE /api/accounting/fiscal-schedule/{id}/attachments/{attachmentId}`
- `GET /api/accounting/fiscal-schedule/{id}/history`

Policies:

- `GET`: `PermissionPolicies.AccountingRead`
- create/update/delete/deposit/payment/reminder/attachment: `PermissionPolicies.AccountingCreate`
- export/print can remain `AccountingRead`.

### Endpoints cabinet multi-societes

Fichier:

- Create: `src/Backend/FactuTrust.API/Controllers/FirmFiscalScheduleController.cs`

Endpoints:

- `GET /api/firm/fiscal-schedule`
- `POST /api/firm/fiscal-schedule/{companyTenantId}/ensure`
- `POST /api/firm/fiscal-schedule/{companyTenantId}/entries`
- `PUT /api/firm/fiscal-schedule/{companyTenantId}/entries/{id}`
- `DELETE /api/firm/fiscal-schedule/{companyTenantId}/entries/{id}`
- `POST /api/firm/fiscal-schedule/{companyTenantId}/entries/{id}/mark-deposited`
- `POST /api/firm/fiscal-schedule/{companyTenantId}/entries/{id}/capture-payment`

Controller attributes:

```csharp
[ApiController]
[Route("api/firm/fiscal-schedule")]
[Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmFiscalScheduleController : ControllerBase
```

Critical guard:

- Before any company-scoped write, verify `IFirmAssignmentService.HasActiveAssignmentAsync(firmTenantId, companyTenantId)`.
- Never allow a cabinet to query arbitrary tenant IDs outside its active assignments.
- If one tenant aggregation fails, log warning and return other tenants with a warning metadata field only if the API response pattern supports it. Do not fail the entire grid for one broken dossier.

---

## 5. Backend implementation tasks

### Task 1: Add domain enums and aggregate

**Files:**

- Create: `src/Backend/FactuTrust.Domain/Enums/FiscalObligationType.cs`
- Create: `src/Backend/FactuTrust.Domain/Enums/FiscalScheduleStatus.cs`
- Create: `src/Backend/FactuTrust.Domain/Enums/FiscalScheduleSourceType.cs`
- Create: `src/Backend/FactuTrust.Domain/Enums/FiscalReminderChannel.cs`
- Create: `src/Backend/FactuTrust.Domain/Entities/FiscalScheduleEntry.cs`
- Create: `src/Backend/FactuTrust.Domain/Entities/FiscalScheduleHistoryEntry.cs`
- Create: `src/Backend/FactuTrust.Domain/Entities/FiscalScheduleAttachment.cs`
- Test: `src/Backend/tests/FactuTrust.Infrastructure.Tests/Domain/FiscalScheduleEntryTests.cs`

**Steps:**

1. Write failing domain tests for:
   - create valid monthly declaration entry;
   - reject negative amount;
   - mark deposited;
   - capture payment;
   - update responsible and observations;
   - calculate display status from due date and deposit/payment dates.
2. Run:

```powershell
dotnet test src/Backend/tests/FactuTrust.Infrastructure.Tests/FactuTrust.Infrastructure.Tests.csproj --filter FiscalScheduleEntryTests
```

Expected: fail because types do not exist.

3. Implement enums and aggregate with private setters.
4. Rerun the same tests.
5. Commit:

```powershell
git add src/Backend/FactuTrust.Domain src/Backend/tests/FactuTrust.Infrastructure.Tests/Domain/FiscalScheduleEntryTests.cs
git commit -m "feat(accounting): add fiscal schedule domain model"
```

### Task 2: Configure EF tenant mapping

**Files:**

- Modify: `src/Backend/FactuTrust.Infrastructure/Persistence/TenantDbContext.cs`
- Create migration: `src/Backend/FactuTrust.Infrastructure/Migrations/Tenant/<timestamp>_AddFiscalSchedule_Tenant.cs`
- Test: `src/Backend/tests/FactuTrust.Infrastructure.Tests/Migrations/FiscalScheduleMigrationTests.cs`

**EF mapping requirements:**

- Add DbSets:
  - `FiscalScheduleEntries`
  - `FiscalScheduleHistoryEntries`
  - `FiscalScheduleAttachments`
- Add `ConfigureFiscalScheduleEntry(builder)`.
- Table names:
  - `FiscalScheduleEntries`
  - `FiscalScheduleHistoryEntries`
  - `FiscalScheduleAttachments`
- Indexes:
  - `{ FiscalYear, DueDate }`
  - `{ DueDate, ObligationType }`
  - `{ ResponsibleUserId }`
  - `{ SourceType, SourceId }`
  - unique optional source index filtered where `SourceId IS NOT NULL`
- Precision:
  - `EstimatedAmount decimal(18,3)`
- Concurrency:
  - `RowVersion` as rowversion.

**Steps:**

1. Write migration test/snapshot guard if project already has migration guards.
2. Add DbSets and mappings.
3. Generate migration:

```powershell
dotnet ef migrations add AddFiscalSchedule_Tenant --project src/Backend/FactuTrust.Infrastructure --startup-project src/Backend/FactuTrust.API --context TenantDbContext --output-dir Migrations/Tenant
```

4. Inspect migration manually for:
   - no changes to unrelated tables;
   - all precision values correct;
   - indexes present;
   - down migration drops only new objects.
5. Build:

```powershell
dotnet build src/Backend/FactuTrust.sln
```

### Task 3: Repository interfaces and implementation

**Files:**

- Create: `src/Backend/FactuTrust.Application/Common/Interfaces/Repositories/IFiscalScheduleRepository.cs`
- Create: `src/Backend/FactuTrust.Infrastructure/Repositories/FiscalScheduleRepository.cs`
- Modify: `src/Backend/FactuTrust.Infrastructure/DependencyInjection.cs`
- Test: `src/Backend/tests/FactuTrust.Infrastructure.Tests/Repositories/FiscalScheduleRepositoryTests.cs`

**Repository methods:**

```csharp
Task<IReadOnlyList<FiscalScheduleEntry>> SearchAsync(FiscalScheduleSearchCriteria criteria, CancellationToken cancellationToken = default);
Task<int> CountAsync(FiscalScheduleSearchCriteria criteria, CancellationToken cancellationToken = default);
Task<FiscalScheduleEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
Task AddAsync(FiscalScheduleEntry entry, CancellationToken cancellationToken = default);
Task UpdateAsync(FiscalScheduleEntry entry, CancellationToken cancellationToken = default);
Task RemoveAsync(FiscalScheduleEntry entry, CancellationToken cancellationToken = default);
Task AddHistoryAsync(FiscalScheduleHistoryEntry entry, CancellationToken cancellationToken = default);
Task<IReadOnlyList<FiscalScheduleHistoryEntry>> GetHistoryAsync(Guid entryId, CancellationToken cancellationToken = default);
```

**Steps:**

1. Write repository tests using the existing EF test fixture pattern.
2. Implement repository.
3. Register DI next to `IVatDeclarationRepository`.
4. Run:

```powershell
dotnet test src/Backend/tests/FactuTrust.Infrastructure.Tests/FactuTrust.Infrastructure.Tests.csproj --filter FiscalScheduleRepositoryTests
```

### Task 4: Fiscal schedule generator service

**Files:**

- Create: `src/Backend/FactuTrust.Application/Features/Accounting/Services/IFiscalScheduleGenerator.cs`
- Create: `src/Backend/FactuTrust.Infrastructure/Services/FiscalScheduleGenerator.cs`
- Modify: `src/Backend/FactuTrust.Infrastructure/DependencyInjection.cs`
- Test: `src/Backend/tests/FactuTrust.Infrastructure.Tests/Services/FiscalScheduleGeneratorTests.cs`

**Purpose:**

Generate missing standard obligations for a fiscal year without overwriting manually edited rows.

Default obligations for first version:

- monthly declaration for each month;
- withholding tax monthly obligation if withholding module data exists;
- FODEC monthly obligation if monthly declaration V2 is enabled;
- provisional corporate tax installments as manually configurable placeholders;
- quarterly VAT if company tax regime requires it, otherwise disabled by default;
- annual financial statements / NCT;
- semi-annual financial statements if enabled by settings;
- IRPP installment placeholder for eligible dossiers.

**Important:**

- Mark legal-rule defaults as configurable.
- Do not claim legal correctness unless validated by the cabinet.
- Use `VatFilingDeadline.ForPeriod` only for monthly VAT/declaration defaults, and document its current assumption.

**Steps:**

1. Write tests for idempotency:
   - first ensure creates rows;
   - second ensure creates zero additional rows;
   - ensure does not overwrite manually updated amount or responsible.
2. Implement generator.
3. Add options class if needed:
   - `src/Backend/FactuTrust.Application/Configuration/FiscalScheduleOptions.cs`
4. Add config section:
   - `src/Backend/FactuTrust.API/appsettings.json`
   - `src/Backend/FactuTrust.API/appsettings.Development.json`

### Task 5: Query handlers for tenant schedule

**Files:**

- Create: `src/Backend/FactuTrust.Application/Features/Accounting/Queries/GetFiscalScheduleQuery.cs`
- Create: `src/Backend/FactuTrust.Application/Features/Accounting/Queries/GetFiscalScheduleDetailQuery.cs`
- Modify: `src/Backend/FactuTrust.Application/DTOs/AccountingDtos.cs`
- Test: `src/Backend/tests/FactuTrust.Infrastructure.Tests/Application/GetFiscalScheduleQueryTests.cs`

**Behavior:**

- Validate filters.
- Default `FiscalYear` to current year if missing at controller level.
- Page bounds:
  - `Page >= 1`
  - `PageSize` between 10 and 100.
- Sort by `DueDate ASC`, then `CompanyName`, then `ObligationType`.
- Calculate summary over the filtered full result, not only current page.
- Return `Companies` as empty in tenant scope.

**Steps:**

1. Write failing tests for summaries and status calculation.
2. Implement mapper helpers:
   - obligation display labels;
   - period display labels;
   - status labels and CSS-neutral enum mapping.
3. Rerun tests.

### Task 6: Commands for CRUD and workflow

**Files:**

- Create: `src/Backend/FactuTrust.Application/Features/Accounting/Commands/FiscalScheduleCommands.cs`
- Test: `src/Backend/tests/FactuTrust.Infrastructure.Tests/Application/FiscalScheduleCommandTests.cs`

Commands:

- `EnsureFiscalScheduleCommand`
- `CreateFiscalScheduleEntryCommand`
- `UpdateFiscalScheduleEntryCommand`
- `DeleteFiscalScheduleEntryCommand`
- `MarkFiscalScheduleDepositedCommand`
- `CaptureFiscalSchedulePaymentCommand`
- `ScheduleFiscalReminderCommand`

Rules:

- Delete should soft-delete or mark `Cancelled`, unless the entry is purely manual and no history/attachment exists. Prefer soft delete to preserve auditability.
- Mark deposited sets `DepositDate` and history.
- Capture payment sets `PaymentDate`, optional amount if different from estimated.
- Reminder creates history and updates `LastReminderAt` only after successful send/schedule.
- All commands write audit logs through `IAuditService`.

### Task 7: Tenant API endpoints

**Files:**

- Modify: `src/Backend/FactuTrust.API/Controllers/AccountingController.cs`
- Test: `src/Backend/tests/FactuTrust.API.Tests/FiscalScheduleControllerTests.cs`

**Steps:**

1. Add `GET /fiscal-schedule`.
2. Add `POST /fiscal-schedule/ensure`.
3. Add workflow endpoints.
4. Write API tests for:
   - authorization policy attributes;
   - query parameter binding;
   - 400 on invalid filters;
   - success response envelope.

### Task 8: Firm aggregate service and controller

**Files:**

- Create: `src/Backend/FactuTrust.Application/Common/Interfaces/IFirmFiscalScheduleService.cs`
- Create: `src/Backend/FactuTrust.Infrastructure/Services/FirmFiscalScheduleService.cs`
- Create: `src/Backend/FactuTrust.API/Controllers/FirmFiscalScheduleController.cs`
- Modify: `src/Backend/FactuTrust.Infrastructure/DependencyInjection.cs`
- Test: `src/Backend/tests/FactuTrust.Infrastructure.Tests/Services/FirmFiscalScheduleServiceTests.cs`
- Test: `src/Backend/tests/FactuTrust.API.Tests/FirmFiscalScheduleControllerTests.cs`

**Pattern to reuse:**

- `src/Backend/FactuTrust.Infrastructure/Services/FirmDashboardService.cs`
- It already:
  - reads active assignments from `MasterDbContext`;
  - gets tenant connection strings from `ITenantService`;
  - creates `TenantDbContext` per client;
  - catches per-tenant failures.

**Behavior:**

- Load active clients assigned to the firm.
- If `CompanyTenantId` filter is set, verify active assignment.
- For each selected tenant:
  - query schedule entries;
  - map with `CompanyTenantId` and `CompanyName`;
  - include failures as logs.
- Apply final pagination after aggregation if cross-tenant page is small. If large, introduce per-tenant pre-filtering by due date/year and cap tenants.

**Regression guard:**

- Do not change `FirmDashboardService`.
- Do not change firm context token switching.

### Task 9: Attachments and storage

**Files:**

- Create: `src/Backend/FactuTrust.Application/Features/Accounting/Commands/FiscalScheduleAttachmentCommands.cs`
- Create: `src/Backend/FactuTrust.Application/Features/Accounting/Queries/GetFiscalScheduleAttachmentsQuery.cs`
- Modify: `src/Backend/FactuTrust.API/Controllers/AccountingController.cs`
- Modify: `src/Backend/FactuTrust.API/Controllers/FirmFiscalScheduleController.cs`
- Test: `src/Backend/tests/FactuTrust.Infrastructure.Tests/Services/FiscalScheduleAttachmentServiceTests.cs`

**Security:**

- Validate file size.
- Allow only configured MIME types.
- Generate server-side file names.
- Never trust uploaded file path.
- Use tenant-specific directory.
- Check schedule entry belongs to current tenant or assigned company tenant.

### Task 10: Export and print

**Files:**

- Create: `src/Backend/FactuTrust.Application/Features/Accounting/Queries/ExportFiscalScheduleQueries.cs`
- Modify: `src/Backend/FactuTrust.API/Controllers/AccountingController.cs`
- Modify: `src/Backend/FactuTrust.API/Controllers/FirmFiscalScheduleController.cs`
- Modify: `src/Frontend/factutrust-web/src/app/features/accounting/services/accounting.service.ts`

Formats:

- CSV first, because the project already has CSV export patterns.
- PDF/print can be frontend print stylesheet in v1 if backend PDF would create too much scope.

---

## 6. Frontend implementation tasks

### Task 11: Frontend models and API services

**Files:**

- Modify: `src/Frontend/factutrust-web/src/app/features/accounting/services/accounting.service.ts`
- Create: `src/Frontend/factutrust-web/src/app/core/services/firm-fiscal-schedule.service.ts`
- Test: `src/Frontend/factutrust-web/src/app/features/accounting/services/accounting.service.spec.ts`
- Test: `src/Frontend/factutrust-web/src/app/core/services/firm-fiscal-schedule.service.spec.ts`

Add TypeScript interfaces:

- `FiscalScheduleFilters`
- `FiscalScheduleSummary`
- `FiscalScheduleEntry`
- `FiscalScheduleList`
- `CreateFiscalScheduleEntryRequest`
- `UpdateFiscalScheduleEntryRequest`
- `MarkFiscalScheduleDepositedRequest`
- `CaptureFiscalSchedulePaymentRequest`

Tenant methods:

```ts
getFiscalSchedule(filters: FiscalScheduleFilters): Observable<ApiResponse<FiscalScheduleList>>;
ensureFiscalSchedule(fiscalYear: number): Observable<ApiResponse<number>>;
createFiscalScheduleEntry(request: CreateFiscalScheduleEntryRequest): Observable<ApiResponse<string>>;
updateFiscalScheduleEntry(id: string, request: UpdateFiscalScheduleEntryRequest): Observable<ApiResponse<boolean>>;
deleteFiscalScheduleEntry(id: string): Observable<ApiResponse<boolean>>;
markFiscalScheduleDeposited(id: string, request: MarkFiscalScheduleDepositedRequest): Observable<ApiResponse<boolean>>;
captureFiscalSchedulePayment(id: string, request: CaptureFiscalSchedulePaymentRequest): Observable<ApiResponse<boolean>>;
```

Firm methods should mirror tenant methods with optional `companyTenantId`.

### Task 12: View-model utilities

**Files:**

- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule.view-model.ts`
- Test: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule.view-model.spec.ts`

Utilities:

- `formatMoneyTnd`
- `formatDateFr`
- `statusTone`
- `statusIcon`
- `periodOptions`
- `obligationTypeOptions`
- `buildSummaryCards`
- `buildFiscalScheduleAnalyzePayload`
- `normalizeFiscalScheduleFilters`
- `canEditFiscalScheduleEntry`
- `canDeleteFiscalScheduleEntry`
- `canMarkDeposited`
- `canCapturePayment`

Tests:

- status calculation display mappings;
- longest labels do not produce empty CSS classes;
- filters omit null/empty values;
- summary card totals match input.

### Task 13: Route component shell

**Files:**

- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule.component.ts`
- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule.component.spec.ts`

Imports:

- `CommonModule`
- `ReactiveFormsModule`
- PrimeNG modules used in existing screens: `TableModule`, `DropdownModule`, `CalendarModule`, `ButtonModule`, `DialogModule`, `TooltipModule`, `TagModule`, `PaginatorModule`
- shared components:
  - `PageHeaderComponent`
  - `AccountingStatusBannerComponent`
  - `AccountingTableShellComponent`
  - existing `app-button` if used globally

State:

- `scope = input<'tenant' | 'firm'>()`
- `loading`
- `error`
- `items`
- `summary`
- `selectedEntry`
- `filtersForm`
- `page`
- `pageSize`
- `totalItems`
- `companies`

Load flow:

1. Build normalized filters.
2. If first load and fiscal year present, call ensure only when enabled for current scope.
3. Call correct service.
4. Select first row if none selected.
5. Preserve selection by `id` after refresh.

### Task 14: Header toolbar

**Files:**

- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-toolbar.component.ts`
- Test: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-toolbar.component.spec.ts`

Inputs:

- `selectedEntry`
- `loading`
- `canCreate`
- `canEdit`
- `canDelete`

Outputs:

- `create`
- `edit`
- `delete`
- `refresh`
- `print`
- `export`
- `scheduleReminder`
- `help`

UI rules:

- Use icons in buttons.
- Disable modify/delete if no row selected.
- In firm delegated read-only routes, disable destructive actions where appropriate.

### Task 15: KPI cards

**Files:**

- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-summary.component.ts`
- Test: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-summary.component.spec.ts`

Cards:

- upcoming <= 7 days: green/teal tone
- upcoming > 7 days: amber/neutral tone
- overdue: red tone
- deposited this month: blue tone
- total: slate/neutral tone

Use stable grid:

```scss
.fiscal-summary-grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: var(--spacing-3);
}
@media (max-width: 1200px) {
  .fiscal-summary-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
@media (max-width: 640px) {
  .fiscal-summary-grid { grid-template-columns: 1fr; }
}
```

### Task 16: Filters bar

**Files:**

- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-filters.component.ts`
- Test: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-filters.component.spec.ts`

Controls:

- `Societe`: only enabled in `firm` scope.
- `Exercice`: numeric/dropdown.
- `Periode`: dropdown.
- `Type d'obligation`: dropdown.
- `Statut`: dropdown.
- `Responsable`: dropdown.
- `Periode du`: date picker.
- `au`: date picker.
- Button `Appliquer les filtres`.

Validation:

- `dueFrom <= dueTo`.
- `fiscalYear` required.
- reset page to 1 on filter apply.

### Task 17: Main table

**Files:**

- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-table.component.ts`
- Test: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-table.component.spec.ts`

Columns:

- status icon
- dueDate
- obligationTypeDisplay
- periodDisplay
- companyName
- estimatedAmount
- statusDisplay
- depositDate
- paymentDate
- responsibleName
- observations

Behavior:

- row selection emits selected entry.
- double click opens declaration if source exists.
- table uses fixed min widths to avoid header overlap.
- mobile/tablet: horizontal scroll, no hidden fiscal data.
- footer total mirrors Sage: count + amount.

### Task 18: Detail panel

**Files:**

- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-detail.component.ts`
- Test: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-detail.component.spec.ts`

Sections:

- Details.
- Pieces jointes.
- Actions rapides.
- Historique.

Quick actions:

- `Ouvrir la declaration`:
  - monthly declaration -> `/accounting/vat-declaration?year=YYYY&month=MM`
  - TEJ -> `/withholding-tax/tej-export?year=YYYY&month=MM`
  - NCT -> `/accounting/nct-statements?fiscalYear=YYYY`
- `Marquer comme deposee`: opens dialog.
- `Saisir le paiement`: opens dialog.
- `Joindre un document`: file picker.

### Task 19: Dialogs

**Files:**

- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-entry-dialog.component.ts`
- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-deposit-dialog.component.ts`
- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-payment-dialog.component.ts`
- Create: `src/Frontend/factutrust-web/src/app/features/accounting/fiscal-schedule/fiscal-schedule-reminder-dialog.component.ts`
- Test matching `.spec.ts` files.

Rules:

- Entry dialog supports create/edit.
- Deposit dialog requires date depot.
- Payment dialog requires date paiement and optional payment reference.
- Reminder dialog requires channel and reminder date.
- All submit buttons show loading and prevent double-submit.

### Task 20: Routing and navigation

**Files:**

- Modify: `src/Frontend/factutrust-web/src/app/features/accounting/accounting.routes.ts`
- Modify: `src/Frontend/factutrust-web/src/app/features/firm/firm.routes.ts`
- Modify: `src/Frontend/factutrust-web/src/app/core/config/accounting-modules.config.ts`
- Modify: `src/Frontend/factutrust-web/src/app/core/config/app-navigation.registry.ts`
- Modify: `src/Frontend/factutrust-web/src/app/core/config/firm-navigation.registry.ts`
- Test: `src/Frontend/factutrust-web/src/app/core/config/app-navigation.registry.spec.ts`
- Test: `src/Frontend/factutrust-web/src/app/core/config/firm-navigation.registry.spec.ts`
- Test: `src/Frontend/factutrust-web/src/app/core/layout/sidebar/sidebar.component.spec.ts`

Add routes:

```ts
{
  path: 'fiscal-schedule',
  loadComponent: () =>
    import('./fiscal-schedule/fiscal-schedule.component').then(m => m.FiscalScheduleComponent),
  data: { fiscalScheduleScope: 'tenant' },
  title: 'Echeancier fiscal - InstaFact'
}
```

Firm route:

```ts
{
  path: 'fiscal-schedule',
  loadComponent: () =>
    import('../accounting/fiscal-schedule/fiscal-schedule.component').then(m => m.FiscalScheduleComponent),
  data: { fiscalScheduleScope: 'firm' },
  title: 'Echeancier fiscal cabinet - InstaFact'
}
```

Navigation:

- In `FIRM_NATIVE_NAV`, add top-level or child:
  - label: `Echeancier fiscal`
  - route: `/firm/fiscal-schedule`
  - icon: `fa-solid fa-calendar-days`
- In `ACCOUNTING_MODULES`, under declaration section, add:
  - `Echeancier fiscal`
  - route `/accounting/fiscal-schedule`
  - perms `['accounting:read']`
- In `ALL_NAV_ITEMS`, add under `Comptabilite` or a future `Declarations` group.

Search keywords:

- `echeancier fiscal`
- `calendrier fiscal`
- `declaration`
- `impots`
- `cabinet`
- `Sage`

### Task 21: AI screen analysis integration

**Files:**

- Modify: `src/Frontend/factutrust-web/src/app/features/ai-assistant/utils/ai-screen-labels.util.ts`
- Modify: `src/Frontend/factutrust-web/src/app/features/ai-assistant/utils/ai-screen-analysis-prompts.ts`
- Modify: `src/Frontend/factutrust-web/src/app/features/ai-assistant/utils/ai-screen-analysis-config.ts`

Screen id:

- `accounting-fiscal-schedule`
- `firm-fiscal-schedule`

Payload:

- counts by status;
- top overdue entries;
- total estimated amount;
- selected company filter;
- selected fiscal year.

### Task 22: E2E smoke coverage

**Files:**

- Create: `src/Frontend/factutrust-web/e2e/fiscal-schedule.spec.ts`

Scenarios:

1. Cabinet manager opens `/firm/fiscal-schedule`.
2. KPI cards render.
3. Filter by company.
4. Select first row.
5. Detail panel renders.
6. Open mark-deposited dialog and cancel.
7. Export button triggers request.

If test fixtures are not ready, mark as smoke with existing auth helper pattern.

---

## 7. UX and visual requirements

### Layout target

Use a dense operational layout, not a landing page:

- page header compact;
- toolbar horizontal;
- KPI cards in one row on desktop;
- filters in a bordered band;
- table as central area;
- detail panel below table, split into four columns on desktop;
- responsive horizontal scroll for the table.

### Accessibility

- Every icon button has `aria-label`.
- Badges include visible text, not color only.
- Table row selection is keyboard reachable.
- Dialog focus returns to triggering button.
- Loading state uses `aria-busy`.

### Empty and error states

- No echeance:
  - show empty state with action `Generer l'echeancier`.
- Partial cabinet aggregation failure:
  - show warning banner, not full page crash.
- Network error:
  - retry button.
- Unauthorized:
  - rely on existing interceptor/dialog.

---

## 8. Non-regression checklist

Before merging:

1. Existing declaration TVA still loads:

```powershell
cd src/Frontend/factutrust-web
npm test -- --watch=false --include=src/app/features/accounting/vat-declaration/vat-declaration.component.spec.ts
```

2. Existing accounting service tests still pass:

```powershell
cd src/Frontend/factutrust-web
npm test -- --watch=false --include=src/app/features/accounting/services/accounting.service.spec.ts
```

3. Navigation tests still pass:

```powershell
cd src/Frontend/factutrust-web
npm test -- --watch=false --include=src/app/core/config/app-navigation.registry.spec.ts --include=src/app/core/config/firm-navigation.registry.spec.ts --include=src/app/core/layout/sidebar/sidebar.component.spec.ts
```

4. Backend build:

```powershell
dotnet build src/Backend/FactuTrust.sln
```

5. Backend targeted tests:

```powershell
dotnet test src/Backend/tests/FactuTrust.Infrastructure.Tests/FactuTrust.Infrastructure.Tests.csproj --filter "FiscalSchedule|GetVatDeclarationV2|FirmClientAssignment|AccountingReportingDraftPolicy"
dotnet test src/Backend/tests/FactuTrust.API.Tests/FactuTrust.API.Tests.csproj --filter "FiscalSchedule|DelegatedAccess"
```

6. Frontend production build:

```powershell
cd src/Frontend/factutrust-web
npm run build
```

7. Manual QA:

- non-firm company user can see `/accounting/fiscal-schedule` if Accounting module and `accounting:read`.
- firm manager can see `/firm/fiscal-schedule`.
- firm accountant cannot access unassigned tenant data.
- delegated firm mode still filters Ventes/Achats as before.
- `/accounting/vat-declaration` save/submit/export still works.
- attaching a document does not expose another tenant file.

---

## 9. Rollout plan

### Phase 1 - Foundation

- Domain + EF + repository.
- Tenant route `/accounting/fiscal-schedule`.
- UI for a single dossier.
- No firm aggregation yet.

### Phase 2 - Cabinet multi-societes

- `/firm/fiscal-schedule`.
- Cross-tenant aggregation.
- `Societe = Toutes`.
- Company filter.

### Phase 3 - Workflow complet

- Attachments.
- Reminders.
- History.
- Export/print.

### Phase 4 - Fiscal rule hardening

- Configurable fiscal calendar.
- Holiday/weekend adjustment.
- Admin settings for obligation templates.
- Validation by the accounting firm before enabling automatic generation in production.

---

## 10. Risks and mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Legal deadlines change | Wrong fiscal follow-up | Make rules configurable; show source/last validation date; allow manual override |
| Cross-tenant aggregation slow | Slow cabinet page | Filter by fiscal year/date first, cap page size, log per tenant failures, consider cached projection later |
| Data leak across cabinet clients | Critical security issue | Verify active assignment for every company tenant ID; add API tests |
| Regression in VAT declaration | Existing workflow broken | Keep `VatDeclaration` unchanged; add route and DTOs additively |
| Attachment traversal or MIME abuse | Security issue | Server-side file names, MIME allowlist, size cap, tenant-scoped storage |
| Conflicting edits | Lost updates | Use `RowVersion` concurrency token |
| UI overcrowded on mobile | Poor usability | Horizontal table scroll; detail panel stacks; stable min widths |

---

## 11. Implementation order

Recommended commit order:

1. `feat(accounting): add fiscal schedule domain model`
2. `feat(accounting): persist fiscal schedule entries`
3. `feat(accounting): expose tenant fiscal schedule api`
4. `feat(accounting): add fiscal schedule page`
5. `feat(firm): aggregate fiscal schedule across clients`
6. `feat(accounting): add fiscal schedule workflow actions`
7. `feat(accounting): add fiscal schedule attachments and reminders`
8. `test(accounting): add fiscal schedule e2e smoke`
9. `docs(accounting): document fiscal schedule validation`

Do not combine backend schema, firm aggregation, and full UI in one commit. That would make regression review unnecessarily hard.

---

## 12. Acceptance criteria

Feature is complete when:

- Cabinet manager opens `Echeancier fiscal` from the cabinet menu and sees all active client companies.
- Dossier delegated mode opens the same screen scoped to the active company.
- All Sage-like zones are present:
  - toolbar actions;
  - five KPI cards;
  - filters;
  - table columns;
  - totals and pagination;
  - detail panel;
  - attachments area;
  - quick actions;
  - history.
- Status counts and amounts match table data.
- `Ouvrir la declaration` navigates to the correct existing screen.
- `Marquer comme deposee` updates `Date depot`, status and history.
- `Saisir le paiement` updates `Date paiement`, status and history.
- `Joindre un document` stores and displays a tenant-scoped attachment.
- Existing VAT declaration, TEJ export, NCT statements, firm dashboard and sidebar tests remain green.

---

## 13. External validation notes

The fiscal deadlines should be validated against official/current references before production activation. Useful references checked during planning:

- JIBAYA agenda fiscal: `https://jibaya.tn/agenda/`
- JIBAYA espace professionnel agenda examples: `https://jibaya.tn/espace-professionnel/`
- Ministere des Finances documents/forms portal: `https://www.finances.gov.tn/fr/document/declarations-de-lacompte-provisionnel`

Do not hard-code these pages as runtime dependencies in v1. Use them as legal validation inputs for configurable rules.

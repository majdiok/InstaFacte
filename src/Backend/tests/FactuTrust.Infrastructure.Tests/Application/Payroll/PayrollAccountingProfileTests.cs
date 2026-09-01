using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Réglage d'imputation comptable de la paie par dossier : invariants du domaine, résolution du
/// profil sur la période du cycle, et carte de comptes qui en découle.
/// </summary>
public sealed class PayrollAccountingProfileTests
{
    // ── Invariants de l'entité ──────────────────────────────────────────────────────────────

    [Fact]
    public void Sce2026_WithoutEffectiveDate_IsRejected()
    {
        // Sans date de bascule, le profil s'appliquerait aussi aux cycles déjà arrêtés : rouvrir
        // puis revalider l'un d'eux en réécrirait l'imputation.
        var result = PayrollAccountingSettings.Create(
            PayrollAccountProfile.Sce2026, null, null, false, false);

        Assert.True(result.IsFailure);
        Assert.Equal("AccountProfileEffectiveDate", result.Error.Code.Split('.')[^1]);
    }

    [Fact]
    public void Legacy_WithoutEffectiveDate_IsAccepted()
    {
        var result = PayrollAccountingSettings.Create(
            PayrollAccountProfile.Legacy, null, null, false, false);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AccountProfileEffectiveDate);
    }

    [Fact]
    public void EffectiveDate_MustBeFirstDayOfMonth()
    {
        var mid = PayrollAccountingSettings.Create(
            PayrollAccountProfile.Sce2026, new DateTime(2026, 10, 15), null, false, false);
        Assert.True(mid.IsFailure);

        var first = PayrollAccountingSettings.Create(
            PayrollAccountProfile.Sce2026, new DateTime(2026, 10, 1), null, false, false);
        Assert.True(first.IsSuccess);
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("8286")]      // classe 8 : hors plan NCT 01
    [InlineData("4286-1")]
    [InlineData("428 6")]
    public void InKindOffsetAccount_MustBeAnSceNumber(string account)
    {
        var result = PayrollAccountingSettings.Create(
            PayrollAccountProfile.Legacy, null, account, false, false);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("4286")]
    [InlineData("421.1")]
    [InlineData("6400")]
    public void InKindOffsetAccount_AcceptsSceNumbers(string account)
    {
        var result = PayrollAccountingSettings.Create(
            PayrollAccountProfile.Legacy, null, account, false, false);

        Assert.True(result.IsSuccess);
        Assert.Equal(account, result.Value.InKindOffsetAccount);
    }

    [Fact]
    public void Update_AppliesAllFields()
    {
        var settings = PayrollAccountingSettings.Create(
            PayrollAccountProfile.Legacy, null, null, false, false).Value;

        var update = settings.Update(
            PayrollAccountProfile.Sce2026, new DateTime(2026, 9, 1), "4286", true, true);

        Assert.True(update.IsSuccess);
        Assert.Equal(PayrollAccountProfile.Sce2026, settings.AccountProfile);
        Assert.Equal(new DateTime(2026, 9, 1), settings.AccountProfileEffectiveDate);
        Assert.Equal("4286", settings.InKindOffsetAccount);
        Assert.True(settings.DisbursementEntriesEnabled);
        Assert.True(settings.DetailedSalarySplitEnabled);
    }

    // ── Résolution du profil sur la période du cycle ─────────────────────────────────────────

    [Theory]
    [InlineData(2026, 8, PayrollAccountProfile.Legacy)]   // se termine le 31/08, avant la bascule
    [InlineData(2026, 9, PayrollAccountProfile.Sce2026)]  // mois de bascule
    [InlineData(2026, 12, PayrollAccountProfile.Sce2026)]
    public void ResolveForPeriod_HonoursEffectiveDate(int year, int month, PayrollAccountProfile expected)
    {
        var snapshot = Snapshot(PayrollAccountProfile.Sce2026, new DateTime(2026, 9, 1));

        Assert.Equal(expected, snapshot.ResolveForPeriod(year, month));
    }

    [Fact]
    public void ResolveForPeriod_WithoutEffectiveDate_AppliesToEveryPeriod()
    {
        var snapshot = Snapshot(PayrollAccountProfile.Sce2026, null);

        Assert.Equal(PayrollAccountProfile.Sce2026, snapshot.ResolveForPeriod(2020, 1));
        Assert.Equal(PayrollAccountProfile.Sce2026, snapshot.ResolveForPeriod(2030, 12));
    }

    // ── Carte de comptes ────────────────────────────────────────────────────────────────────

    [Fact]
    public void AccountMap_UnderLegacy_KeepsHistoricalAccounts()
    {
        var map = Snapshot(PayrollAccountProfile.Legacy, null).BuildAccountMap(PayrollAccountProfile.Legacy);

        // Le profil historique doit rester identique à l'octet près : compensation AN et retenues
        // non typées au 421, comme avant l'introduction du profil SCE.
        Assert.Equal("421", map.InKindBenefitOffsetAccount);
        Assert.Equal("421", map.DefaultOtherAccount);
        Assert.Equal("421", map.ResolveCreditAccount(DeductionKind.Other));
        Assert.Equal("421", map.ResolveCreditAccount(DeductionKind.Advance));
    }

    [Fact]
    public void AccountMap_UnderSce2026_MovesNonAdvanceDeductionsTo4286()
    {
        var map = Snapshot(PayrollAccountProfile.Sce2026, new DateTime(2026, 9, 1))
            .BuildAccountMap(PayrollAccountProfile.Sce2026);

        // 421 est une créance sur le salarié : y créditer une retenue non typée ou la compensation
        // d'un avantage en nature y laisserait un solde créditeur permanent.
        Assert.Equal("4286", map.InKindBenefitOffsetAccount);
        Assert.Equal("4286", map.DefaultOtherAccount);
        Assert.Equal("4286", map.ResolveCreditAccount(DeductionKind.Other));

        // Les avances, elles, éteignent bien la créance 421.
        Assert.Equal("421", map.ResolveCreditAccount(DeductionKind.Advance));
        Assert.Equal("427", map.ResolveCreditAccount(DeductionKind.Garnishment));
    }

    [Fact]
    public void AccountMap_UsesTenantOverrideForInKindOffset()
    {
        var snapshot = Snapshot(PayrollAccountProfile.Sce2026, new DateTime(2026, 9, 1)) with
        {
            InKindOffsetAccount = "4586"
        };

        Assert.Equal("4586", snapshot.BuildAccountMap(PayrollAccountProfile.Sce2026).InKindBenefitOffsetAccount);
    }

    // ── Repli sur la configuration globale ──────────────────────────────────────────────────

    [Fact]
    public void GlobalSettings_ProjectToSnapshotUnchanged()
    {
        var settings = new AccountingSettings
        {
            PayrollAccountProfile = PayrollAccountProfile.Sce2026,
            PayrollAccountProfileEffectiveDate = new DateTime(2026, 9, 1),
            PayrollDisbursementEntriesEnabled = true
        };

        var snapshot = settings.ToPayrollProfileSnapshot();

        Assert.Equal(PayrollAccountProfile.Sce2026, snapshot.Profile);
        Assert.Equal(new DateTime(2026, 9, 1), snapshot.EffectiveDate);
        Assert.Equal(settings.PayrollInKindOffsetAccount, snapshot.InKindOffsetAccount);
        Assert.Equal(settings.PayrollEmployeeLoansAccount, snapshot.LoansAccount);
        Assert.Equal(settings.PayrollGarnishmentsAccount, snapshot.GarnishmentsAccount);
        Assert.True(snapshot.DisbursementEntriesEnabled);
        Assert.False(snapshot.IsTenantOverride);
    }

    [Fact]
    public void GlobalDefault_IsLegacyWith4286Offset()
    {
        var snapshot = new AccountingSettings().ToPayrollProfileSnapshot();

        // Défaut inchangé : aucun dossier ne bascule tout seul.
        Assert.Equal(PayrollAccountProfile.Legacy, snapshot.Profile);
        // Le compte de compensation par défaut a quitté la branche « État » (4386) pour « Personnel ».
        Assert.Equal("4286", snapshot.InKindOffsetAccount);
    }

    private static PayrollAccountingProfileSnapshot Snapshot(
        PayrollAccountProfile profile, DateTime? effectiveDate) =>
        new AccountingSettings
        {
            PayrollAccountProfile = profile,
            PayrollAccountProfileEffectiveDate = effectiveDate
        }.ToPayrollProfileSnapshot();
}

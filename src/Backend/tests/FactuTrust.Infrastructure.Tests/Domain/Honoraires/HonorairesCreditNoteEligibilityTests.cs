using FactuTrust.Domain.Entities.Honoraires;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Honoraires;

public sealed class HonorairesCreditNoteEligibilityTests
{
  [Theory]
  [InlineData(HonorairesInvoiceStatus.Draft, false)]
  [InlineData(HonorairesInvoiceStatus.Validated, true)]
  [InlineData(HonorairesInvoiceStatus.Paid, true)]
  [InlineData(HonorairesInvoiceStatus.PartiallyPaid, true)]
  [InlineData(HonorairesInvoiceStatus.Cancelled, false)]
  public void FinalizedInvoiceStatuses_AreEligibleForCreditNote(HonorairesInvoiceStatus status, bool eligible)
  {
    var isEligible = status is HonorairesInvoiceStatus.Validated
      or HonorairesInvoiceStatus.Paid
      or HonorairesInvoiceStatus.PartiallyPaid;
    Assert.Equal(eligible, isEligible);
  }

  [Fact]
  public void CreateCreditNoteDraft_RequiresLinkedInvoice()
  {
    var result = HonorairesInvoice.CreateDraft(
      Guid.NewGuid(),
      "Client",
      DateTime.UtcNow,
      type: HonorairesDocumentType.CreditNote,
      linkedInvoiceId: null);
    Assert.True(result.IsFailure);
  }

  [Fact]
  public void CreateCreditNote_FromValidatedInvoice_CopiesNegativeTotals()
  {
    var sourceId = Guid.NewGuid();
    var source = HonorairesInvoice.CreateDraft(Guid.NewGuid(), "Client SARL", DateTime.UtcNow).Value;
    source.AddLine("Mission", null, 1m, Money.Create(500m), VatRate.Standard);
    source.AssignNumber("FAC-2026-010");
    source.Validate();

    var credit = HonorairesInvoice.CreateCreditNoteDraft(
      source.FirmClientAssignmentId, source.ClientName, sourceId, DateTime.UtcNow).Value;
    var replace = credit.ReplaceLines(new (string? ActivityCode, string Designation, string? Description, decimal Quantity, Money UnitPrice, VatRate VatRate, decimal? DiscountPercent)[]
    {
      (null, "Mission", null, 1m, Money.Create(500m), VatRate.Standard, null)
    });
    Assert.True(replace.IsSuccess);
    Assert.Equal(HonorairesDocumentType.CreditNote, credit.Type);
    Assert.True(credit.TotalAmount.Amount < 0);
  }
}

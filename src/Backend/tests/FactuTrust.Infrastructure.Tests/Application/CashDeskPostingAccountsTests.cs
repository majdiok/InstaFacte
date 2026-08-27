using FactuTrust.Application.Features.Accounting;
using FactuTrust.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Vérifie ligne à ligne le mapping catégorie → compte NCT 01 de <see cref="CashDeskPostingAccounts"/>
/// (table §3 du plan). Fonctions pures, aucun mock nécessaire.
/// </summary>
public sealed class CashDeskPostingAccountsTests
{
    [Theory]
    [InlineData(CashExpenseCategory.RentPayment, false, "613")]
    [InlineData(CashExpenseCategory.SuppliesAndConsumables, false, "606")]
    [InlineData(CashExpenseCategory.MaintenanceAndRepair, false, "615")]
    [InlineData(CashExpenseCategory.NetSalaries, false, "640")]
    [InlineData(CashExpenseCategory.NetSalaries, true, "425")]
    [InlineData(CashExpenseCategory.TransportCosts, false, "624")]
    [InlineData(CashExpenseCategory.TravelAndTrips, false, "6251")]
    [InlineData(CashExpenseCategory.VehicleRepairMaintenance, false, "615")]
    [InlineData(CashExpenseCategory.VehicleRentalAndTransport, false, "613")]
    [InlineData(CashExpenseCategory.UtilitiesAndEnergy, false, "606")]
    [InlineData(CashExpenseCategory.ProfessionalFees, false, "622")]
    [InlineData(CashExpenseCategory.Insurance, false, "616")]
    [InlineData(CashExpenseCategory.TaxesAndDuties, false, "6651")]
    [InlineData(CashExpenseCategory.MarketingAdvertising, false, "623")]
    [InlineData(CashExpenseCategory.ITAndSoftware, false, "604")]
    [InlineData(CashExpenseCategory.SupplierInvoicePayment, false, "4011")]
    [InlineData(CashExpenseCategory.Other, false, "63")]
    public void ExpenseAccount_ReturnsExpectedAccount(CashExpenseCategory category, bool payrollCycleExists, string expected)
    {
        CashDeskPostingAccounts.ExpenseAccount(category, payrollCycleExists).Should().Be(expected);
    }

    [Fact]
    public void ExpenseAccount_BankDeposit_ThrowsArgumentOutOfRangeException()
    {
        var act = () => CashDeskPostingAccounts.ExpenseAccount(CashExpenseCategory.BankDeposit, false);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(CashRevenueCategory.CashSalesReceipt, "707")]
    [InlineData(CashRevenueCategory.ClientReceivablesReceipt, "4111")]
    [InlineData(CashRevenueCategory.PartnerContributionsReceipt, "446")]
    [InlineData(CashRevenueCategory.BankCreditReceipt, "5321")]
    [InlineData(CashRevenueCategory.Other, "73")]
    public void RevenueAccount_ReturnsExpectedAccount(CashRevenueCategory category, string expected)
    {
        CashDeskPostingAccounts.RevenueAccount(category).Should().Be(expected);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, "5411")]
    [InlineData(PaymentMethod.BankTransfer, "5321")]
    [InlineData(PaymentMethod.Check, "5321")]
    [InlineData(PaymentMethod.Card, "5321")]
    [InlineData(PaymentMethod.MobilePayment, "5321")]
    [InlineData(PaymentMethod.Other, "5321")]
    public void TreasuryAccount_ReturnsExpectedAccount(PaymentMethod method, string expected)
    {
        CashDeskPostingAccounts.TreasuryAccount(method).Should().Be(expected);
    }

    [Fact]
    public void TreasuryAccount_Traite_ThrowsArgumentOutOfRangeException()
    {
        var act = () => CashDeskPostingAccounts.TreasuryAccount(PaymentMethod.Traite);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

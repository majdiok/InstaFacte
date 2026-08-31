using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

/// <summary>
/// R-29 (repository) : <see cref="EmployeeRepository.GetActiveOrTerminatedInMonthAsync"/> inclut les
/// salariés actifs ET ceux partis en cours du mois (TerminationDate dans le mois), pourvu qu'un
/// contrat couvre au moins un jour du mois. Exclut les départs antérieurs et les contrats hors mois.
/// </summary>
public sealed class EmployeeRepositoryTerminatedInMonthTests
{
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly EmployeeRepository _repository;

    public EmployeeRepositoryTerminatedInMonthTests()
    {
        _contextFactory = new TestTenantDbContextFactory($"TestDb_EmpTerm_{Guid.NewGuid()}");
        _repository = new EmployeeRepository(_contextFactory);
        Seed();
    }

    private void Seed()
    {
        // 1. Salarié actif, contrat ouvert couvrant le mois → inclus.
        var active = Employee.Create("E-ACTIVE", "Active", "Un", new DateTime(2025, 1, 1)).Value;
        Assert.True(active.AddContract(
            ContractType.Cdi, SocialRegime.Rsna, new DateTime(2025, 1, 1), 2000m, 0.4m, endDate: null).IsSuccess);
        _repository.AddAsync(active).GetAwaiter().GetResult();

        // 2. Salarié parti en cours du mois (R-29) → inclus.
        var termInMonth = Employee.Create("E-TERMID", "Term", "InMonth", new DateTime(2025, 1, 1)).Value;
        Assert.True(termInMonth.AddContract(
            ContractType.Cdi, SocialRegime.Rsna, new DateTime(2025, 1, 1), 2000m, 0.4m,
            endDate: new DateTime(2026, 1, 15)).IsSuccess);
        termInMonth.Terminate(new DateTime(2026, 1, 15));
        _repository.AddAsync(termInMonth).GetAwaiter().GetResult();

        // 3. Salarié parti le mois précédent → exclu (TerminationDate hors du mois).
        var termPrev = Employee.Create("E-TERMPREV", "Term", "Prev", new DateTime(2024, 1, 1)).Value;
        Assert.True(termPrev.AddContract(
            ContractType.Cdi, SocialRegime.Rsna, new DateTime(2024, 1, 1), 2000m, 0.4m,
            endDate: new DateTime(2025, 12, 15)).IsSuccess);
        termPrev.Terminate(new DateTime(2025, 12, 15));
        _repository.AddAsync(termPrev).GetAwaiter().GetResult();

        // 4. Salarié actif mais contrat commençant le mois suivant → exclu (aucun contrat couvrant).
        var future = Employee.Create("E-FUTURE", "Future", "Start", new DateTime(2026, 2, 1)).Value;
        Assert.True(future.AddContract(
            ContractType.Cdi, SocialRegime.Rsna, new DateTime(2026, 2, 1), 2000m, 0.4m, endDate: null).IsSuccess);
        _repository.AddAsync(future).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetActiveOrTerminatedInMonthAsync_IncludesActiveAndTerminatedInMonth_ExcludesOthers()
    {
        var result = await _repository.GetActiveOrTerminatedInMonthAsync(2026, 1);

        var numbers = result.Select(e => e.EmployeeNumber).ToList();
        Assert.Contains("E-ACTIVE", numbers);
        Assert.Contains("E-TERMID", numbers);
        Assert.DoesNotContain("E-TERMPREV", numbers);
        Assert.DoesNotContain("E-FUTURE", numbers);
    }

    [Fact]
    public async Task GetActiveOrTerminatedInMonthAsync_LoadsContractsForEachEmployee()
    {
        var result = await _repository.GetActiveOrTerminatedInMonthAsync(2026, 1);

        // Le filtre repose sur Contracts.Any(...) : chacun retourné doit avoir son contrat chargé.
        Assert.All(result, e => Assert.NotEmpty(e.Contracts));
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new TenantDbContext(options);
        }
    }
}

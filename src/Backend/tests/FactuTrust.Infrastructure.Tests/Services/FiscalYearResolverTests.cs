using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Tests unitaires du résolveur d'exercice (plan « Exercices décalés », P1) : bascule de mois
/// 1..12, bornes (1er jour du mois de début / fin d'exercice), années bissextiles, libellés.
/// Le résolveur est pur/déterministe — aucun mock nécessaire.
/// </summary>
public sealed class FiscalYearResolverTests
{
    private readonly FiscalYearResolver _resolver = new();

    // ------------------------------------------------------------------
    // FiscalYearKey — bascule selon le mois de début d'exercice
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(7, 2026, 7, 1, 2026)]     // 1er jour du mois de début → exercice courant
    [InlineData(7, 2026, 6, 30, 2025)]     // veille du mois de début (juin) → exercice précédent
    [InlineData(7, 2027, 1, 15, 2026)]     // mois antérieur au début, année suivante → exercice N-1
    [InlineData(7, 2026, 12, 31, 2026)]    // décembre (≥ juillet) → exercice courant
    [InlineData(1, 2026, 1, 1, 2026)]      // civil : 1er janvier → année courante
    [InlineData(1, 2026, 12, 31, 2026)]    // civil : 31 décembre → année courante
    [InlineData(4, 2026, 4, 1, 2026)]      // exercice avril : 1er avril → exercice 2026
    [InlineData(4, 2026, 3, 31, 2025)]     // exercice avril : 31 mars → exercice 2025
    public void FiscalYearKey_Boundaries_ShouldResolveStartYear(
        int startMonth, int dateYear, int dateMonth, int dateDay, int expectedKey)
    {
        var date = new DateTime(dateYear, dateMonth, dateDay);
        Assert.Equal(expectedKey, _resolver.FiscalYearKey(date, startMonth));
    }

    [Fact]
    public void FiscalYearKey_JulyStart_AllMonths_ShouldSwitchCorrectly()
    {
        // Exercice démarrant en juillet : juillet→décembre = exercice N, janvier→juin = exercice N-1.
        for (var month = 1; month <= 12; month++)
        {
            var date = new DateTime(2026, month, 15);
            var key = _resolver.FiscalYearKey(date, 7);
            if (month >= 7)
                Assert.Equal(2026, key);
            else
                Assert.Equal(2025, key);
        }
    }

    [Fact]
    public void FiscalYearKey_CivilStart_AlwaysEqualsCalendarYear()
    {
        for (var month = 1; month <= 12; month++)
        {
            var date = new DateTime(2026, month, 15);
            Assert.Equal(2026, _resolver.FiscalYearKey(date, 1));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void FiscalYearKey_InvalidStartMonth_ShouldThrow(int startMonth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _resolver.FiscalYearKey(new DateTime(2026, 6, 15), startMonth));
    }

    // ------------------------------------------------------------------
    // Bornes calendaires (début / fin d'exercice)
    // ------------------------------------------------------------------

    [Fact]
    public void FiscalYearStartDateTime_JulyStart_ShouldBeFirstOfJuly()
    {
        Assert.Equal(new DateTime(2026, 7, 1), _resolver.FiscalYearStartDateTime(2026, 7));
    }

    [Fact]
    public void FiscalYearEndDateTime_CivilStart_ShouldBeDecember31()
    {
        Assert.Equal(new DateTime(2026, 12, 31), _resolver.FiscalYearEndDateTime(2026, 1));
    }

    [Fact]
    public void FiscalYearEndDateTime_JulyStart_ShouldBeJune30NextYear()
    {
        // Exercice 2026 (juil. 2026 → juin 2027) : fin = 30/06/2027.
        Assert.Equal(new DateTime(2027, 6, 30), _resolver.FiscalYearEndDateTime(2026, 7));
    }

    [Fact]
    public void FiscalYearEndDateTime_MarchStart_LeapYear_ShouldBeFebruary29()
    {
        // Exercice démarrant en mars : fin = fin février de l'année suivante.
        // Exercice 2023 (mar. 2023 → fév. 2024) : 2024 est bissextile → 29/02/2024.
        Assert.Equal(new DateTime(2024, 2, 29), _resolver.FiscalYearEndDateTime(2023, 3));
    }

    [Fact]
    public void FiscalYearEndDateTime_MarchStart_NonLeapYear_ShouldBeFebruary28()
    {
        // Exercice 2022 (mar. 2022 → fév. 2023) : 2023 n'est pas bissextile → 28/02/2023.
        Assert.Equal(new DateTime(2023, 2, 28), _resolver.FiscalYearEndDateTime(2022, 3));
    }

    [Fact]
    public void FiscalYearStartAndEnd_ShouldBeContiguousAcrossExercises()
    {
        // La fin d'un exercice est la veille du début du suivant (frontière sans trou ni chevauchement).
        var end = _resolver.FiscalYearEndDateTime(2026, 7);
        var nextStart = _resolver.FiscalYearStartDateTime(2027, 7);
        Assert.Equal(end.AddDays(1), nextStart);
    }

    // ------------------------------------------------------------------
    // Libellés (décision D2)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("N/N+1")]
    [InlineData("N")]
    public void FiscalYearLabel_CivilStart_AlwaysReturnsSingleYear(string format)
    {
        // Exercice civil (janvier) → toujours « N » quelle que soit la valeur du format.
        Assert.Equal("2026", _resolver.FiscalYearLabel(2026, 1, format));
    }

    [Fact]
    public void FiscalYearLabel_OffsetStart_Nn1Format_ShouldReturnSlashRange()
    {
        Assert.Equal("2026/2027", _resolver.FiscalYearLabel(2026, 7, "N/N+1"));
    }

    [Fact]
    public void FiscalYearLabel_OffsetStart_NFormat_ShouldReturnStartYearOnly()
    {
        Assert.Equal("2026", _resolver.FiscalYearLabel(2026, 7, "N"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("n/n+1")]
    public void FiscalYearLabel_OffsetStart_UnknownFormat_ShouldDefaultToNn1(string format)
    {
        // Toute valeur de format non reconnue (hors « N ») est traitée comme « N/N+1 ».
        Assert.Equal("2026/2027", _resolver.FiscalYearLabel(2026, 7, format));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void FiscalYearLabel_InvalidStartMonth_ShouldThrow(int startMonth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _resolver.FiscalYearLabel(2026, startMonth, "N/N+1"));
    }
}

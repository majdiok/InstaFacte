using System.Reflection;
using FactuTrust.Application.Common;
using Microsoft.Data.SqlClient;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Common;

public sealed class SqlExceptionHelperTests
{
    [Fact]
    public void FindSqlException_ReturnsSqlException_FromInnerChain()
    {
        var sqlException = SqlExceptionTestFactory.Create(207, "Invalid column name 'CreatedBy'.");
        var wrapped = new InvalidOperationException("outer", new Exception("middle", sqlException));

        var found = SqlExceptionHelper.FindSqlException(wrapped);

        Assert.NotNull(found);
        Assert.Equal(207, found!.Number);
    }

    [Fact]
    public void IsSchemaDrift_ReturnsTrue_ForInvalidColumnName()
    {
        var sqlException = SqlExceptionTestFactory.Create(207, "Invalid column name 'Version'.");
        var wrapped = new Exception("save failed", sqlException);

        Assert.True(SqlExceptionHelper.IsSchemaDrift(wrapped));
    }

    [Fact]
    public void IsSchemaDrift_ReturnsTrue_ForInvalidObjectName()
    {
        var sqlException = SqlExceptionTestFactory.Create(208, "Invalid object name 'Invoices'.");
        var wrapped = new Exception("save failed", sqlException);

        Assert.True(SqlExceptionHelper.IsSchemaDrift(wrapped));
    }

    [Fact]
    public void IsSchemaDrift_ReturnsFalse_ForOtherSqlErrors()
    {
        var sqlException = SqlExceptionTestFactory.Create(2627, "Violation of UNIQUE KEY constraint.");
        var wrapped = new Exception("save failed", sqlException);

        Assert.False(SqlExceptionHelper.IsSchemaDrift(wrapped));
    }

    [Fact]
    public void IsSchemaDrift_ReturnsTrue_WhenWrappedInDbUpdateException()
    {
        var sqlException = SqlExceptionTestFactory.Create(207, "Invalid column name 'CreatedBy'.");
        var dbUpdate = new Microsoft.EntityFrameworkCore.DbUpdateException("update failed", sqlException);

        Assert.True(SqlExceptionHelper.IsSchemaDrift(dbUpdate));
    }
}

internal static class SqlExceptionTestFactory
{
    public static SqlException Create(int number, string message)
    {
        var errorCollection = (SqlErrorCollection)Activator.CreateInstance(
            typeof(SqlErrorCollection),
            nonPublic: true)!;

        var error = (SqlError)Activator.CreateInstance(
            typeof(SqlError),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [number, (byte)2, (byte)0, string.Empty, message, string.Empty, 0, (uint)0, null],
            culture: null)!;

        typeof(SqlErrorCollection)
            .GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(errorCollection, [error]);

        return (SqlException)Activator.CreateInstance(
            typeof(SqlException),
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            args: [message, errorCollection, null, Guid.NewGuid()],
            culture: null)!;
    }
}

using Microsoft.Data.SqlClient;

namespace FactuTrust.Application.Common;

/// <summary>
/// Walks exception chains to detect SQL Server schema drift (missing columns / tables).
/// </summary>
public static class SqlExceptionHelper
{
  /// <summary>SQL Server « Invalid column name » — column of the EF model missing from the database.</summary>
  public const int InvalidColumnName = 207;

  /// <summary>SQL Server « Invalid object name » — table of the EF model missing from the database.</summary>
  public const int InvalidObjectName = 208;

  /// <summary>
  /// SQL Server error numbers for invalid column name (207) and invalid object name (208).
  /// </summary>
  public static bool IsSchemaDrift(Exception exception) =>
      FindSqlException(exception) is { Number: InvalidColumnName or InvalidObjectName };

  public static SqlException? FindSqlException(Exception? exception)
  {
    var current = exception;
    while (current is not null)
    {
      if (current is SqlException sqlException)
        return sqlException;

      current = current.InnerException;
    }

    return null;
  }
}

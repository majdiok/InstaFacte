using Microsoft.Data.SqlClient;

namespace FactuTrust.Application.Common;

/// <summary>
/// Walks exception chains to detect SQL Server schema drift (missing columns / tables).
/// </summary>
public static class SqlExceptionHelper
{
  /// <summary>
  /// SQL Server error numbers for invalid column name (207) and invalid object name (208).
  /// </summary>
  public static bool IsSchemaDrift(Exception exception) =>
      FindSqlException(exception) is { Number: 207 or 208 };

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

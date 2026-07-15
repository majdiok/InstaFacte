namespace FactuTrust.Application.DTOs;

/// <summary>
/// Paginated response wrapper.
/// </summary>
public sealed record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

/// <summary>
/// API response wrapper.
/// </summary>
public sealed record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    /// <summary>Optional stable code for clients/support (e.g. TENANT_MIGRATION_FAILED).</summary>
    public string? Code { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ApiResponse<T> Ok(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static ApiResponse<T> Fail(string error, string? code = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = error,
            Errors = new[] { error },
            Code = code
        };
    }

    public static ApiResponse<T> Fail(IEnumerable<string> errors, string? code = null)
    {
        var errorsList = errors.ToArray();
        return new ApiResponse<T>
        {
            Success = false,
            Message = errorsList.Length > 0 ? errorsList[0] : null,
            Errors = errorsList,
            Code = code
        };
    }
}

/// <summary>
/// Simple key-value option for dropdowns.
/// </summary>
public sealed record SelectOption
{
    public string Value { get; init; } = null!;
    public string Label { get; init; } = null!;
    public bool Disabled { get; init; }
}

/// <summary>
/// Dashboard statistics DTO.
/// </summary>
public sealed record DashboardStatsDto
{
    public int TotalInvoicesThisMonth { get; init; }
    public int PendingInvoices { get; init; }
    public int OverdueInvoices { get; init; }
    public decimal TotalRevenueThisMonth { get; init; }
    public decimal TotalOutstanding { get; init; }
    public int TotalClients { get; init; }
    public IReadOnlyList<RecentInvoiceDto> RecentInvoices { get; init; } = Array.Empty<RecentInvoiceDto>();
    public IReadOnlyList<MonthlyRevenueDto> MonthlyRevenue { get; init; } = Array.Empty<MonthlyRevenueDto>();
}

/// <summary>
/// Recent invoice for dashboard.
/// </summary>
public sealed record RecentInvoiceDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public string ClientName { get; init; } = null!;
    public decimal Amount { get; init; }
    public string Status { get; init; } = null!;
    public DateTime Date { get; init; }
}

/// <summary>
/// Monthly revenue for charts.
/// </summary>
public sealed record MonthlyRevenueDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string MonthName { get; init; } = null!;
    public decimal Revenue { get; init; }
    public int InvoiceCount { get; init; }
}

namespace AttendanceSystem.DTOs;

public record ApiResponse<T>(bool Success, T? Data = default, string? Message = null, string? ErrorCode = null, object? Errors = null)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);
    public static ApiResponse<T> Fail(string errorCode, string message, object? data = null, object? errors = null)
        => new(false, (T?)data, message, errorCode, errors);
}

public record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

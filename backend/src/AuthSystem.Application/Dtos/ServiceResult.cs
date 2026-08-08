namespace AuthSystem.Application.Dtos;

public class ServiceResult
{
    public bool Succeeded { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static ServiceResult Success() => new() { Succeeded = true };
    public static ServiceResult Failure(params IReadOnlyList<string> errors) => new() { Succeeded = false, Errors = errors };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Success(T data) => new() { Succeeded = true, Data = data };
    public static new ServiceResult<T> Failure(params IReadOnlyList<string> errors) => new() { Succeeded = false, Errors = errors };
}

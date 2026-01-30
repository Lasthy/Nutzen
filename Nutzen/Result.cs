using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Nutzen;

public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string ErrorMessage => FriendlyErrors.Length > 0 ? string.Join("; ", FriendlyErrors) : string.Empty;
    public string[] FriendlyErrors { get; set; } = Array.Empty<string>();
    [JsonIgnore]
    public string TecnicalError { get; set; } = null!;

    public T? Value { get; }

    protected Result(bool isSuccess, T? value = default, string[] friendlyErrors = default!, string tecnicalError = null!)
    {
        IsSuccess = isSuccess;
        FriendlyErrors = friendlyErrors ?? Array.Empty<string>();
        TecnicalError = tecnicalError;
        Value = value;
    }

    public static Result<T> Success(T value)
    {
        return new Result<T>(true, value);
    }

    public static Result<T> Failure(string technicalError)
    {
        return new Result<T>(false, default, tecnicalError: technicalError);
    }

    public static Result<T> Failure(string[] friendlyErrors, string technicalError = null!)
    {
        return new Result<T>(false, default, friendlyErrors, technicalError);
    }

    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }
}

public class Result : Result<Empty>
{
    protected Result(bool isSuccess, string[] friendlyErrors = default!, string tecnicalError = null!) : base(isSuccess, default, friendlyErrors, tecnicalError)
    {

    }

    public static Result Success()
    {
        return new Result(true);
    }

    public static new Result Failure(string technicalError)
    {
        return new Result(false, tecnicalError: technicalError);
    }

    public static new Result Failure(string[] friendlyErrors, string technicalError = null!)
    {
        return new Result(false, friendlyErrors, technicalError);
    }
}
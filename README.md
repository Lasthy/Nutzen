# Nutzen

A lightweight .NET library for implementing the Unit of Work pattern with request/response handling, including Roslyn analyzers for compile-time validation.

## Installation

```bash
dotnet add package Nutzen
```

## Features

- **Unit of Work Pattern**: Structured approach to organizing business logic
- **Request/Handler Pattern**: Clean separation of concerns with `IRequest` and `IRequestHandler`
- **Interceptable Handlers**: Built-in support for request interceptors (middleware pattern)
- **Dispatcher**: Central dispatching mechanism for requests
- **Roslyn Analyzers**: Compile-time validation to ensure correct implementation

## Quick Start

### Define a Unit of Work

```csharp
using Nutzen;

[UnitOfWork]
public static class CreateUser
{
    [Request]
    public record Request : Nutzen.Request
    {
        public string Name { get; init; }
        public string Email { get; init; }
    }

    [Handler]
    public class Handler : InterceptableRequestHandler<Request>
    {
        protected override Task<Result<Empty>> Operation(Request request)
        {
            // Your business logic here
            return Task.FromResult(Result<Empty>.Success(default));
        }
    }
}
```

### With a Response Type

```csharp
using Nutzen;

[UnitOfWork]
public static class GetUser
{
    public record UserResponse(int Id, string Name, string Email);

    [Request]
    public record Request : Nutzen.Request<UserResponse>
    {
        public int UserId { get; init; }
    }

    [Handler]
    public class Handler : InterceptableRequestHandler<Request, UserResponse>
    {
        protected override Task<Result<UserResponse>> Operation(Request request)
        {
            var user = new UserResponse(request.UserId, "John", "john@example.com");
            return Task.FromResult(Result<UserResponse>.Success(user));
        }
    }
}
```

### Dispatch Requests

```csharp
// Register in DI container
services.AddScoped<IDispatcher, Dispatcher>();
services.AddScoped<IRequestHandler<CreateUser.Request, Empty>, CreateUser.Handler>();

// Use the dispatcher
var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
var result = await dispatcher.DispatchAsync<CreateUser.Request>(new CreateUser.Request
{
    Name = "John",
    Email = "john@example.com"
});
```

## Analyzers

The package includes Roslyn analyzers that enforce the Unit of Work pattern at compile time:

| Code | Description |
|------|-------------|
| UOW001 | Class with `[UnitOfWork]` must be static |
| UOW002 | Class with `[UnitOfWork]` must have an inner record with `[Request]` inheriting from `Request` |
| UOW003 | Class with `[UnitOfWork]` must have an inner class with `[Handler]` implementing `IRequestHandler` |

All rules include automatic code fixes to help you quickly resolve issues.

## License

MIT

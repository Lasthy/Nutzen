using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = Nutzen.Analyzers.UnitOfWork.Test.CSharpCodeFixVerifier<
    Nutzen.Analyzers.UnitOfWork.NutzenAnalyzersUnitOfWorkAnalyzer,
    Nutzen.Analyzers.UnitOfWork.NutzenAnalyzersUnitOfWorkCodeFixProvider>;

namespace Nutzen.Analyzers.UnitOfWork.Test
{
    [TestClass]
    public class NutzenAnalyzersUnitOfWorkUnitTest
    {
        private const string NutzenTypes = @"
namespace Nutzen
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class UnitOfWorkAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class RequestAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class HandlerAttribute : System.Attribute { }

    public record Request { }
    public record Request<TResponse> { }

    public struct Empty { }
    public struct Result<T> { }

    public interface IRequest<TResponse> { }
    public interface IRequest : IRequest<Empty> { }

    public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse> { }
    public interface IRequestHandler<TRequest> : IRequestHandler<TRequest, Empty> where TRequest : IRequest { }

    public abstract class InterceptableRequestHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse> 
    {
        protected abstract System.Threading.Tasks.Task<Result<TResponse>> Operation(TRequest request);
    }

    public abstract class InterceptableRequestHandler<TRequest> : InterceptableRequestHandler<TRequest, Empty>
        where TRequest : IRequest { }
}
";

        // No diagnostics expected for class without UnitOfWorkAttribute
        [TestMethod]
        public async Task NoDiagnostic_WhenClassHasNoUnitOfWorkAttribute()
        {
            var test = NutzenTypes + @"
namespace TestNamespace
{
    public class RegularClass
    {   
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // No diagnostics expected for a valid UnitOfWork class
        [TestMethod]
        public async Task NoDiagnostic_WhenUnitOfWorkIsValid()
        {
            var test = NutzenTypes + @"
namespace TestNamespace
{
    [Nutzen.UnitOfWork]
    public static class ValidUnitOfWork
    {
        [Nutzen.Request]
        public record Request : Nutzen.Request;

        [Nutzen.Handler]
        public class Handler : Nutzen.InterceptableRequestHandler<Request>
        {
            protected override System.Threading.Tasks.Task<Nutzen.Result<Nutzen.Empty>> Operation(Request request)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // UOW001: Class with UnitOfWorkAttribute must be static
        [TestMethod]
        public async Task Diagnostic_WhenUnitOfWorkClassIsNotStatic()
        {
            var test = NutzenTypes + @"
namespace TestNamespace
{
    [Nutzen.UnitOfWork]
    public class {|#0:NonStaticUnitOfWork|}
    {
        [Nutzen.Request]
        public record Request : Nutzen.Request;

        [Nutzen.Handler]
        public class Handler : Nutzen.InterceptableRequestHandler<Request>
        {
            protected override System.Threading.Tasks.Task<Nutzen.Result<Nutzen.Empty>> Operation(Request request)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}";

            var expected = VerifyCS.Diagnostic(NutzenAnalyzersUnitOfWorkAnalyzer.MustBeStaticDiagnosticId)
                .WithLocation(0)
                .WithArguments("NonStaticUnitOfWork");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // UOW002: Class with UnitOfWorkAttribute must have Request inner record
        [TestMethod]
        public async Task Diagnostic_WhenUnitOfWorkMissingRequest()
        {
            var test = NutzenTypes + @"
namespace TestNamespace
{
    [Nutzen.UnitOfWork]
    public static class {|#0:MissingRequestUnitOfWork|}
    {
        [Nutzen.Handler]
        public class Handler : Nutzen.InterceptableRequestHandler<Nutzen.Request>
        {
            protected override System.Threading.Tasks.Task<Nutzen.Result<Nutzen.Empty>> Operation(Nutzen.Request request)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}";

            var expected = VerifyCS.Diagnostic(NutzenAnalyzersUnitOfWorkAnalyzer.MustHaveRequestDiagnosticId)
                .WithLocation(0)
                .WithArguments("MissingRequestUnitOfWork");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // UOW003: Class with UnitOfWorkAttribute must have Handler inner class
        [TestMethod]
        public async Task Diagnostic_WhenUnitOfWorkMissingHandler()
        {
            var test = NutzenTypes + @"
namespace TestNamespace
{
    [Nutzen.UnitOfWork]
    public static class {|#0:MissingHandlerUnitOfWork|}
    {
        [Nutzen.Request]
        public record Request : Nutzen.Request;
    }
}";

            var expected = VerifyCS.Diagnostic(NutzenAnalyzersUnitOfWorkAnalyzer.MustHaveHandlerDiagnosticId)
                .WithLocation(0)
                .WithArguments("MissingHandlerUnitOfWork");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // All three diagnostics when class has only the attribute
        [TestMethod]
        public async Task Diagnostic_WhenUnitOfWorkHasOnlyAttribute()
        {
            var test = NutzenTypes + @"
namespace TestNamespace
{
    [Nutzen.UnitOfWork]
    public class {|#0:IncompleteUnitOfWork|}
    {
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic(NutzenAnalyzersUnitOfWorkAnalyzer.MustBeStaticDiagnosticId)
                    .WithLocation(0)
                    .WithArguments("IncompleteUnitOfWork"),
                VerifyCS.Diagnostic(NutzenAnalyzersUnitOfWorkAnalyzer.MustHaveRequestDiagnosticId)
                    .WithLocation(0)
                    .WithArguments("IncompleteUnitOfWork"),
                VerifyCS.Diagnostic(NutzenAnalyzersUnitOfWorkAnalyzer.MustHaveHandlerDiagnosticId)
                    .WithLocation(0)
                    .WithArguments("IncompleteUnitOfWork")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}

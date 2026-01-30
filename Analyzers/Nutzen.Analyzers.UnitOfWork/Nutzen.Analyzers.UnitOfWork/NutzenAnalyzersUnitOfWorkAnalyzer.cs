using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Nutzen.Analyzers.UnitOfWork
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NutzenAnalyzersUnitOfWorkAnalyzer : DiagnosticAnalyzer
    {
        // Attribute and type names from Nutzen library
        private const string UnitOfWorkAttributeName = "UnitOfWorkAttribute";
        private const string UnitOfWorkAttributeFullName = "Nutzen.UnitOfWorkAttribute";
        private const string RequestAttributeName = "RequestAttribute";
        private const string RequestAttributeFullName = "Nutzen.RequestAttribute";
        private const string HandlerAttributeName = "HandlerAttribute";
        private const string HandlerAttributeFullName = "Nutzen.HandlerAttribute";
        private const string RequestBaseTypeName = "Request";
        private const string RequestBaseTypeFullName = "Nutzen.Request";
        private const string IRequestHandlerInterfaceName = "IRequestHandler";
        private const string IRequestHandlerInterfaceFullName = "Nutzen.IRequestHandler";

        // Diagnostic IDs
        public const string MustBeStaticDiagnosticId = "UOW001";
        public const string MustHaveRequestDiagnosticId = "UOW002";
        public const string MustHaveHandlerDiagnosticId = "UOW003";

        private const string Category = "Design";

        private static readonly DiagnosticDescriptor MustBeStaticRule = new DiagnosticDescriptor(
            MustBeStaticDiagnosticId,
            "UnitOfWork class must be static",
            "Class '{0}' with UnitOfWorkAttribute must be static",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Classes decorated with UnitOfWorkAttribute must be declared as static.");

        private static readonly DiagnosticDescriptor MustHaveRequestRule = new DiagnosticDescriptor(
            MustHaveRequestDiagnosticId,
            "UnitOfWork class must have a Request inner record",
            "Class '{0}' with UnitOfWorkAttribute must have an inner record with RequestAttribute inheriting from Request",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Classes decorated with UnitOfWorkAttribute must contain an inner record with RequestAttribute that inherits from Request.");

        private static readonly DiagnosticDescriptor MustHaveHandlerRule = new DiagnosticDescriptor(
            MustHaveHandlerDiagnosticId,
            "UnitOfWork class must have a Handler inner class",
            "Class '{0}' with UnitOfWorkAttribute must have an inner class with HandlerAttribute implementing IRequestHandler",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Classes decorated with UnitOfWorkAttribute must contain an inner class with HandlerAttribute that implements IRequestHandler.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(MustBeStaticRule, MustHaveRequestRule, MustHaveHandlerRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Only analyze top-level classes (not nested types)
            if (namedTypeSymbol.ContainingType != null)
                return;

            // Check if the class has UnitOfWorkAttribute
            if (!HasAttribute(namedTypeSymbol, UnitOfWorkAttributeName, UnitOfWorkAttributeFullName))
                return;

            // Rule 1: Must be static
            if (!namedTypeSymbol.IsStatic)
            {
                var diagnostic = Diagnostic.Create(MustBeStaticRule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }

            // Rule 2: Must have inner record with RequestAttribute inheriting from Request
            var requestType = FindInnerTypeWithAttribute(namedTypeSymbol, RequestAttributeName, RequestAttributeFullName);
            if (requestType == null || !InheritsFrom(requestType, RequestBaseTypeName, RequestBaseTypeFullName))
            {
                var diagnostic = Diagnostic.Create(MustHaveRequestRule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }

            // Rule 3: Must have inner class with HandlerAttribute implementing IRequestHandler
            var handlerType = FindInnerTypeWithAttribute(namedTypeSymbol, HandlerAttributeName, HandlerAttributeFullName);
            if (handlerType == null || !ImplementsInterface(handlerType, IRequestHandlerInterfaceName, IRequestHandlerInterfaceFullName))
            {
                var diagnostic = Diagnostic.Create(MustHaveHandlerRule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool HasAttribute(INamedTypeSymbol symbol, string attributeName, string attributeFullName)
        {
            return symbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name == attributeName ||
                attr.AttributeClass?.ToDisplayString() == attributeFullName);
        }

        private static INamedTypeSymbol FindInnerTypeWithAttribute(INamedTypeSymbol containingType, string attributeName, string attributeFullName)
        {
            return containingType.GetTypeMembers()
                .FirstOrDefault(member => HasAttribute(member, attributeName, attributeFullName));
        }

        private static bool InheritsFrom(INamedTypeSymbol symbol, string baseTypeName, string baseTypeFullName)
        {
            var baseType = symbol.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == baseTypeName || 
                    baseType.ToDisplayString() == baseTypeFullName ||
                    baseType.OriginalDefinition.ToDisplayString().StartsWith(baseTypeFullName))
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }
            return false;
        }

        private static bool ImplementsInterface(INamedTypeSymbol symbol, string interfaceName, string interfaceFullName)
        {
            return symbol.AllInterfaces.Any(iface =>
                iface.Name == interfaceName ||
                iface.ToDisplayString().StartsWith(interfaceFullName) ||
                iface.OriginalDefinition.ToDisplayString().StartsWith(interfaceFullName));
        }
    }
}

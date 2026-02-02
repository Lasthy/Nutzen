using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Nutzen.Generators.ServiceRegistration;

/// <summary>
/// Source generator that creates service registration infrastructure.
/// - For each class marked with [Interceptor], generates a {ClassName}Attribute
/// - Generates registration code for request handlers with interceptors
/// - Generates registration code for event handlers
/// </summary>
[Generator]
public class ServiceRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes with [Interceptor] attribute
        var interceptorClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsClassWithAttributes(node),
                transform: static (ctx, _) => GetInterceptorClassInfo(ctx))
            .Where(static info => info is not null);

        // Find all request handlers
        var requestHandlerClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsClassDeclaration(node),
                transform: static (ctx, _) => GetRequestHandlerClassInfo(ctx))
            .Where(static info => info is not null);

        // Find all event handlers
        var eventHandlerClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsClassDeclaration(node),
                transform: static (ctx, _) => GetEventHandlerClassInfo(ctx))
            .Where(static info => info is not null);

        // Combine interceptors with compilation for attribute generation
        var compilationAndInterceptors = context.CompilationProvider.Combine(interceptorClasses.Collect());
        context.RegisterSourceOutput(compilationAndInterceptors, static (spc, source) => 
            GenerateInterceptorAttributes(source.Left, source.Right!, spc));

        // Combine handlers with compilation for registration generation
        var compilationAndRequestHandlers = context.CompilationProvider.Combine(requestHandlerClasses.Collect());
        var compilationAndEventHandlers = compilationAndRequestHandlers.Combine(eventHandlerClasses.Collect());
        context.RegisterSourceOutput(compilationAndEventHandlers, static (spc, source) => 
            GenerateRegistration(source.Left.Left, source.Left.Right!, source.Right!, spc));
    }

    private static bool IsClassWithAttributes(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl && classDecl.AttributeLists.Count > 0;
    }

    private static bool IsClassDeclaration(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax;
    }

    private static InterceptorClassInfo? GetInterceptorClassInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol is null) return null;

        // Check if the class has the Interceptor attribute using semantic model
        foreach (var attr in classSymbol.GetAttributes())
        {
            var attrTypeName = attr.AttributeClass?.ToDisplayString();
            if (attrTypeName == "Nutzen.InterceptorAttribute")
            {
                // Get the order if specified (handles negative numbers correctly)
                int order = 0;
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Order" && namedArg.Value.Value is int orderValue)
                    {
                        order = orderValue;
                    }
                }

                // Get the number of type parameters for open generic typeof syntax
                int typeParameterCount = classSymbol.TypeParameters.Length;
                
                // Build the type name for typeof - use open generic syntax if generic
                string typeNameForTypeof;
                if (typeParameterCount > 0)
                {
                    // For generic types, use open generic syntax: typeof(LoggingInterceptor<,>)
                    var commas = new string(',', typeParameterCount - 1);
                    typeNameForTypeof = $"{classSymbol.ContainingNamespace.ToDisplayString()}.{classSymbol.Name}<{commas}>";
                }
                else
                {
                    typeNameForTypeof = classSymbol.ToDisplayString();
                }

                return new InterceptorClassInfo(
                    classSymbol.Name,
                    classSymbol.ContainingNamespace.ToDisplayString(),
                    typeNameForTypeof,
                    order);
            }
        }

        return null;
    }

    private static RequestHandlerClassInfo? GetRequestHandlerClassInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol is null || classSymbol.IsAbstract) return null;

        // Check if class implements IRequestHandler<,>
        INamedTypeSymbol? requestType = null;
        INamedTypeSymbol? responseType = null;

        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.ToDisplayString() == "Nutzen.IRequestHandler<TRequest, TResponse>")
            {
                if (iface.TypeArguments.Length == 2)
                {
                    requestType = iface.TypeArguments[0] as INamedTypeSymbol;
                    responseType = iface.TypeArguments[1] as INamedTypeSymbol;
                    break;
                }
            }
        }

        if (requestType is null || responseType is null) return null;

        // Get interceptor attributes
        var interceptors = new List<InterceptorReference>();
        foreach (var attr in classSymbol.GetAttributes())
        {
            var attrType = attr.AttributeClass;
            if (attrType is null) continue;

            // Check if this attribute inherits from InterceptorAttributeBase
            var baseType = attrType.BaseType;
            while (baseType is not null)
            {
                if (baseType.ToDisplayString() == "Nutzen.InterceptorAttributeBase")
                {
                    // Get the InterceptorType property value
                    var interceptorTypeProp = attrType.GetMembers("InterceptorType")
                        .OfType<IPropertySymbol>()
                        .FirstOrDefault();

                    if (interceptorTypeProp is not null)
                    {
                        // We need to get the actual type - this is tricky with source generators
                        // We'll use the attribute class name to infer the interceptor type
                        var attrName = attrType.Name;
                        var interceptorName = attrName.EndsWith("Attribute") 
                            ? attrName.Substring(0, attrName.Length - 9) 
                            : attrName;
                        
                        var interceptorFullName = attrType.ContainingNamespace.ToDisplayString() + "." + interceptorName;

                        // First, try to get the default order from the interceptor class's [Interceptor] attribute
                        int defaultOrder = 0;
                        var interceptorClassSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(interceptorFullName + "`2");
                        if (interceptorClassSymbol is not null)
                        {
                            foreach (var interceptorAttr in interceptorClassSymbol.GetAttributes())
                            {
                                var interceptorAttrName = interceptorAttr.AttributeClass?.ToDisplayString();
                                if (interceptorAttrName == "Nutzen.InterceptorAttribute")
                                {
                                    foreach (var namedArg in interceptorAttr.NamedArguments)
                                    {
                                        if (namedArg.Key == "Order" && namedArg.Value.Value is int orderVal)
                                        {
                                            defaultOrder = orderVal;
                                        }
                                    }
                                    break;
                                }
                            }
                        }

                        // Check if order was explicitly specified on the handler's attribute (overrides default)
                        int order = defaultOrder;
                        foreach (var namedArg in attr.NamedArguments)
                        {
                            if (namedArg.Key == "Order" && namedArg.Value.Value is int orderVal)
                            {
                                order = orderVal;
                            }
                        }

                        interceptors.Add(new InterceptorReference(interceptorFullName, attrType.ToDisplayString(), order));
                    }
                    break;
                }
                baseType = baseType.BaseType;
            }
        }

        return new RequestHandlerClassInfo(
            classSymbol.Name,
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.ToDisplayString(),
            requestType.ToDisplayString(),
            responseType.ToDisplayString(),
            interceptors.ToArray());
    }

    private static EventHandlerClassInfo? GetEventHandlerClassInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol is null || classSymbol.IsAbstract) return null;

        // Check if class implements IEventHandler<>
        var eventHandlerInterfaces = new List<(string EventTypeName, string InterfaceTypeName)>();

        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.ToDisplayString() == "Nutzen.IEventHandler<TEvent>")
            {
                if (iface.TypeArguments.Length == 1)
                {
                    var eventType = iface.TypeArguments[0] as INamedTypeSymbol;
                    if (eventType is not null)
                    {
                        eventHandlerInterfaces.Add((eventType.ToDisplayString(), iface.ToDisplayString()));
                    }
                }
            }
        }

        if (eventHandlerInterfaces.Count == 0) return null;

        return new EventHandlerClassInfo(
            classSymbol.Name,
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.ToDisplayString(),
            eventHandlerInterfaces.ToArray());
    }

    private static void GenerateInterceptorAttributes(Compilation compilation, ImmutableArray<InterceptorClassInfo?> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty) return;

        foreach (var classInfo in classes.Distinct())
        {
            if (classInfo is null) continue;

            var source = GenerateInterceptorAttribute(classInfo);
            context.AddSource($"{classInfo.ClassName}Attribute.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateInterceptorAttribute(InterceptorClassInfo classInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {classInfo.Namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Attribute to apply the {classInfo.ClassName} interceptor to a request handler.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = true)]");
        sb.AppendLine($"public sealed class {classInfo.ClassName}Attribute : Nutzen.InterceptorAttributeBase");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <inheritdoc/>");
        sb.AppendLine($"    public override System.Type InterceptorType => typeof({classInfo.FullTypeName});");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new instance of {classInfo.ClassName}Attribute with default order ({classInfo.DefaultOrder}).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public {classInfo.ClassName}Attribute()");
        sb.AppendLine("    {");
        sb.AppendLine($"        Order = {classInfo.DefaultOrder};");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateRegistration(
        Compilation compilation, 
        ImmutableArray<RequestHandlerClassInfo?> requestHandlers, 
        ImmutableArray<EventHandlerClassInfo?> eventHandlers,
        SourceProductionContext context)
    {
        var validRequestHandlers = requestHandlers.Where(h => h is not null).Cast<RequestHandlerClassInfo>().Distinct().ToList();
        var validEventHandlers = eventHandlers.Where(h => h is not null).Cast<EventHandlerClassInfo>().Distinct().ToList();
        
        if (validRequestHandlers.Count == 0 && validEventHandlers.Count == 0) return;

        var assemblyName = compilation.AssemblyName ?? "Assembly";
        
        // Strip "Nutzen." prefix if present for cleaner method names
        var hasNutzenPrefix = assemblyName.StartsWith("Nutzen.");
        var nameForMethod = hasNutzenPrefix 
            ? assemblyName.Substring(7) 
            : assemblyName;
        var safeAssemblyName = new string(nameForMethod.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        
        // If assembly starts with "Nutzen.", use "AddNutzen{Name}", otherwise "AddNutzenFrom{Name}"
        var methodName = hasNutzenPrefix 
            ? $"AddNutzenFrom{safeAssemblyName}" 
            : $"AddNutzenFrom{safeAssemblyName}";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        sb.AppendLine("namespace Nutzen;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Generated registration methods for handlers in {assemblyName}.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class NutzenRegistration_{safeAssemblyName}");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Registers all request handlers and event handlers from {assemblyName}.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static IServiceCollection {methodName}(this IServiceCollection services)");
        sb.AppendLine("    {");

        // Register request handlers
        if (validRequestHandlers.Count > 0)
        {
            sb.AppendLine("        // ==========================================");
            sb.AppendLine("        // Request Handlers");
            sb.AppendLine("        // ==========================================");
            sb.AppendLine();
        }

        foreach (var handler in validRequestHandlers)
        {
            var hasInterceptors = handler.Interceptors.Length > 0;

            // Check if this is a handler with Empty response (implements IRequestHandler<TRequest>)
            var isEmptyResponseHandler = handler.ResponseTypeName == "Nutzen.Empty";

            if (hasInterceptors)
            {
                // Register interceptors
                var distinctInterceptors = handler.Interceptors
                    .GroupBy(i => i.InterceptorTypeName)
                    .Select(g => g.First());
                foreach (var interceptor in distinctInterceptors)
                {
                    sb.AppendLine($"        // Register interceptor: {interceptor.InterceptorTypeName}");
                    sb.AppendLine($"        services.AddTransient<{interceptor.InterceptorTypeName}<{handler.RequestTypeName}, {handler.ResponseTypeName}>>();");
                }

                // Register handler as keyed service
                sb.AppendLine();
                sb.AppendLine($"        // Register handler {handler.ClassName} as keyed service (has interceptors)");
                sb.AppendLine($"        services.AddKeyedTransient<IRequestHandler<{handler.RequestTypeName}, {handler.ResponseTypeName}>, {handler.FullTypeName}>(\"_inner_{handler.FullTypeName}\");");

                // Register the intercepted handler as the main handler
                sb.AppendLine($"        services.AddTransient<IRequestHandler<{handler.RequestTypeName}, {handler.ResponseTypeName}>>(sp =>");
                sb.AppendLine("        {");
                sb.AppendLine($"            var innerHandler = sp.GetRequiredKeyedService<IRequestHandler<{handler.RequestTypeName}, {handler.ResponseTypeName}>>(\"_inner_{handler.FullTypeName}\");");

                // Build the pipeline - order interceptors by Order property
                var orderedInterceptors = handler.Interceptors.OrderBy(i => i.Order).ToArray();

                sb.AppendLine($"            Func<{handler.RequestTypeName}, Task<Result<{handler.ResponseTypeName}>>> pipeline = innerHandler.Handle;");
                sb.AppendLine();

                // Wrap in reverse order so the first interceptor is outermost
                for (int i = orderedInterceptors.Length - 1; i >= 0; i--)
                {
                    var interceptor = orderedInterceptors[i];
                    sb.AppendLine($"            // Interceptor: {interceptor.InterceptorTypeName} (Order: {interceptor.Order})");
                    sb.AppendLine($"            var interceptor{i} = sp.GetRequiredService<{interceptor.InterceptorTypeName}<{handler.RequestTypeName}, {handler.ResponseTypeName}>>();");
                    sb.AppendLine($"            var previousPipeline{i} = pipeline;");
                    sb.AppendLine($"            pipeline = request => interceptor{i}.Intercept(request, previousPipeline{i});");
                    sb.AppendLine();
                }

                sb.AppendLine($"            return new InterceptedRequestHandler<{handler.RequestTypeName}, {handler.ResponseTypeName}>(pipeline);");
                sb.AppendLine("        });");

                // Also register as IRequestHandler<TRequest> if response type is Empty
                if (isEmptyResponseHandler)
                {
                    sb.AppendLine();
                    sb.AppendLine($"        // Also register as IRequestHandler<TRequest> for convenience (using specialized InterceptedRequestHandler)");
                    sb.AppendLine($"        services.AddTransient<IRequestHandler<{handler.RequestTypeName}>>(sp =>");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var innerHandler = sp.GetRequiredKeyedService<IRequestHandler<{handler.RequestTypeName}, Nutzen.Empty>>(\"_inner_{handler.FullTypeName}\");");
                    sb.AppendLine($"            Func<{handler.RequestTypeName}, Task<Result<Nutzen.Empty>>> pipeline = innerHandler.Handle;");
                    sb.AppendLine();

                    // Wrap in reverse order so the first interceptor is outermost
                    for (int i = orderedInterceptors.Length - 1; i >= 0; i--)
                    {
                        var interceptor = orderedInterceptors[i];
                        sb.AppendLine($"            // Interceptor: {interceptor.InterceptorTypeName} (Order: {interceptor.Order})");
                        sb.AppendLine($"            var interceptor{i} = sp.GetRequiredService<{interceptor.InterceptorTypeName}<{handler.RequestTypeName}, Nutzen.Empty>>();");
                        sb.AppendLine($"            var previousPipeline{i} = pipeline;");
                        sb.AppendLine($"            pipeline = request => interceptor{i}.Intercept(request, previousPipeline{i});");
                        sb.AppendLine();
                    }

                    sb.AppendLine($"            return new InterceptedRequestHandler<{handler.RequestTypeName}>(pipeline);");
                    sb.AppendLine("        });");
                }
            }
            else
            {
                // Register handler directly
                sb.AppendLine($"        // Register handler {handler.ClassName} (no interceptors)");
                sb.AppendLine($"        services.AddTransient<IRequestHandler<{handler.RequestTypeName}, {handler.ResponseTypeName}>, {handler.FullTypeName}>();");

                // Also register as IRequestHandler<TRequest> if response type is Empty
                if (isEmptyResponseHandler)
                {
                    sb.AppendLine($"        // Also register as IRequestHandler<TRequest> for convenience");
                    sb.AppendLine($"        services.AddTransient<IRequestHandler<{handler.RequestTypeName}>>(sp =>");
                    sb.AppendLine($"            (IRequestHandler<{handler.RequestTypeName}>)sp.GetRequiredService<IRequestHandler<{handler.RequestTypeName}, Nutzen.Empty>>());");
                }
            }

            sb.AppendLine();
        }

        // Register event handlers
        if (validEventHandlers.Count > 0)
        {
            sb.AppendLine("        // ==========================================");
            sb.AppendLine("        // Event Handlers");
            sb.AppendLine("        // ==========================================");
            sb.AppendLine();
        }

        foreach (var handler in validEventHandlers)
        {
            foreach (var (eventTypeName, interfaceTypeName) in handler.EventInterfaces)
            {
                sb.AppendLine($"        // Register event handler {handler.ClassName} for {eventTypeName}");
                sb.AppendLine($"        services.AddTransient<IEventHandler<{eventTypeName}>, {handler.FullTypeName}>();");
            }
            sb.AppendLine();
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource($"NutzenRegistration_{safeAssemblyName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private sealed record InterceptorClassInfo(
        string ClassName, 
        string Namespace, 
        string FullTypeName, 
        int DefaultOrder);
    private sealed record InterceptorReference(
        string InterceptorTypeName, 
        string AttributeTypeName, 
        int Order);
    private sealed record RequestHandlerClassInfo(
        string ClassName, 
        string Namespace, 
        string FullTypeName, 
        string RequestTypeName, 
        string ResponseTypeName,
        InterceptorReference[] Interceptors);
    private sealed record EventHandlerClassInfo(
        string ClassName,
        string Namespace,
        string FullTypeName,
        (string EventTypeName, string InterfaceTypeName)[] EventInterfaces);
}

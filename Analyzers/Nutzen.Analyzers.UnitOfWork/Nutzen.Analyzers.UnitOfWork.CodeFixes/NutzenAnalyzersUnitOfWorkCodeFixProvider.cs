using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nutzen.Analyzers.UnitOfWork
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NutzenAnalyzersUnitOfWorkCodeFixProvider)), Shared]
    public class NutzenAnalyzersUnitOfWorkCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(
                NutzenAnalyzersUnitOfWorkAnalyzer.MustBeStaticDiagnosticId,
                NutzenAnalyzersUnitOfWorkAnalyzer.MustHaveRequestDiagnosticId,
                NutzenAnalyzersUnitOfWorkAnalyzer.MustHaveHandlerDiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            foreach (var diagnostic in context.Diagnostics)
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;
                var declaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (declaration == null) continue;

                switch (diagnostic.Id)
                {
                    case NutzenAnalyzersUnitOfWorkAnalyzer.MustBeStaticDiagnosticId:
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                title: "Make class static",
                                createChangedDocument: c => MakeStaticAsync(context.Document, declaration, c),
                                equivalenceKey: "MakeStatic"),
                            diagnostic);
                        break;

                    case NutzenAnalyzersUnitOfWorkAnalyzer.MustHaveRequestDiagnosticId:
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                title: "Add Request inner record",
                                createChangedDocument: c => AddRequestRecordAsync(context.Document, declaration, c),
                                equivalenceKey: "AddRequest"),
                            diagnostic);
                        break;

                    case NutzenAnalyzersUnitOfWorkAnalyzer.MustHaveHandlerDiagnosticId:
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                title: "Add Handler inner class",
                                createChangedDocument: c => AddHandlerClassAsync(context.Document, declaration, c),
                                equivalenceKey: "AddHandler"),
                            diagnostic);
                        break;
                }
            }
        }

        private async Task<Document> MakeStaticAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) return document;

            var staticModifier = SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space);
            var newModifiers = classDecl.Modifiers.Add(staticModifier);
            var newClassDecl = classDecl.WithModifiers(newModifiers);

            var newRoot = root.ReplaceNode(classDecl, newClassDecl);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> AddRequestRecordAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) return document;

            // Check if there's already a type with RequestAttribute
            var existingRequest = classDecl.Members
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(m => m.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(a => a.Name.ToString().Contains("Request")));

            if (existingRequest != null)
            {
                // Add inheritance if missing
                if (existingRequest is RecordDeclarationSyntax recordDecl && recordDecl.BaseList == null)
                {
                    var baseList = SyntaxFactory.BaseList(
                        SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                            SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("Request"))));
                    var newRecordDecl = recordDecl.WithBaseList(baseList);
                    var newRoot = root.ReplaceNode(recordDecl, newRecordDecl);
                    return document.WithSyntaxRoot(newRoot);
                }
                return document;
            }

            // Create new Request record
            var requestRecord = SyntaxFactory.RecordDeclaration(
                SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                SyntaxFactory.Identifier("Request"))
                .WithAttributeLists(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AttributeList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Request"))))))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithBaseList(
                    SyntaxFactory.BaseList(
                        SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                            SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("Nutzen.Request")))))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace("        "))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            var newClassDecl = classDecl.AddMembers(requestRecord);
            var newRootWithRequest = root.ReplaceNode(classDecl, newClassDecl);
            return document.WithSyntaxRoot(newRootWithRequest);
        }

        private async Task<Document> AddHandlerClassAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) return document;

            // Find the Request type name
            var requestType = classDecl.Members
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(m => m.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(a => a.Name.ToString().Contains("Request")));

            var requestTypeName = requestType?.Identifier.Text ?? "Request";
            var fullRequestTypeName = $"{classDecl.Identifier.Text}.{requestTypeName}";

            // Check if there's already a type with HandlerAttribute
            var existingHandler = classDecl.Members
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(m => m.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(a => a.Name.ToString().Contains("Handler")));

            if (existingHandler != null)
            {
                // Add interface implementation if missing
                if (existingHandler is ClassDeclarationSyntax existingClass)
                {
                    var hasInterface = existingClass.BaseList?.Types
                        .Any(t => t.ToString().Contains("IRequestHandler")) ?? false;

                    if (!hasInterface)
                    {
                        var interfaceType = SyntaxFactory.SimpleBaseType(
                            SyntaxFactory.ParseTypeName($"IRequestHandler<{requestTypeName}>"));
                        var newBaseList = existingClass.BaseList == null
                            ? SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(interfaceType))
                            : existingClass.BaseList.AddTypes(interfaceType);
                        var newExistingClass = existingClass.WithBaseList(newBaseList);
                        var newRoot = root.ReplaceNode(existingClass, newExistingClass);
                        return document.WithSyntaxRoot(newRoot);
                    }
                }
                return document;
            }

            // Create new Handler class
            var handlerClass = SyntaxFactory.ClassDeclaration("Handler")
                .WithAttributeLists(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AttributeList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Handler"))))))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithBaseList(
                    SyntaxFactory.BaseList(
                        SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                            SyntaxFactory.SimpleBaseType(
                                SyntaxFactory.ParseTypeName($"Nutzen.InterceptableRequestHandler<{requestTypeName}>")))))
                .WithMembers(
                    SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                        SyntaxFactory.MethodDeclaration(
                            SyntaxFactory.ParseTypeName($"System.Threading.Tasks.Task<Nutzen.Result<Nutzen.Empty>>"),
                            "Operation")
                        .WithModifiers(SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                            SyntaxFactory.Token(SyntaxKind.OverrideKeyword)))
                        .WithParameterList(
                            SyntaxFactory.ParameterList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("request"))
                                        .WithType(SyntaxFactory.ParseTypeName(requestTypeName)))))
                        .WithBody(
                            SyntaxFactory.Block(
                                SyntaxFactory.ThrowStatement(
                                    SyntaxFactory.ObjectCreationExpression(
                                        SyntaxFactory.ParseTypeName("System.NotImplementedException"))
                                    .WithArgumentList(SyntaxFactory.ArgumentList()))))))
                .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace("        "))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            var newClassDecl = classDecl.AddMembers(handlerClass);
            var newRootWithHandler = root.ReplaceNode(classDecl, newClassDecl);
            return document.WithSyntaxRoot(newRootWithHandler);
        }
    }
}

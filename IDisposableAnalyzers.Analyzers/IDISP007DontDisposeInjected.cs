namespace IDisposableAnalyzers
{
    using System.Collections.Immutable;
    using Gu.Roslyn.AnalyzerExtensions;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class IDISP007DontDisposeInjected : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "IDISP007";

        private static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: "Don't dispose injected.",
            messageFormat: "Don't dispose injected.",
            category: AnalyzerCategory.Correctness,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: AnalyzerConstants.EnabledByDefault,
            description: "Don't dispose disposables you do not own.",
            helpLinkUri: HelpLink.ForId(DiagnosticId));

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptor);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(HandleUsing, SyntaxKind.UsingStatement);
            context.RegisterSyntaxNodeAction(HandleInvocation, SyntaxKind.InvocationExpression);
        }

        private static void HandleUsing(SyntaxNodeAnalysisContext context)
        {
            if (context.IsExcludedFromAnalysis())
            {
                return;
            }

            var usingStatement = (UsingStatementSyntax)context.Node;
            if (usingStatement.Expression is InvocationExpressionSyntax ||
                usingStatement.Expression is IdentifierNameSyntax)
            {
                if (Disposable.IsPotentiallyCachedOrInjected(usingStatement.Expression, context.SemanticModel, context.CancellationToken))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptor, usingStatement.Expression.GetLocation()));
                    return;
                }
            }

            if (usingStatement.Declaration != null)
            {
                foreach (var variableDeclarator in usingStatement.Declaration.Variables)
                {
                    if (variableDeclarator.Initializer == null)
                    {
                        continue;
                    }

                    var value = variableDeclarator.Initializer.Value;
                    if (Disposable.IsPotentiallyCachedOrInjected(value, context.SemanticModel, context.CancellationToken))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptor, value.GetLocation()));
                        return;
                    }
                }
            }
        }

        private static void HandleInvocation(SyntaxNodeAnalysisContext context)
        {
            if (context.IsExcludedFromAnalysis())
            {
                return;
            }

            if (context.Node is InvocationExpressionSyntax invocation &&
                invocation.ArgumentList != null &&
                invocation.ArgumentList.Arguments.Count == 0 &&
                !invocation.TryFirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>(out _) &&
                context.SemanticModel.TryGetSymbol(invocation, KnownSymbol.IDisposable.Dispose, context.CancellationToken, out var dispose) &&
                dispose.Parameters.Length == 0 &&
                Disposable.IsPotentiallyCachedOrInjected(invocation, context.SemanticModel, context.CancellationToken))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, invocation.FirstAncestorOrSelf<StatementSyntax>()?.GetLocation() ?? invocation.GetLocation()));
            }
        }
    }
}

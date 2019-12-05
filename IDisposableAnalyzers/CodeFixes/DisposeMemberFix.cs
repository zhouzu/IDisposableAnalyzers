﻿namespace IDisposableAnalyzers
{
    using System.Collections.Immutable;
    using System.Composition;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Gu.Roslyn.AnalyzerExtensions;
    using Gu.Roslyn.CodeFixExtensions;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Editing;

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DisposeMemberFix))]
    [Shared]
    internal class DisposeMemberFix : DocumentEditorCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(Descriptors.IDISP002DisposeMember.Id);

        protected override async Task RegisterCodeFixesAsync(DocumentEditorCodeFixContext context)
        {
            var syntaxRoot = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
                                          .ConfigureAwait(false);

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken)
                                             .ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                if (syntaxRoot.TryFindNode<MemberDeclarationSyntax>(diagnostic, out var member) &&
                    semanticModel.TryGetSymbol(member, context.CancellationToken, out ISymbol? symbol) &&
                    FieldOrProperty.TryCreate(symbol, out var disposable))
                {
                    if (DisposeMethod.TryFindVirtualDispose(symbol.ContainingType, semanticModel.Compilation, Search.TopLevel, out var disposeSymbol) &&
                        disposeSymbol.TrySingleDeclaration(context.CancellationToken, out MethodDeclarationSyntax? disposeDeclaration))
                    {
                        if (disposeDeclaration is { ParameterList: { Parameters: { Count: 1 } parameters }, Body: { } block })
                        {
                            context.RegisterCodeFix(
                                $"{symbol.Name}.Dispose() in {disposeSymbol}",
                                (editor, token) => Dispose(editor, token),
                                "Dispose member.",
                                diagnostic);

                            void Dispose(DocumentEditor editor, CancellationToken cancellationToken)
                            {
                                if (TryFindIfNotDisposingReturn(disposeDeclaration!, out var ifNotDisposingReturn) &&
                                    ifNotDisposingReturn.Parent is BlockSyntax)
                                {
                                    editor.InsertAfter(
                                        ifNotDisposingReturn,
                                        IDisposableFactory.DisposeStatement(disposable, editor.SemanticModel, cancellationToken));
                                }
                                else if (TryFindIfDisposing(disposeDeclaration!, out var ifDisposing))
                                {
                                    _ = editor.ReplaceNode(
                                        ifDisposing.Statement,
                                        x => x is BlockSyntax ifBlock
                                            ? ifBlock.AddStatements(IDisposableFactory.DisposeStatement(disposable, editor.SemanticModel, cancellationToken))
                                            : SyntaxFactory.Block(x, IDisposableFactory.DisposeStatement(disposable, editor.SemanticModel, cancellationToken)));
                                }
                                else
                                {
                                    ifDisposing = SyntaxFactory.IfStatement(
                                        SyntaxFactory.IdentifierName(parameters[0].Identifier),
                                        SyntaxFactory.Block(IDisposableFactory.DisposeStatement(disposable, editor.SemanticModel, cancellationToken)));
                                    if (DisposeMethod.TryFindBaseCall(disposeDeclaration!, editor.SemanticModel, cancellationToken, out var baseCall))
                                    {
                                        editor.InsertBefore(baseCall.Parent, ifDisposing);
                                    }
                                    else
                                    {
                                        _ = editor.ReplaceNode(block, x => x.AddStatements(ifDisposing));
                                    }
                                }
                            }
                        }
                    }
                    else if (DisposeMethod.TryFindIDisposableDispose(symbol.ContainingType, semanticModel.Compilation, Search.TopLevel, out disposeSymbol) &&
                             disposeSymbol.TrySingleDeclaration(context.CancellationToken, out disposeDeclaration))
                    {
                        switch (disposeDeclaration)
                        {
                            case { ExpressionBody: { Expression: { } expression } }:
                                context.RegisterCodeFix(
                                    $"{symbol.Name}.Dispose() in {disposeSymbol}",
                                    (editor, cancellationToken) => editor.ReplaceNode(
                                        disposeDeclaration,
                                        x => x.AsBlockBody(
                                            SyntaxFactory.ExpressionStatement(expression),
                                            IDisposableFactory.DisposeStatement(disposable, editor.SemanticModel, cancellationToken))),
                                    "Dispose member.",
                                    diagnostic);
                                break;
                            case { Body: { } body }:
                                context.RegisterCodeFix(
                                    $"{symbol.Name}.Dispose() in {disposeSymbol}",
                                    (editor, cancellationToken) => Dispose(editor, cancellationToken),
                                    "Dispose member.",
                                    diagnostic);

                                void Dispose(DocumentEditor editor, CancellationToken cancellationToken)
                                {
                                    if (DisposeMethod.TryFindBaseCall(disposeDeclaration!, editor.SemanticModel, cancellationToken, out var baseCall))
                                    {
                                        editor.InsertBefore(
                                            baseCall.Parent,
                                            IDisposableFactory.DisposeStatement(disposable, editor.SemanticModel, cancellationToken));
                                    }
                                    else
                                    {
                                        _ = editor.ReplaceNode(
                                            body,
                                            x => x.AddStatements(IDisposableFactory.DisposeStatement(disposable, editor.SemanticModel, cancellationToken)));
                                    }
                                }

                                break;
                        }
                    }
                }
            }
        }

        private static bool TryFindIfDisposing(MethodDeclarationSyntax disposeMethod, [NotNullWhen(true)] out IfStatementSyntax? result)
        {
            if (disposeMethod is { ParameterList: { Parameters: { Count: 1 } parameters }, Body: { } body } &&
                parameters[0] is { Type: { } type, Identifier: { ValueText: { } valueText } } &&
                type == KnownSymbol.Boolean)
            {
                foreach (var statement in body.Statements)
                {
                    if (statement is IfStatementSyntax { Condition: IdentifierNameSyntax condition } ifStatement &&
                        condition.Identifier.ValueText == valueText)
                    {
                        result = ifStatement;
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        private static bool TryFindIfNotDisposingReturn(MethodDeclarationSyntax disposeMethod, [NotNullWhen(true)] out IfStatementSyntax? result)
        {
            if (disposeMethod is { ParameterList: { Parameters: { Count: 1 } parameters }, Body: { } body } &&
                parameters[0] is { Type: { } type, Identifier: { ValueText: { } valueText } } &&
                type == KnownSymbol.Boolean)
            {
                foreach (var statement in body.Statements)
                {
                    if (statement is IfStatementSyntax { Condition: PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax operand } condition } ifStatement &&
                        condition.IsKind(SyntaxKind.LogicalNotExpression) &&
                        operand.Identifier.ValueText == valueText &&
                        IsReturn(ifStatement.Statement))
                    {
                        result = ifStatement;
                        return true;
                    }
                }
            }

            result = null;
            return false;

            static bool IsReturn(StatementSyntax statement)
            {
                return statement switch
                {
                    ReturnStatementSyntax _ => true,
                    BlockSyntax { Statements: { } statements } => statements.LastOrDefault() is ReturnStatementSyntax,
                    _ => false,
                };
            }
        }
    }
}

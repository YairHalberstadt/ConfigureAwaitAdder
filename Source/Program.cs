using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Formatting;

namespace ConfigureAwaitAdder
{
    class Program
    {
        static async Task Main(string[] args)
		{
			foreach (var projectPath in args)
			{
				await RunOnProject(projectPath);
			}
		}

		public static async Task RunOnProject(string projectPath)
		{
			try
			{
				using (var workspace = MSBuildWorkspace.Create())
				{
					workspace.LoadMetadataForReferencedProjects = true;
					var project = await workspace.OpenProjectAsync(projectPath);
					ImmutableList<WorkspaceDiagnostic> diagnostics = workspace.Diagnostics;
					foreach (var diagnostic in diagnostics)
					{
						Console.WriteLine(diagnostic.Message);
					}

					await FixProject(project);
				}

			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
		}

		public static async Task FixProject(Project project)
		{
			var compilation = await project.GetCompilationAsync();

			var methodVisitor = new MethodRewriter();

			foreach (var syntaxTree in compilation.SyntaxTrees)
			{
				var newSyntaxTree = methodVisitor.Visit(syntaxTree.GetRoot());
				if (syntaxTree.GetRoot() != newSyntaxTree)
				{
					File.WriteAllText(syntaxTree.FilePath, newSyntaxTree.GetText().ToString());
				}
			}
		}
    }

	class MethodRewriter : CSharpSyntaxRewriter
	{
		public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
		{
			if (node.Modifiers.All(x => x.ValueText != "async"))
				return node;

			return (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
		}

		public override SyntaxNode VisitAwaitExpression(AwaitExpressionSyntax node)
		{
			node = (AwaitExpressionSyntax)base.VisitAwaitExpression(node);
			if (node.Expression is InvocationExpressionSyntax invocationExpression
				&& invocationExpression.Expression is MemberAccessExpressionSyntax memberAccessExpression
				&& memberAccessExpression.Name.Identifier.ValueText == "ConfigureAwait")
				return node;
			var newExpression = SyntaxFactory.ParseExpression(node.ToFullString() + ".ConfigureAwait(false)");
			return newExpression;
		}
	}
}

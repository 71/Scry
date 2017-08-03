using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EnvDTE80;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Scry
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class Globals : IDisposable
    {
        private int _indent;

        public Globals Context => this;

        public VisualStudioWorkspace Workspace { get; }
        public Project Project { get; }

        public TextWriter Output { get; }
        public string Extension { get; set; }

        public string ScriptFile { get; }

        public int Indentation
        {
            get => _indent;
            set => _indent = value < 0 ? 0 : value;
        }

        public int Indent
        {
            get => _indent;
            set => _indent = value < 0 ? 0 : value;
        }

        public bool AutoWriteIndentation { get; set; }

        internal MemoryStream OutputStream { get; }

        public Globals(string script, Project project, VisualStudioWorkspace w)
        {
            Workspace = w;
            Project = project;

            if (Project == null)
                throw new Exception("Could not open project. Are you sure it has fully loaded?");

            ScriptFile = script;
            Output = new StreamWriter(OutputStream = new MemoryStream(), Encoding.UTF8);
        }

        public Globals WriteLine(string format, params object[] args)
        {
            if (AutoWriteIndentation)
                this.WriteIndentation();

            Output.WriteLine(format, args);
            return this;
        }

        public Globals Write(string format, params object[] args)
        {
            Output.Write(format, args);
            return this;
        }

        public Globals WriteLine(params object[] args)
        {
            if (AutoWriteIndentation)
                this.WriteIndentation();

            Output.WriteLine(string.Join(" ", args));
            return this;
        }

        public Globals Write(params object[] args)
        {
            Output.Write(string.Join(" ", args));
            return this;
        }

        public Globals WriteUsings(params string[] usings)
        {
            foreach (string u in usings)
                WriteIndentation().Output.WriteLine("using {0};", u);

            return this;
        }

        public Globals WriteNamespace(string @namespace)
        {
            this.WriteLine("namespace {0}", @namespace)
                .WriteLine('{')
                .Indentation += 4;

            return this;
        }

        public Globals WriteEnd()
        {
            this.DecreaseIndentation()
                .WriteLine('}');

            return this;
        }

        public Globals WriteIndentation()
        {
            Output.Write(new string(' ', Indentation));
            return this;
        }

        public Globals IncreaseIndentation(int indent = 4)
        {
            Indentation += indent;
            return this;
        }

        public Globals DecreaseIndentation(int indent = 4)
        {
            Indentation -= indent;
            return this;
        }

        internal async Task WriteToFileAsync()
        {
            await Output.FlushAsync();

            OutputStream.Seek(0, SeekOrigin.Begin);

            using (FileStream fs = File.OpenWrite(Path.ChangeExtension(ScriptFile, Extension)))
            {
                fs.SetLength(0);

                await OutputStream.CopyToAsync(fs);
            }
        }

        public void Dispose()
        {
            OutputStream.Dispose();
        }

        public SyntaxNode Syntax(string filename)
        {
            return Project.Documents.FirstOrDefault(x => x.Name.EndsWith(filename))?.GetSyntaxRootAsync().Result;
        }

        public CSharpSyntaxTree Tree(string filename)
        {
            return Project.Documents.FirstOrDefault(x => x.Name.EndsWith(filename))?.GetSyntaxTreeAsync().Result as CSharpSyntaxTree;
        }

        public SemanticModel Model(string filename)
        {
            return Project.Documents.FirstOrDefault(x => x.Name.EndsWith(filename))?.GetSemanticModelAsync().Result;
        }

        public IEnumerable<SyntaxNode> Syntaxes => Project.Documents.Select(x => x.GetSyntaxRootAsync().Result);
        public IEnumerable<CSharpSyntaxTree> Trees => Project.Documents.Select(x => x.GetSyntaxTreeAsync().Result).OfType<CSharpSyntaxTree>();
        public IEnumerable<SemanticModel> Models => Project.Documents.Select(x => x.GetSemanticModelAsync().Result);
    }

    internal class ScriptRunner
    {
        // In theory, ScriptRunner isn't used till the ScryPackage is initialized
        public static ErrorListProvider ErrorListProvider = new ErrorListProvider(ScryPackage.Instance);

        public static async Task<(Globals globals, ScriptState<object> state, ImmutableArray<Diagnostic> diags, Exception exception)> RunAsync(string scriptpath, Project project, string content)
        {
            var globals = new Globals(scriptpath, project, ScryPackage.GlobalWorkspace)
            {
                Extension = ".g.cs"
            };

            var opts = ScriptOptions.Default
                .WithFilePath(scriptpath)
                .AddImports("System",
                            "System.Collections.Generic",
                            "System.Linq",
                            "System.Reflection",
                            "System.Threading.Tasks",
                            "Microsoft.CodeAnalysis",
                            "Microsoft.CodeAnalysis.CSharp",
                            "Microsoft.CodeAnalysis.CSharp.Syntax")
                .AddReferences(typeof(string).Assembly,
                               typeof(MethodInfo).Assembly,
                               typeof(Task).Assembly,
                               typeof(SyntaxNode).Assembly,
                               typeof(CSharpCompilation).Assembly,
                               typeof(Workspace).Assembly);

            var script = CSharpScript.Create(content, opts, globals.GetType());
            var diagnostics = script.Compile();

            bool hadError = diagnostics.Any(x => x.IsWarningAsError || x.Severity == DiagnosticSeverity.Error);

            if (hadError)
                return (globals, null, diagnostics, null);

            try
            {
                ScriptState<object> state = await script.RunAsync(globals);

                await globals.WriteToFileAsync();
                globals.Dispose();

                return (globals, state, diagnostics, null);
            }
            catch (Exception x)
            {
                return (globals, null, diagnostics, x);
            }
        }

        public static async Task RunScripts(string[] scripts)
        {
            ErrorListProvider.Tasks.Clear();

            foreach (string script in scripts)
            {
                string content = File.ReadAllText(script);
                string projfile = GetMatchingProject(script);
                Project project = ScryPackage.GlobalWorkspace.CurrentSolution.Projects.FirstOrDefault(x => x.FilePath == projfile);

                var (_,_,diags,ex) = await RunAsync(script, project, content);

                foreach (var diag in diags)
                    Log(script, diag);
                if (ex != null)
                    ErrorListProvider.Tasks.Add(new ErrorTask(ex) { Document = script });
            }
        }

        public static void Log(string script, Diagnostic diag)
        {
            var loc = diag.Location.GetMappedLineSpan();

            ErrorListProvider.Tasks.Add(new ErrorTask
            {
                Category = TaskCategory.BuildCompile,
                ErrorCategory = diag.IsWarningAsError || diag.Severity == DiagnosticSeverity.Error
                    ? TaskErrorCategory.Error : TaskErrorCategory.Warning,
                Line = loc.StartLinePosition.Line,
                Column = loc.StartLinePosition.Character,
                Document = script,
                Text = diag.GetMessage(),
                HelpKeyword = diag.Descriptor.HelpLinkUri
            });
        }

        public static string GetMatchingProject(string filename)
        {
            DTE2 dte = Package.GetGlobalService(typeof(SDTE)) as DTE2;

            Debug.Assert(dte != null);

            var projItem = dte.Solution.FindProjectItem(filename);

            return projItem.ContainingProject.FileName;
        }
    }
}

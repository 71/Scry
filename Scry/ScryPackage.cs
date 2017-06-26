using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Project = Microsoft.CodeAnalysis.Project;

namespace Scry
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class ScryPackage : Package
    {
        /// <summary>
        /// CreateScriptCommandPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "2d6aa5e5-4ce8-411e-85fd-e18167a3d194";

        public static ScryPackage Instance { get; private set; }

        public ScryPackage()
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the <see cref="VisualStudioWorkspace"/> of the current solution.
        /// </summary>
        public VisualStudioWorkspace Workspace { get; private set; }

        public static VisualStudioWorkspace GlobalWorkspace
            => GetGlobalService(typeof(SComponentModel)) is IComponentModel componentModel
             ? componentModel.GetService<VisualStudioWorkspace>()
             : null;

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            if (GetGlobalService(typeof(SComponentModel)) is IComponentModel componentModel)
            {
                Workspace = componentModel.GetService<VisualStudioWorkspace>();
                Workspace.WorkspaceChanged += WorkspaceChanged;
            }

            RunSelectedScriptsCommand.Initialize(this);
            RunProjectScriptsCommand.Initialize(this);

            base.Initialize();
        }

        #endregion

        /// <summary>
        /// Keeps track of changed workspaces.
        /// </summary>
        private void WorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
        {
            switch (e.Kind)
            {
                case WorkspaceChangeKind.SolutionChanged:
                case WorkspaceChangeKind.SolutionRemoved:
                case WorkspaceChangeKind.SolutionCleared:
                case WorkspaceChangeKind.SolutionReloaded:
                    return;
            }

            if (e.ProjectId == null)
                return;

            Project proj = Workspace.CurrentSolution.GetProject(e.ProjectId);
            IVsHierarchy vsProj = Workspace.GetHierarchy(e.ProjectId);

            foreach (var script in proj.Documents)
            {
                if (Path.GetExtension(script.FilePath) != ".csx")
                    continue;
            }
        }
    }
}

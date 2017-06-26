using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace Scry
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class RunProjectScriptsCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0200;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("8771fa60-6d1d-4298-b676-64c5eda2b366");

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static RunProjectScriptsCommand Instance { get; private set; }

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        private string[] LastSelectedScripts;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunProjectScriptsCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private RunProjectScriptsCommand(Package package)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));

            if (ServiceProvider.GetService(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new OleMenuCommand(this.MenuItemCallback, menuCommandID);

                menuItem.BeforeQueryStatus += BeforeQueryStatus;

                commandService.AddCommand(menuItem);
            }
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            if (!(sender is OleMenuCommand menuCommand))
                return;

            menuCommand.Visible = false;
            menuCommand.Enabled = false;

            DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

            if (dte == null)
                return;

            UIHierarchy uih = dte.ToolWindows.SolutionExplorer;
            Array uiSelectedItems = uih.SelectedItems as Array;

            if (uiSelectedItems == null || uiSelectedItems.Length != 1)
                return;

            Project proj = (uiSelectedItems.GetValue(0) as UIHierarchyItem)?.Object as Project;

            if (proj == null)
                return;

            Stack<string> scripts = new Stack<string>();

            foreach (ProjectItem projItem in proj.ProjectItems)
            {
                for (short i = 0; i < projItem.FileCount; i++)
                {
                    string fullName = projItem.FileNames[i];

                    if (Path.GetExtension(fullName) == ".csx")
                        scripts.Push(fullName);
                }
            }

            if (scripts.Count == 0)
                return;

            menuCommand.Text = scripts.Count == 1
                ? "Run script with Scry"
                : "Run all scripts with Scry";

            menuCommand.Visible = true;
            menuCommand.Enabled = true;

            LastSelectedScripts = scripts.ToArray();
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider => this.package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new RunProjectScriptsCommand(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private async void MenuItemCallback(object sender, EventArgs e)
        {
            await ScriptRunner.RunScripts(LastSelectedScripts);
        }
    }
}

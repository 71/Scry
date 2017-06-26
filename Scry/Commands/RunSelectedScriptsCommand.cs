using System;
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
    internal sealed class RunSelectedScriptsCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0300;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("8771fa60-6d1d-4298-b676-64c5eda2b366");

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static RunSelectedScriptsCommand Instance { get; private set; }

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        private string[] LastSelectedScripts;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunSelectedScriptsCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private RunSelectedScriptsCommand(Package package)
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

            if (!TryGetSelectedItems(out string[] selectedItems))
                return;

            if (!Array.TrueForAll(selectedItems, item => Path.GetExtension(item) == ".csx"))
                return;

            menuCommand.Text = selectedItems.Length == 1
                ? "Run script with Scry"
                : "Run selected scripts with Scry";

            menuCommand.Visible = true;
            menuCommand.Enabled = true;

            LastSelectedScripts = selectedItems;
        }

        private static bool TryGetSelectedItems(out string[] selectedItems)
        {
            selectedItems = null;

            DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

            if (dte == null)
                return false;

            UIHierarchy uih = dte.ToolWindows.SolutionExplorer;
            Array uiSelectedItems = uih.SelectedItems as Array;

            if (uiSelectedItems == null || uiSelectedItems.Length == 0)
                return false;

            selectedItems = new string[uiSelectedItems.Length];

            for (int i = 0; i < uiSelectedItems.Length; i++)
            {
                ProjectItem projectItem = (uiSelectedItems.GetValue(i) as UIHierarchyItem)?.Object as ProjectItem;

                if (projectItem == null)
                    return false;

                selectedItems[i] = projectItem.Properties.Item("FullPath").Value.ToString();
            }

            return true;
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
            Instance = new RunSelectedScriptsCommand(package);
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

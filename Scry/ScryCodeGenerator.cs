// After hours of tests, I still can't manage to get VS to
// recognize this code generator, and I'm thus giving up for now.
// It doesn't really matter, since the WorkspaceChanged event hook
// in ScryPackage.cs takes care of automatically updating the produced files.
#if false
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Scry
{
    /// <summary>
    /// A managed wrapper for VS's concept of an IVsSingleFileGenerator which is
    /// a custom tool invoked at design time which can take any file as an input
    /// and provide any file as output.
    /// </summary>
    [ComVisible(true)]
    [Guid("63fc50b3-d1a0-4a01-9f02-d8a955d7637d")]
    [CodeGeneratorRegistration(typeof(ScryCodeGenerator), "Scry code generator", UIContextGuids.SolutionExists, GeneratesDesignTimeSource = true)]
    [ProvideObject(typeof(ScryCodeGenerator), RegisterUsing = RegistrationMethod.CodeBase)]
    public sealed class ScryCodeGenerator : IVsSingleFileGenerator
    {
        /// <summary>
        /// Implements the IVsSingleFileGenerator.DefaultExtension method. 
        /// Returns the extension of the generated file
        /// </summary>
        /// <param name="pbstrDefaultExtension">Out parameter, will hold the extension that is to be given to the output file name. The returned extension must include a leading period</param>
        /// <returns>S_OK if successful, E_FAIL if not</returns>
        int IVsSingleFileGenerator.DefaultExtension(out string pbstrDefaultExtension)
        {
            pbstrDefaultExtension = ".g.cs";
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Implements the IVsSingleFileGenerator.Generate method.
        /// Executes the transformation and returns the newly generated output file, whenever a custom tool is loaded, or the input file is saved
        /// </summary>
        /// <param name="wszInputFilePath">The full path of the input file. May be a null reference (Nothing in Visual Basic) in future releases of Visual Studio, so generators should not rely on this value</param>
        /// <param name="bstrInputFileContents">The contents of the input file. This is either a UNICODE BSTR (if the input file is text) or a binary BSTR (if the input file is binary). If the input file is a text file, the project system automatically converts the BSTR to UNICODE</param>
        /// <param name="wszDefaultNamespace">This parameter is meaningful only for custom tools that generate code. It represents the namespace into which the generated code will be placed. If the parameter is not a null reference (Nothing in Visual Basic) and not empty, the custom tool can use the following syntax to enclose the generated code</param>
        /// <param name="rgbOutputFileContents">[out] Returns an array of bytes to be written to the generated file. You must include UNICODE or UTF-8 signature bytes in the returned byte array, as this is a raw stream. The memory for rgbOutputFileContents must be allocated using the .NET Framework call, System.Runtime.InteropServices.AllocCoTaskMem, or the equivalent Win32 system call, CoTaskMemAlloc. The project system is responsible for freeing this memory</param>
        /// <param name="pcbOutput">[out] Returns the count of bytes in the rgbOutputFileContent array</param>
        /// <param name="pGenerateProgress">A reference to the IVsGeneratorProgress interface through which the generator can report its progress to the project system</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns E_FAIL</returns>
        int IVsSingleFileGenerator.Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace, IntPtr[] rgbOutputFileContents, out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
        {
            if (bstrInputFileContents == null)
                throw new ArgumentNullException(nameof(bstrInputFileContents));

            CodeGeneratorProgress = pGenerateProgress;

            byte[] bytes = GenerateCodeAsync(wszInputFilePath, bstrInputFileContents).Result;

            if (bytes == null)
            {
                pcbOutput = 0;
                return VSConstants.E_FAIL;
            }

            pcbOutput = (uint)bytes.Length;

            rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(bytes.Length);

            Marshal.Copy(bytes, 0, rgbOutputFileContents[0], bytes.Length);

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Interface to the VS shell object we use to tell our progress while we are generating
        /// </summary>
        internal IVsGeneratorProgress CodeGeneratorProgress { get; private set; }

        /// <summary>
        /// The method that does the actual work of generating code given the input file
        /// </summary>
        /// <returns>The generated code file as a byte-array</returns>
        private async Task<byte[]> GenerateCodeAsync(string filename, string inputFileContent)
        {
            var (globals, state, diags, exception) = await ScriptRunner.RunAsync(filename, ScriptRunner.GetMatchingProject(filename), inputFileContent);

            foreach (var diag in diags)
            {
                var span = diag.Location.GetLineSpan().StartLinePosition;

                if (diag.Severity == 0 || diag.IsWarningAsError)
                {
                    GeneratorError((uint)diag.WarningLevel, diag.GetMessage(), (uint)span.Line, (uint)span.Character);
                }
                else
                {
                    GeneratorWarning((uint)diag.WarningLevel, diag.GetMessage(), (uint)span.Line, (uint)span.Character);
                }
            }

            if (state == null)
                return null;

            if (exception != null)
                GeneratorError(10u, exception.Message, 0u, 0u);

            await globals.Output.FlushAsync();

            return globals.OutputStream.ToArray();
        }

        /// <summary>
        /// Method that will communicate an error via the shell callback mechanism
        /// </summary>
        /// <param name="level">Level or severity</param>
        /// <param name="message">Text displayed to the user</param>
        /// <param name="line">Line number of error</param>
        /// <param name="column">Column number of error</param>
        private void GeneratorError(uint level, string message, uint line, uint column) => CodeGeneratorProgress?.GeneratorError(0, level, message, line, column);

        /// <summary>
        /// Method that will communicate a warning via the shell callback mechanism
        /// </summary>
        /// <param name="level">Level or severity</param>
        /// <param name="message">Text displayed to the user</param>
        /// <param name="line">Line number of warning</param>
        /// <param name="column">Column number of warning</param>
        private void GeneratorWarning(uint level, string message, uint line, uint column) => CodeGeneratorProgress?.GeneratorError(1, level, message, line, column);
    }
}
#endif
# Scry
![Icon](/Scry/Resources/ScryPackage.png)

Visual Studio extension that provides the ability to run C# scripts, giving them access to the current Roslyn workspace.

## Getting started
*Visual Studio 2017+ required.*

### 1. Install extension
Download the VSIX file from the [Releases](../releases), and open any project in Visual Studio.

### 2. Create a Scry script
Right-click on a project or folder, and select `Add > New Item...`. Scroll a bit, and create a new `Scry Script`.

### 3. Run the script
Right-click on the `.csx` script, and select `Run script with Scry`.

## Features
### Imports
By default, the following namespaces (and the matching references) are imported in Scry scripts:
* System
* System.Collections.Generic
* System.Linq
* System.Reflection
* System.Threading.Tasks
* Microsoft.CodeAnalysis
* Microsoft.CodeAnalysis.CSharp
* Microsoft.CodeAnalysis.CSharp.Syntax

### Globals
Properties and methods of the [`Globals`](./Scry/ScriptRunner.cs) class are globally available in Scry scripts. Its definition is (roughly):
```csharp
class Globals
{
    // Reference to itself.
    public Globals Context => this;

    // VS Workspace.
    public VisualStudioWorkspace Workspace { get; }
    
    // Project in which the script file is.
    public Project Project { get; }

    // TextWriter for the generated file.
    public TextWriter Output { get; }
    
    // Extension of the generated file (default: '.g.cs').
    public string Extension { get; set; }
  
    // Path of the script file.
    public string ScriptFile { get; }

    // Indentation used by WriteIndentation().
    public int Indentation { get; set; }
    
    // Alias for 'Indentation'.
    public int Indent { get; set; }

    // Whether or not WriteLine() methods should automatically
    // call WriteIndentation() before writing the given arguments.
    public bool AutoWriteIndentation { get; set; }

    // Shortcuts to Output.WriteLine.
    public Globals WriteLine(string format, params object[] args);
    public Globals WriteLine(params object[] args);
    
    // Shortcuts to Output.Write.
    public Globals Write(string format, params object[] args);
    public Globals Write(params object[] args);

    // Utility to write multiple using statements.
    public Globals WriteUsings(params string[] usings);

    // Utilities to write the start and end of a namespace.
    public Globals WriteNamespace(string @namespace);
    public Globals WriteEnd();

    // Change indentation.
    public Globals WriteIndentation();
    public Globals IncreaseIndentation(int indent = 4);
    public Globals DecreaseIndentation(int indent = 4);

    // Get root nodes, syntax trees and semantic models.
    public SyntaxNode Syntax(string filename);
    public CSharpSyntaxTree Tree(string filename);
    public SemanticModel Model(string filename);

    public IEnumerable<SyntaxNode> Syntaxes { get; }
    public IEnumerable<CSharpSyntaxTree> Trees { get; }
    public IEnumerable<SemanticModel> Models { get; }
}
```

## Example script
```csharp
AutoWriteIndentation = true;

Context
    .WriteUsings("System", "System.Reflection")
    .WriteLine()
    .WriteNamespace("TestNamespace")

    .WriteLine("public static class Files")
    .WriteLine('{')
    .IncreaseIndentation(4);

foreach (CSharpSyntaxTree tree in Trees)
    Context.WriteLine("public static readonly string {0} = \"{1}\";",
                      Path.GetFileNameWithoutExtension(tree.FilePath ?? ""), tree.FilePath);
    
Context
    .DecreaseIndentation(4)
    .WriteLine('}')

    .WriteEnd();
```

## Nested generated file
You can nest the generated file by copying and pasting the following snippet in the `.csproj` file, and replacing the filenames accordingly.

```xml
<ItemGroup>
  <None Update="ScryScript.csx" />
  <Compile Update="ScryScript.g.cs">
    <DesignTime>True</DesignTime>
    <AutoGen>True</AutoGen>
    <DependentUpon>ScryScript.csx</DependentUpon>
  </Compile>
</ItemGroup>
```

## Why not [Scripty](https://github.com/daveaglick/Scripty)?
Scripty is much more stable, and has more features than Scry. However, using the former was impossible for me, due to [Scripty#50](https://github.com/daveaglick/Scripty/issues/50).

Requiring a build for code generation simply isn't possible for me, which is why I created Scry instead. Unlike Scripty, it should be able to run in any C# project, even new ones.

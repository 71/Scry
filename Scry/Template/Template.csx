AutoWriteIndentation = true;

Context
    .WriteUsings("System", "System.Reflection")
    .WriteLine()
    .WriteNamespace("$rootnamespace$")

    .WriteLine("public static class $safeitemname$")
    .WriteLine('{')
    .IncreaseIndentation(4)

    .DecreateIndentation(4)
    .WriteLine('}')

    .WriteEnd();
namespace WebAssemblyInfo;

public class Embedder
{
    public readonly string TemplateModulePath;
    public readonly string OutputModulePath;
    public readonly string AssemblyFilePath;

    public Embedder(string templateModule, string outputModule, string assemblyFile)
    {
        TemplateModulePath = templateModule;
        OutputModulePath = outputModule;
        AssemblyFilePath = assemblyFile;
    }
}
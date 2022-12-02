using System.Threading.Tasks;

namespace WebAssemblyInfo;

public class Program
{
    public static bool Verbose => false;
    public static bool Verbose2 => false;
    public static bool PrintOffsets => false;

    public static async Task Main(string[] args)
    {

        string inFile = "template.wasm";
        string outFile = "out.wasm";
        string asmFile = "sample.dll";

        await new Embedder(inFile, outFile, asmFile).Embed();
    }
}
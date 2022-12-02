using System.Collections.Generic;
using System.IO;

namespace WebAssemblyInfo.WebCIL;

public record Model(int Version, IEnumerable<Module> Modules);

public record Module(string Name, Stream byteStream);


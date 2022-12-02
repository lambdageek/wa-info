using WebAssemblyInfo.Idx;

namespace WebAssemblyInfo.WebCIL;

// All the info we capture from reading the template
//
public record EmbeddingTemplate(GlobalIdx VersionGlobalIdx,
                                GlobalIdx DescriptorLengthGlobalIdx,
                                DataIdx DescriptorDataIdx,
                                GlobalIdx Module0LengthGlobalIdx,
                                DataIdx Module0DataIdx)
{
    public EmbeddingTemplate() : this(default!, default!, default!, default!, default!) { }
}

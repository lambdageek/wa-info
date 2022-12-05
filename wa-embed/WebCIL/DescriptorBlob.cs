
namespace WebAssemblyInfo.WebCIL;

public struct DescriptorBlob
{
    // image count as a uint32, followed by the image data
    public ImageDescriptorBlob[] Images;
}


public struct ImageDescriptorBlob
{
    public uint BlobSize; // size of the blob in bytes, including this field

    // path length in bytes as a uint32, followed by the path (UTF-8 encoded, terminated by a nul)
    public byte[] Path;
    // content length in bytes  as a uint32, followed by the content
    public uint ContentSize;
}
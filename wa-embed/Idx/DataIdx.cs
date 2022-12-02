namespace WebAssemblyInfo.Idx;

public record struct DataIdx(uint Index)
{
    public static implicit operator DataIdx(uint index) => new(index);
}

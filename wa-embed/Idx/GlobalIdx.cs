namespace WebAssemblyInfo.Idx;

public record struct GlobalIdx(uint Index)
{
    public static implicit operator GlobalIdx(uint index) => new(index);
}

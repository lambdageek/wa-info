namespace WebAssemblyInfo.Idx;

public record struct FuncIdx(uint Index)
{
    public static implicit operator FuncIdx(uint index) => new(index);
}

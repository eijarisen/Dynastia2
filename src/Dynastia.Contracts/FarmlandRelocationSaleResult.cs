namespace Dynastia.Contracts;

public sealed record FarmlandRelocationSaleResult(
    int ParcelCount,
    int LivestockCount,
    decimal Proceeds)
{
    public static FarmlandRelocationSaleResult None { get; } =
        new(0, 0, 0m);
}

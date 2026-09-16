namespace Dynastia.Contracts;

public sealed record AppearanceSnapshot(
    HairColor HairGeneA,
    HairColor HairGeneB,
    HairColor HairColor,
    HairTexture HairTexture,
    int GreyingStartAge,
    BaldingTendency BaldingTendency,
    BeardDensity BeardDensity);

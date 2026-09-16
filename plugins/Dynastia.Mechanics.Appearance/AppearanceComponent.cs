using Dynastia.Contracts;

namespace Dynastia.Mechanics.Appearance;

public sealed class AppearanceComponent
{
    public HairColor HairGeneA { get; set; }

    public HairColor HairGeneB { get; set; }

    public HairColor HairColor { get; set; }

    public HairTexture HairTexture { get; set; }

    public int GreyingStartAge { get; set; } = 50;

    public BaldingTendency BaldingTendency { get; set; }

    public BeardDensity BeardDensity { get; set; }
}

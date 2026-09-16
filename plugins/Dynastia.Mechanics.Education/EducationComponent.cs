namespace Dynastia.Mechanics.Education;

public sealed class EducationComponent
{
    public int Level { get; set; }

    // Added after the first historical-education pass. Old saves that lack
    // this flag default to false so the compatibility migration in the
    // service can run once, while new-game values (including a legitimate
    // level 0) remain authoritative.
    public bool IsInitialized { get; set; }
}

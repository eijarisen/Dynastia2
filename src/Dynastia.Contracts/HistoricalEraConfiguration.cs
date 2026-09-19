namespace Dynastia.Contracts;

public static class HistoricalEraConfiguration
{
    public static string GetDisplayName(
        int year)
    {
        return year switch
        {
            <= 1771 => "Polish–Lithuanian Commonwealth",
            <= 1794 => "Age of Partitions",
            <= 1806 => "Partitioned Lands",
            <= 1814 => "Duchy of Warsaw",
            <= 1862 => "Partition Era",
            <= 1913 => "Late Partition Era",
            <= 1917 => "First World War",
            <= 1921 => "Reborn Poland",
            <= 1938 => "Second Polish Republic",
            <= 1944 => "Second World War",
            <= 1955 => "Postwar Reconstruction",
            <= 1988 => "Polish People's Republic",
            _ => "Third Polish Republic"
        };
    }
}

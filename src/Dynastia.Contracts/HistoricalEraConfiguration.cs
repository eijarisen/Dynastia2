namespace Dynastia.Contracts;

public static class HistoricalEraConfiguration
{
    public static string GetDisplayName(
        int year)
    {
        return year switch
        {
            < 1800 => "Early Modern",
            < 1850 => "Early Industrial",
            < 1914 => "Industrial",
            < 1946 => "Modernizing",
            < 1990 => "Postwar",
            _ => "Contemporary"
        };
    }
}

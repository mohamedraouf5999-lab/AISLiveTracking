namespace AISLiveTracking.API.Data.Services;

public static class NavStatusMapper
{
    public static string GetText(int? navStatus)
    {
        return navStatus switch
        {
            0 => "Under way using engine",
            1 => "At anchor",
            2 => "Not under command",
            3 => "Restricted manoeuvrability",
            4 => "Constrained by her draught",
            5 => "Moored",
            6 => "Aground",
            7 => "Engaged in fishing",
            8 => "Under way sailing",
            9 => "Reserved for future amendment of navigational status",
            10 => "Reserved for future amendment of navigational status",
            11 => "Power-driven vessel towing astern",
            12 => "Power-driven vessel pushing ahead or towing alongside",
            13 => "Reserved for future use",
            14 => "AIS-SART is active",
            15 => "Not defined",
            _ => "Unknown"
        };
    }
}
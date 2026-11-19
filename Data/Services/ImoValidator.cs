namespace AISLiveTracking.API.Services;

public static class ImoValidator
{
    public static bool IsValid(int imo)
    {
        if (imo < 1000000 || imo > 9999999)
            return false;

        int sum = 0;
        int[] weights = { 7, 6, 5, 4, 3, 2 };

        for (int i = 0; i < 6; i++)
        {
            int digit = (imo / (int)Math.Pow(10, 6 - i)) % 10;
            sum += digit * weights[i];
        }

        int checkDigit = imo % 10;
        return sum % 10 == checkDigit;
    }
}
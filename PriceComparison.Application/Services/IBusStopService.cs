namespace PriceComparison.Api.Services
{
    public interface IBusStopService
    {
        (double Lat, double Lon)? GetStopLocation(string stopId);
        string GetBaseDataFolder(); // פונקציה שתעזור לנו למצוא איפה הקבצים יושבים
    }
}
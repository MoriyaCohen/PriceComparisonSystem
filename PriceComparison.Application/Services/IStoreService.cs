using PriceComparison.Api.DTOs; // <--- חובה! בלי זה הוא לא מזהה את הפונקציה
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PriceComparison.Api.Services
{
    public interface IStoreService // לוודא שכתוב public
    {
        // הפונקציה חייבת להיות כתובה בדיוק ככה
        Task<List<NearbyStoreDto>> FindNearbyStores(double lat, double lon, double radiusKm);
    }
}
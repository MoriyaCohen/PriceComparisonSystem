namespace PriceComparison.Api.DTOs // או ה-Namespace שבו הקובץ נמצא אצלך
{
    public class NearbyStoreDto
    {
        // --- הוספנו את השדות האלו כדי שנוכל לזהות את החנות ---
        public string ChainId { get; set; }
        public string StoreId { get; set; }

        // --- השדות הקיימים ---
        public string StoreName { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public double DistanceKm { get; set; }
        public string MapLink { get; set; }
    }

    public class StoreInternalModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
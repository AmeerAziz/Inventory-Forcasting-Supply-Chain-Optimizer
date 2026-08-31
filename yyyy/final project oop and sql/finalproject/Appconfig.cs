

namespace InventoryOptimizer
{
    public static class AppConfig
    {
     
        public static string ConnectionString { get; set; } =
            "Server=desktop-eu3a7cd;Database=InventoryDB;Trusted_Connection=True;";

     
        public static int DefaultSafetyStock { get; set; } = 50;

      
        public static int DefaultLeadTimeDays { get; set; } = 7;

      
        public static double HotSellerThreshold  { get; set; } =  20.0;  
        public static double DeadStockThreshold  { get; set; } = -20.0;  

      
        public static int ExpiryWarningThresholdDays { get; set; } = 30;

   
        public static int NonPerishableMaxStorageDays { get; set; } = 36500;


        public static double VendorGoodThresholdPct { get; set; } = 85.0;

      
        public static int ReportDefaultMonthsLookback { get; set; } = 6;
    }
}

using System;

namespace InventoryOptimizer.Models
{
   
    public class PerishableProduct : Product
    {

        public DateTime ExpiryDate     { get; set; }
        public int      ShelfLifeDays  { get; set; }   

        public int DaysUntilExpiry => Math.Max(0, (ExpiryDate - DateTime.Today).Days);

    
        public bool IsNearExpiry =>
            DaysUntilExpiry <= AppConfig.ExpiryWarningThresholdDays;

      
        public override int    MaxStorageDays => ShelfLifeDays;
        public override string ProductType    => "Perishable";

        public override string ToString() =>
            base.ToString() + $"  | Expires: {ExpiryDate:yyyy-MM-dd} ({DaysUntilExpiry}d left)";
    }
}
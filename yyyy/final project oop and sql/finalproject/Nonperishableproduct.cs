
namespace InventoryOptimizer.Models
{
   
    public class NonPerishableProduct : Product
    {

        public double WeightKg   { get; set; }
        public double VolumeM3   { get; set; }
        
        public bool RequiresClimateControl { get; set; }

       
        public override int    MaxStorageDays => AppConfig.NonPerishableMaxStorageDays;
        public override string ProductType    => "Non-Perishable";
    }
}

using System;

namespace InventoryOptimizer.Models
{
    
    public abstract class Product
    {
       
        private int    _currentStock;
        private double _averageDailyDemand;
        private double _unitCost;

      
        public int    ProductId   { get; set; }
     public string SKU         { get; set; } = string.Empty;
public string Name        { get; set; } = string.Empty;
public string Category    { get; set; } = string.Empty;
        public int    VendorId    { get; set; }

        
        public int CurrentStock
        {
            get => _currentStock;
            set => _currentStock = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(CurrentStock), "Stock cannot be negative.");
        }

        public double AverageDailyDemand
        {
            get => _averageDailyDemand;
            set => _averageDailyDemand = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(AverageDailyDemand), "Demand cannot be negative.");
        }

        public double UnitCost
        {
            get => _unitCost;
            set => _unitCost = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(UnitCost), "Cost cannot be negative.");
        }

        // ── Ordering parameters ───────────────────────────────────
        public double OrderingCostPerOrder { get; set; }   // S  (fixed cost to place one order)
        public double HoldingCostPerUnit   { get; set; }   // H  (annual cost to hold one unit)
        public int    LeadTimeDays         { get; set; }   // L  (days from order to receipt)

        // ── Demand trend label (set by business logic layer) ──────
        public DemandTrend DemandTrend { get; set; } = DemandTrend.Stable;

        
        public abstract int MaxStorageDays { get; }

        
        public abstract string ProductType { get; }

        public override string ToString() =>
            $"[{ProductType}] {SKU} — {Name}  (Stock: {CurrentStock})";
    }

        public enum DemandTrend
    {
        HotSeller,   // demand change > +20 %
        Stable,      // demand change within ±20 %
        DeadStock    // demand change < -20 %
    }
}

using System;

namespace InventoryOptimizer.Models
{
    public class Vendor
    {
        public int    VendorId          { get; set; }
         public string Name                 { get; set; } = string.Empty;  
        public string ContactEmail         { get; set; } = string.Empty;  
        public string ContactPhone         { get; set; } = string.Empty;

        
        public int PromisedLeadTimeDays { get; set; }

       
        public double ActualLeadTimeDays { get; set; }

     
        public double PerformanceScore { get; set; }

        
        public double LeadTimeVarianceDays =>
            PromisedLeadTimeDays - ActualLeadTimeDays;

        public override string ToString() =>
            $"Vendor #{VendorId}: {Name}  (Score: {PerformanceScore:F1}%)";
    }
}


namespace InventoryOptimizer.Models
{
    public class SalesRecord
    {
        public int      SalesId      { get; set; }
        public int      ProductId    { get; set; }
        public DateTime SaleDate     { get; set; }
        public int      QuantitySold { get; set; }
        public decimal  Revenue      { get; set; }
    }
}

namespace InventoryOptimizer.Models
{
   
    public class ForecastResult
    {
        public int    ProductId        { get; set; }
        public  string ProductName      { get; set; } = string.Empty;

        
        public double EOQ              { get; set; }   
        public double ReorderPoint     { get; set; }  
        public double SafetyStock      { get; set; }   
        public bool   IsBelowROP       { get; set; }   


        public double? WhatIfDemandIncreasePct  { get; set; }
        public double? ProjectedMonthlySales    { get; set; }
        public double? ProjectedEOQ             { get; set; }

        public DemandTrend Trend        { get; set; }
        public double      TrendChangePct { get; set; }
    }
}
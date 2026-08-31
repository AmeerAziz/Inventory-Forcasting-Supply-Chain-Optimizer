
using System;
using InventoryOptimizer.Models;


namespace InventoryOptimizer.Services
{
    public interface IForecastStrategy
    {
        
        ///<param name="product">The product to analyse.</param>6

        ForecastResult Calculate(Product product);
    }
}



namespace InventoryOptimizer.Services
{
   
    public class StandardForecastStrategy : IForecastStrategy
    {
        public ForecastResult Calculate(Product product)
        {
            double annualDemand  = product.AverageDailyDemand * 365;
            double S             = product.OrderingCostPerOrder;
            double H             = product.HoldingCostPerUnit;
            int    leadTime      = product.LeadTimeDays > 0
                                    ? product.LeadTimeDays
                                    : AppConfig.DefaultLeadTimeDays;

           
            double eoq = (H > 0 && S > 0 && annualDemand > 0)
                ? Math.Sqrt((2 * annualDemand * S) / H)
                : 0;


            double safetyStock = AppConfig.DefaultSafetyStock;

           
            double rop = (product.AverageDailyDemand * leadTime) + safetyStock;

            return new ForecastResult
            {
                ProductId    = product.ProductId,
                ProductName  = product.Name,
                EOQ          = Math.Round(eoq, 2),
                SafetyStock  = safetyStock,
                ReorderPoint = Math.Round(rop, 2),
                IsBelowROP   = product.CurrentStock <= rop,
                Trend        = product.DemandTrend
            };
        }
    }
}



namespace InventoryOptimizer.Services
{
   
    public class WhatIfForecastStrategy : IForecastStrategy
    {
        private readonly double _demandIncreasePct;

        /// <param name="demandIncreasePct">
       
        public WhatIfForecastStrategy(double demandIncreasePct)
        {
            _demandIncreasePct = demandIncreasePct;
        }

        public ForecastResult Calculate(Product product)
        {
          
            double projectedDailyDemand =
                product.AverageDailyDemand * (1 + _demandIncreasePct / 100.0);

            double projectedAnnualDemand = projectedDailyDemand * 365;
            double projectedMonthlySales = projectedDailyDemand * 30;

            double S = product.OrderingCostPerOrder;
            double H = product.HoldingCostPerUnit;

       
            double projectedEOQ = (H > 0 && S > 0 && projectedAnnualDemand > 0)
                ? Math.Sqrt((2 * projectedAnnualDemand * S) / H)
                : 0;

           
            var standard = new StandardForecastStrategy().Calculate(product);

            return new ForecastResult
            {
                ProductId              = product.ProductId,
                ProductName            = product.Name,
                EOQ                    = standard.EOQ,
                SafetyStock            = standard.SafetyStock,
                ReorderPoint           = standard.ReorderPoint,
                IsBelowROP             = standard.IsBelowROP,
                Trend                  = product.DemandTrend,

                
                WhatIfDemandIncreasePct = _demandIncreasePct,
                ProjectedMonthlySales   = Math.Round(projectedMonthlySales, 1),
                ProjectedEOQ            = Math.Round(projectedEOQ, 2)
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using InventoryOptimizer.DataAccess;
using InventoryOptimizer.Models;

namespace InventoryOptimizer.Services
{
    public class InventoryService
    {
        private readonly IInventoryRepository _repo;

        public InventoryService(IInventoryRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

      
        public double CalculateEOQ(double annualDemand, double orderingCost, double holdingCost)
        {
            if (holdingCost <= 0 || orderingCost <= 0 || annualDemand <= 0) return 0;
            return Math.Sqrt((2 * annualDemand * orderingCost) / holdingCost);
        }

        

        public double CalculateROP(double avgDailyDemand, int leadTimeDays, double safetyStock)
        {
            return (avgDailyDemand * leadTimeDays) + safetyStock;
        }

       


        public double GetSafetyStock() => AppConfig.DefaultSafetyStock;

       


        public DemandTrend ClassifyDemandTrend(double previousMonthlyDemand, double currentMonthlyDemand)
        {
            if (previousMonthlyDemand == 0) return DemandTrend.Stable;

            double changePct = ((currentMonthlyDemand - previousMonthlyDemand)
                                / previousMonthlyDemand) * 100.0;

            if (changePct > AppConfig.HotSellerThreshold)  return DemandTrend.HotSeller;
            if (changePct < AppConfig.DeadStockThreshold)  return DemandTrend.DeadStock;
            return DemandTrend.Stable;
        }

       
        public List<ForecastResult> RunStandardForecast()
        {
            var products = _repo.GetAllProducts();
            var strategy = new StandardForecastStrategy();

            EnrichWithTrendData(products);

            return products.Select(p => strategy.Calculate(p)).ToList();
        }

       


        public List<ForecastResult> RunWhatIfSimulation(double demandIncreasePct)
        {
            var products = _repo.GetAllProducts();
            var strategy = new WhatIfForecastStrategy(demandIncreasePct);

            EnrichWithTrendData(products);

            return products.Select(p => strategy.Calculate(p)).ToList();
        }

        



        public List<Vendor> GetVendorPerformanceReport()
        {
            var vendors = _repo.GetAllVendors();
            foreach (var v in vendors)
            {
                if (v.ActualLeadTimeDays <= 0)
                {
                    v.PerformanceScore = 0;
                    continue;
                }
                double score = ((double)v.PromisedLeadTimeDays / v.ActualLeadTimeDays) * 100;
                v.PerformanceScore = Math.Min(100, Math.Round(score, 1));
            }
            return vendors.OrderByDescending(v => v.PerformanceScore).ToList();
        }

        // ── 8. Critical stock list (below ROP) ───────────────────
        public List<ForecastResult> GetCriticalStockAlerts()
        {
            return RunStandardForecast()
                .Where(r => r.IsBelowROP)
                .OrderBy(r => r.ProductName)
                .ToList();
        }

        
        private void EnrichWithTrendData(IEnumerable<Product> products)
        {
            foreach (var p in products)
            {
                var history = _repo.GetMonthlySalesHistory(p.ProductId,
                                  AppConfig.ReportDefaultMonthsLookback);

                if (history.Count >= 2)
                {
                    double prev    = history[^2];   // second-to-last month
                    double current = history[^1];   // most recent month
                    double changePct = prev == 0 ? 0
                        : ((current - prev) / prev) * 100.0;

                    p.DemandTrend = ClassifyDemandTrend(prev, current);
                    // Also update AverageDailyDemand from actual history
                    p.AverageDailyDemand = history.Average() / 30.0;
                }
            }
        }
    }
}
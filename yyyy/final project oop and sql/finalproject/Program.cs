
using System;
using Microsoft.Extensions.Configuration;
using InventoryOptimizer.Models;
using InventoryOptimizer.Services;
using InventoryOptimizer.DataAccess;

namespace InventoryOptimizer
{
    class Program
    {
       
        static SqlInventoryRepository repo = null!;
        static InventoryService service = null!;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================");
            Console.WriteLine("   INVENTORY OPTIMIZER — SQL SERVER MODE     ");
            Console.WriteLine("==============================================");
            Console.ResetColor();

            IConfiguration config;
            try
            {
                config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ Cannot read appsettings.json: {ex.Message}");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            // ── Get connection string ─────────────────────────────
            string? connStr = config.GetConnectionString("InventoryDB");
            if (string.IsNullOrEmpty(connStr))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ✗ Connection string 'InventoryDB' missing in appsettings.json");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

          
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  Connecting to SQL Server...");
            Console.ResetColor();

            try
            {
                repo    = new SqlInventoryRepository(connStr);
                service = new InventoryService(repo);

                var test = repo.GetAllProducts();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ Connected! Found {test.Count} products in InventoryDB.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ Connection failed: {ex.Message}");
                Console.WriteLine("    Check: SQL Server running | server name | Schema.sql executed");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            // ── Main menu ─────────────────────────────────────────
            bool running = true;
            while (running)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("─── MAIN MENU ──────────────────────");
                Console.ResetColor();
                Console.WriteLine("  1.  Show all products");
                Console.WriteLine("  2.  ROP report          (usp_CalculateROP)");
                Console.WriteLine("  3.  Critical alerts     (below ROP)");
                Console.WriteLine("  4.  What-If simulation  (usp_WhatIfSimulation)");
                Console.WriteLine("  5.  Vendor performance  (usp_VendorPerformance)");
                Console.WriteLine("  6.  Test EOQ formula    (ufn_TestEOQ)");
                Console.WriteLine("  7.  Run forecast for product (ufn_RunForecast)");
                Console.WriteLine("  8.  Demand trend classifier");
                
                Console.WriteLine("  9.  ADD new product     (usp_AddProduct)");
                Console.WriteLine("  10. DELETE a product    (usp_DeleteProduct)");
                Console.WriteLine("  0.  Exit");
                Console.Write("\nEnter choice: ");

                string choice = Console.ReadLine() ?? "0";
                Console.WriteLine();

                try
                {
                    switch (choice)
                    {
                        case "1":  ShowAllProducts();       break;
                        case "2":  ShowROPReport();         break;
                        case "3":  ShowCriticalAlerts();    break;
                        case "4":  RunWhatIf();             break;
                        case "5":  ShowVendorReport();      break;
                        case "6":  TestEOQFunction();       break;
                        case "7":  RunForecastFunction();   break;
                        case "8":  TestTrendClassifier();   break;
                        case "9":  AddProduct();            break;
                        case "10": DeleteProduct();         break;
                        case "0":  running = false;         break;
                        default:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("  Invalid choice.");
                            Console.ResetColor();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n  ✗ Error: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nGoodbye!");
            Console.ResetColor();
        }

        // ── 1. Show all products ──────────────────────────────────
        static void ShowAllProducts()
        {
            PrintHeader("ALL PRODUCTS");
            var products = repo.GetAllProducts();

            if (products.Count == 0)
            {
                Console.WriteLine("  No products found. Run Schema.sql to add sample data.");
                return;
            }

            foreach (var p in products)
            {
                Console.ForegroundColor = p is PerishableProduct
                    ? ConsoleColor.Magenta : ConsoleColor.White;

                Console.WriteLine($"  [{p.ProductType,-14}] " +
                                  $"ID:{p.ProductId,-4} " +
                                  $"SKU:{p.SKU,-10} " +
                                  $"Name:{p.Name,-25} " +
                                  $"Stock:{p.CurrentStock,-6} " +
                                  $"Demand/day:{p.AverageDailyDemand}");

                if (p is PerishableProduct pp)
                {
                    Console.ForegroundColor = pp.IsNearExpiry
                        ? ConsoleColor.Red : ConsoleColor.DarkGray;
                    Console.WriteLine($"                Expires: {pp.ExpiryDate:yyyy-MM-dd}" +
                                      $"  ({pp.DaysUntilExpiry}d left)" +
                                      (pp.IsNearExpiry ? "  ⚠ NEAR EXPIRY" : ""));
                }
                Console.ResetColor();
            }
            Console.WriteLine($"\n  Total: {products.Count} products");
        }

        // ── 2. ROP Report via usp_CalculateROP ───────────────────
        static void ShowROPReport()
        {
            PrintHeader("ROP REPORT — usp_CalculateROP");
            var results = repo.ExecuteCalculateROP(safetyStock: 50);

            Console.WriteLine($"  {"ID",-4} {"Product",-25} {"Stock",7} {"ROP",8} {"Safety",8} {"Status"}");
            Console.WriteLine(new string('─', 72));

            foreach (var r in results)
            {
                Console.ForegroundColor = r.IsBelowROP
                    ? ConsoleColor.Red : ConsoleColor.Green;
                Console.WriteLine($"  {r.ProductId,-4} {r.Name,-25} " +
                                  $"{r.CurrentStock,7} " +
                                  $"{r.ReorderPoint,8:F1} " +
                                  $"{r.SafetyStock,8} " +
                                  $"{(r.IsBelowROP ? "⚠ CRITICAL" : "OK")}");
                Console.ResetColor();
            }
        }

        // ── 3. Critical alerts ────────────────────────────────────
        static void ShowCriticalAlerts()
        {
            PrintHeader("CRITICAL STOCK ALERTS — usp_CalculateROP");
            var results = repo.ExecuteCalculateROP(50);
            bool any = false;

            foreach (var r in results)
            {
                if (!r.IsBelowROP) continue;
                any = true;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ⚠  {r.Name} (ID:{r.ProductId})");
                Console.ResetColor();
                Console.WriteLine($"     Current Stock : {r.CurrentStock}");
                Console.WriteLine($"     Reorder Point : {r.ReorderPoint:F1}");
                Console.WriteLine($"     Safety Stock  : {r.SafetyStock}");
                Console.WriteLine();
            }

            if (!any)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ All products are above reorder point.");
                Console.ResetColor();
            }
        }

        // ── 4. What-If via usp_WhatIfSimulation ──────────────────
        static void RunWhatIf()
        {
            PrintHeader("WHAT-IF SIMULATION — usp_WhatIfSimulation");
            Console.Write("  Enter demand increase % (e.g. 30): ");

            if (!double.TryParse(Console.ReadLine(), out double pct))
            {
                Console.WriteLine("  Invalid. Using 30%.");
                pct = 30;
            }

            var results = repo.ExecuteWhatIfSimulation(pct);

            Console.WriteLine($"\n  Simulating +{pct}% demand...\n");
            Console.WriteLine($"  {"Product",-25} {"Curr Demand",12} {"Proj Demand",12} {"Proj Sales/mo",14} {"Proj EOQ",10}");
            Console.WriteLine(new string('─', 80));

            foreach (var r in results)
            {
                Console.WriteLine($"  {r.Name,-25} " +
                                  $"{r.AverageDailyDemand,12:F1} " +
                                  $"{r.ProjectedDailyDemand,12:F1} " +
                                  $"{r.ProjectedMonthlySales,14:F1} " +
                                  $"{r.ProjectedEOQ,10:F1}");
            }
        }

        // ── 5. Vendor Performance via usp_VendorPerformance ──────
        static void ShowVendorReport()
        {
            PrintHeader("VENDOR PERFORMANCE — usp_VendorPerformance");
            var vendors = repo.ExecuteVendorPerformance();

            Console.WriteLine($"  {"Vendor",-22} {"Promised",10} {"Actual",8} {"Variance",10} {"Score",8} {"Rating"}");
            Console.WriteLine(new string('─', 75));

            foreach (var v in vendors)
            {
                Console.ForegroundColor = v.PerformanceScore >= 85
                    ? ConsoleColor.Green : ConsoleColor.Yellow;
                Console.WriteLine($"  {v.Name,-22} " +
                                  $"{v.PromisedLeadTimeDays,10} " +
                                  $"{v.ActualLeadTimeDays,8:F1} " +
                                  $"{v.LeadTimeVariance,10:F1} " +
                                  $"{v.PerformanceScore,8:F1}% " +
                                  $"{v.Rating}");
                Console.ResetColor();
            }
        }

        // ── 6. EOQ via dbo.ufn_TestEOQ ───────────────────────────
        static void TestEOQFunction()
        {
            PrintHeader("EOQ TEST — dbo.ufn_TestEOQ");
            Console.WriteLine("  Formula: EOQ = √(2 × D × S ÷ H)\n");

            Console.Write("  Annual Demand (D)         : ");
            int.TryParse(Console.ReadLine(), out int d);

            Console.Write("  Ordering Cost per order(S): ");
            decimal.TryParse(Console.ReadLine(), out decimal s);

            Console.Write("  Holding Cost per unit  (H): ");
            decimal.TryParse(Console.ReadLine(), out decimal h);

            double eoq = repo.ExecuteTestEOQ(d, s, h);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  SQL Result: EOQ = {eoq:F2} units");
            Console.ResetColor();
        }

        // ── 7. Forecast via dbo.ufn_RunForecast ──────────────────
        static void RunForecastFunction()
        {
            PrintHeader("FORECAST — dbo.ufn_RunForecast");
            Console.WriteLine("  Returns EOQ and ROP for a specific product.\n");

            Console.Write("  Product ID     : ");
            int.TryParse(Console.ReadLine(), out int pid);

            Console.Write("  Annual Demand  : ");
            int.TryParse(Console.ReadLine(), out int demand);

            Console.Write("  Ordering Cost  : ");
            decimal.TryParse(Console.ReadLine(), out decimal oc);

            Console.Write("  Holding Cost   : ");
            decimal.TryParse(Console.ReadLine(), out decimal hc);

            Console.Write("  Daily Demand   : ");
            int.TryParse(Console.ReadLine(), out int dd);

            Console.Write("  Lead Time Days : ");
            int.TryParse(Console.ReadLine(), out int lt);

            var results = repo.ExecuteRunForecast(pid, demand, oc, hc, dd, lt);

            Console.ForegroundColor = ConsoleColor.Cyan;
            foreach (var r in results)
            {
                Console.WriteLine($"\n  Product ID : {r.ProductId}");
                Console.WriteLine($"  EOQ        : {r.EOQ:F2} units");
                Console.WriteLine($"  ROP        : {r.ROP:F2} units");
            }
            Console.ResetColor();
        }

        // ── 8. Trend classifier ───────────────────────────────────
        static void TestTrendClassifier()
        {
            PrintHeader("DEMAND TREND CLASSIFIER");
            Console.WriteLine("  >+20% = Hot Seller | <-20% = Dead Stock | else = Stable\n");

            Console.Write("  Previous month sales: ");
            double.TryParse(Console.ReadLine(), out double prev);

            Console.Write("  Current month sales : ");
            double.TryParse(Console.ReadLine(), out double curr);

            var trend = service.ClassifyDemandTrend(prev, curr);
            double changePct = prev == 0 ? 0 : ((curr - prev) / prev) * 100;

            Console.ForegroundColor = trend switch
            {
                DemandTrend.HotSeller => ConsoleColor.Green,
                DemandTrend.DeadStock => ConsoleColor.Red,
                _                     => ConsoleColor.Yellow
            };
            Console.WriteLine($"\n  Change : {changePct:+0.0;-0.0}%");
            Console.WriteLine($"  Trend  : {trend}");
            Console.ResetColor();
        }

        // ── 9. ADD PRODUCT via usp_AddProduct ─────────────────────
        static void AddProduct()
        {
            PrintHeader("ADD NEW PRODUCT — usp_AddProduct");

            // Show vendors
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  Available Vendors:");
            Console.ResetColor();
            foreach (var v in repo.GetAllVendors())
                Console.WriteLine($"    ID:{v.VendorId}  {v.Name}");
            Console.WriteLine();

            string sku      = Prompt("  SKU (e.g. SKU-006)            : ");
            string name     = Prompt("  Product Name                  : ");
            string category = Prompt("  Category                      : ");
            int    vendorId = PromptInt("  Vendor ID                     : ");
            int    stock    = PromptInt("  Current Stock (units)         : ");
            double demand   = PromptDouble("  Avg Daily Demand              : ");
            double unitCost = PromptDouble("  Unit Cost ($)                 : ");
            double ordCost  = PromptDouble("  Ordering Cost per Order ($)   : ");
            double holdCost = PromptDouble("  Holding Cost per Unit ($)     : ");
            int    leadTime = PromptInt("  Lead Time (days)              : ");

            Console.Write("  Type (P=Perishable / N=NonPerishable) : ");
            string typeIn      = (Console.ReadLine() ?? "N").Trim().ToUpper();
            string productType = typeIn == "P" ? "Perishable" : "NonPerishable";

            DateTime? expiryDate    = null;
            int?      shelfLifeDays = null;
            double?   weightKg      = null;
            double?   volumeM3      = null;

            if (productType == "Perishable")
            {
                Console.Write("  Expiry Date (yyyy-MM-dd)      : ");
                if (DateTime.TryParse(Console.ReadLine(), out DateTime exp))
                    expiryDate = exp;
                else
                {
                    expiryDate = DateTime.Today.AddDays(30);
                    Console.WriteLine($"  Invalid date — defaulting to {expiryDate:yyyy-MM-dd}");
                }
                shelfLifeDays = PromptInt("  Shelf Life Days               : ");
            }
            else
            {
                weightKg = PromptDouble("  Weight (kg)                   : ");
                volumeM3 = PromptDouble("  Volume (m³)                   : ");
            }

            // Confirm
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  ── Confirm ───────────────────────────────");
            Console.ResetColor();
            Console.WriteLine($"  SKU: {sku}  Name: {name}  Type: {productType}");
            Console.WriteLine($"  Stock: {stock}  Demand/day: {demand}  VendorId: {vendorId}");
            Console.Write("\n  Save? (Y/N): ");
            if ((Console.ReadLine() ?? "N").Trim().ToUpper() != "Y")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Cancelled.");
                Console.ResetColor();
                return;
            }

            // Call SQL stored procedure
            int newId = repo.AddProductSP(
                sku, name, category, vendorId, stock,
                demand, unitCost, ordCost, holdCost, leadTime,
                productType, expiryDate, shelfLifeDays,
                weightKg, volumeM3);

            if (newId > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  ✓ Product added via usp_AddProduct — New ID: {newId}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ✗ Failed to add product. Check SQL Server error.");
                Console.ResetColor();
            }
        }

        // ── 10. DELETE PRODUCT via usp_DeleteProduct ──────────────
        static void DeleteProduct()
        {
            PrintHeader("DELETE PRODUCT — usp_DeleteProduct");

            // Show all products
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  Current Products:");
            Console.ResetColor();

            var products = repo.GetAllProducts();
            if (products.Count == 0)
            {
                Console.WriteLine("  No products in database.");
                return;
            }

            foreach (var p in products)
                Console.WriteLine($"    ID:{p.ProductId,-4} SKU:{p.SKU,-10} " +
                                  $"Name:{p.Name,-25} Type:{p.ProductType}");

            int id = PromptInt("\n  Enter Product ID to delete (0 = cancel): ");
            if (id == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Cancelled.");
                Console.ResetColor();
                return;
            }

            var product = products.Find(p => p.ProductId == id);
            if (product == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ No product found with ID {id}.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  About to delete: [{product.ProductType}] {product.SKU} — {product.Name}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ⚠ This will also delete all sales history for this product!");
            Console.ResetColor();
            Console.Write("  Type YES to confirm: ");

            if ((Console.ReadLine() ?? "").Trim().ToUpper() != "YES")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Cancelled.");
                Console.ResetColor();
                return;
            }

            // Call SQL stored procedure
            bool deleted = repo.DeleteProductSP(id);

            if (deleted)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  ✓ '{product.Name}' deleted via usp_DeleteProduct.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ Could not delete product ID {id}.");
                Console.ResetColor();
            }
        }

        // ── Input helpers ─────────────────────────────────────────
        static string Prompt(string msg)
        {
            Console.Write(msg);
            return (Console.ReadLine() ?? "").Trim();
        }

        static int PromptInt(string msg)
        {
            while (true)
            {
                Console.Write(msg);
                if (int.TryParse(Console.ReadLine(), out int r)) return r;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Enter a whole number.");
                Console.ResetColor();
            }
        }

        static double PromptDouble(string msg)
        {
            while (true)
            {
                Console.Write(msg);
                if (double.TryParse(Console.ReadLine(), out double r)) return r;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Enter a decimal number.");
                Console.ResetColor();
            }
        }

        static void PrintHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"─── {title} " + new string('─', Math.Max(0, 44 - title.Length)));
            Console.ResetColor();
        }
    }
}
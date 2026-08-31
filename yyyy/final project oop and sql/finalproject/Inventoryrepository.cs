
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using InventoryOptimizer.Models;

namespace InventoryOptimizer.DataAccess
{
    public interface IInventoryRepository
    {
        List<Product>     GetAllProducts();
        Product?          GetProductById(int productId);
        void              SaveProduct(Product product);
        void              DeleteProduct(int productId);
        List<Vendor>      GetAllVendors();
        Vendor?           GetVendorById(int vendorId);
        List<SalesRecord> GetSalesRecords(int productId, int monthsBack);
        List<double>      GetMonthlySalesHistory(int productId, int months);


        int  AddProductSP(
            string sku, string name, string category, int vendorId,
            int currentStock, double avgDailyDemand, double unitCost,
            double orderingCost, double holdingCost, int leadTimeDays,
            string productType,
            DateTime? expiryDate = null, int? shelfLifeDays = null,
            double? weightKg = null, double? volumeM3 = null,
            bool requiresClimate = false);

        bool DeleteProductSP(int productId);

        // ── Stored procedure wrappers ─────────────────────────────
        List<RopResult>      ExecuteCalculateROP(int safetyStock = 50);
        List<WhatIfResult>   ExecuteWhatIfSimulation(double demandIncreasePct = 30);
        List<VendorPerfResult> ExecuteVendorPerformance();
        double               ExecuteTestEOQ(int annualDemand, decimal orderingCost, decimal holdingCost);
        List<ForecastSpResult> ExecuteRunForecast(int productId, int annualDemand,
                                decimal orderingCost, decimal holdingCost,
                                int dailyDemand, int leadTimeDays);
    }


    public class RopResult
    {
        public int    ProductId      { get; set; }
        public string Name           { get; set; } = string.Empty;
        public double AverageDailyDemand { get; set; }
        public int    LeadTimeDays   { get; set; }
        public int    SafetyStock    { get; set; }
        public double ReorderPoint   { get; set; }
        public int    CurrentStock   { get; set; }
        public bool   IsBelowROP     { get; set; }
    }

    public class WhatIfResult
    {
        public int    ProductId             { get; set; }
        public string Name                  { get; set; } = string.Empty;
        public double AverageDailyDemand    { get; set; }
        public double ProjectedDailyDemand  { get; set; }
        public double ProjectedMonthlySales { get; set; }
        public double ProjectedEOQ          { get; set; }
    }

    public class VendorPerfResult
    {
        public int    VendorId             { get; set; }
        public string Name                 { get; set; } = string.Empty;
        public int    PromisedLeadTimeDays { get; set; }
        public double ActualLeadTimeDays   { get; set; }
        public double LeadTimeVariance     { get; set; }
        public double PerformanceScore     { get; set; }
        public string Rating               { get; set; } = string.Empty;
    }

    public class ForecastSpResult
    {
        public int    ProductId { get; set; }
        public double EOQ       { get; set; }
        public double ROP       { get; set; }
    }

    // ── Main Repository Implementation ──────
    public class SqlInventoryRepository : IInventoryRepository
    {
        private readonly string _connStr;

        public SqlInventoryRepository(string connectionString)
        {
            _connStr = connectionString
                ?? throw new InvalidOperationException("Connection string cannot be null.");
        }

        private SqlConnection OpenConnection()
        {
            var conn = new SqlConnection(_connStr);
            conn.Open();
            return conn;
        }

        // ────────────────────────────────────────────────────────
        //  PRODUCTS
        // ────────────────────────────────────────────────────────
        public List<Product> GetAllProducts()
        {
            var list = new List<Product>();
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand("SELECT * FROM vw_AllProducts", conn);
            using var rdr  = cmd.ExecuteReader();
            while (rdr.Read())
                list.Add(MapProduct(rdr));
            return list;
        }

        public Product? GetProductById(int productId)
        {
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand(
                "SELECT * FROM vw_AllProducts WHERE ProductId = @id", conn);
            cmd.Parameters.AddWithValue("@id", productId);
            using var rdr = cmd.ExecuteReader();
            return rdr.Read() ? MapProduct(rdr) : null;
        }

        // Uses existing usp_UpsertProduct
        public void SaveProduct(Product product)
        {
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand("usp_UpsertProduct", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ProductId",          product.ProductId);
            cmd.Parameters.AddWithValue("@SKU",                product.SKU);
            cmd.Parameters.AddWithValue("@Name",               product.Name);
            cmd.Parameters.AddWithValue("@Category",           product.Category);
            cmd.Parameters.AddWithValue("@VendorId",           product.VendorId);
            cmd.Parameters.AddWithValue("@CurrentStock",       product.CurrentStock);
            cmd.Parameters.AddWithValue("@AverageDailyDemand", product.AverageDailyDemand);
            cmd.Parameters.AddWithValue("@UnitCost",           product.UnitCost);
            cmd.Parameters.AddWithValue("@OrderingCost",       product.OrderingCostPerOrder);
            cmd.Parameters.AddWithValue("@HoldingCost",        product.HoldingCostPerUnit);
            cmd.Parameters.AddWithValue("@LeadTimeDays",       product.LeadTimeDays);
            cmd.Parameters.AddWithValue("@ProductType",        product.ProductType);
            cmd.ExecuteNonQuery();
        }

       
        public void DeleteProduct(int productId)
        {
            DeleteProductSP(productId);
        }


        public int AddProductSP(
            string sku, string name, string category, int vendorId,
            int currentStock, double avgDailyDemand, double unitCost,
            double orderingCost, double holdingCost, int leadTimeDays,
            string productType,
            DateTime? expiryDate = null, int? shelfLifeDays = null,
            double? weightKg = null, double? volumeM3 = null,
            bool requiresClimate = false)
        {
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand("usp_AddProduct", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@SKU",                sku);
            cmd.Parameters.AddWithValue("@Name",               name);
            cmd.Parameters.AddWithValue("@Category",           category);
            cmd.Parameters.AddWithValue("@VendorId",           vendorId);
            cmd.Parameters.AddWithValue("@CurrentStock",       currentStock);
            cmd.Parameters.AddWithValue("@AverageDailyDemand", avgDailyDemand);
            cmd.Parameters.AddWithValue("@UnitCost",           unitCost);
            cmd.Parameters.AddWithValue("@OrderingCost",       orderingCost);
            cmd.Parameters.AddWithValue("@HoldingCost",        holdingCost);
            cmd.Parameters.AddWithValue("@LeadTimeDays",       leadTimeDays);
            cmd.Parameters.AddWithValue("@ProductType",        productType);

            cmd.Parameters.AddWithValue("@ExpiryDate",
                expiryDate.HasValue ? (object)expiryDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ShelfLifeDays",
                shelfLifeDays.HasValue ? (object)shelfLifeDays.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@WeightKg",
                weightKg.HasValue ? (object)weightKg.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@VolumeM3",
                volumeM3.HasValue ? (object)volumeM3.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@RequiresClimate", requiresClimate);

            var outParam = new SqlParameter("@NewProductId", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(outParam);

            cmd.ExecuteNonQuery();

            return outParam.Value != DBNull.Value
                ? Convert.ToInt32(outParam.Value)
                : -1;
        }




        public bool DeleteProductSP(int productId)
        {
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand("usp_DeleteProduct", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ProductId", productId);

            var outParam = new SqlParameter("@RowsDeleted", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(outParam);

            cmd.ExecuteNonQuery();

            return outParam.Value != DBNull.Value && Convert.ToInt32(outParam.Value) > 0;
        }





        // Calls usp_CalculateROP
        public List<RopResult> ExecuteCalculateROP(int safetyStock = 50)
        {
            var list = new List<RopResult>();
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand("usp_CalculateROP", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@SafetyStock", safetyStock);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                list.Add(new RopResult
                {
                    ProductId           = (int)    rdr["ProductId"],
                    Name                = (string) rdr["Name"],
                    AverageDailyDemand  = (double) rdr["AverageDailyDemand"],
                    LeadTimeDays        = (int)    rdr["LeadTimeDays"],
                    SafetyStock         = (int)    rdr["SafetyStock"],
                    ReorderPoint        = Convert.ToDouble(rdr["ReorderPoint"]),
                    CurrentStock        = (int)    rdr["CurrentStock"],
                    IsBelowROP          = Convert.ToInt32(rdr["IsBelowROP"]) == 1
                });
            return list;
        }

        // Calls usp_WhatIfSimulation
        public List<WhatIfResult> ExecuteWhatIfSimulation(double demandIncreasePct = 30)
        {
            var list = new List<WhatIfResult>();
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand("usp_WhatIfSimulation", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@DemandIncreasePct", demandIncreasePct);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                list.Add(new WhatIfResult
                {
                    ProductId             = (int)    rdr["ProductId"],
                    Name                  = (string) rdr["Name"],
                    AverageDailyDemand    = (double) rdr["AverageDailyDemand"],
                    ProjectedDailyDemand  = Convert.ToDouble(rdr["ProjectedDailyDemand"]),
                    ProjectedMonthlySales = Convert.ToDouble(rdr["ProjectedMonthlySales"]),
                    ProjectedEOQ          = Convert.ToDouble(rdr["ProjectedEOQ"])
                });
            return list;
        }

        // Calls usp_VendorPerformance
        public List<VendorPerfResult> ExecuteVendorPerformance()
        {
            var list = new List<VendorPerfResult>();
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand("usp_VendorPerformance", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                list.Add(new VendorPerfResult
                {
                    VendorId             = (int)    rdr["VendorId"],
                    Name                 = (string) rdr["Name"],
                    PromisedLeadTimeDays = (int)    rdr["PromisedLeadTimeDays"],
                    ActualLeadTimeDays   = (double) rdr["ActualLeadTimeDays"],
                    LeadTimeVariance     = Convert.ToDouble(rdr["LeadTimeVariance"]),
                    PerformanceScore     = Convert.ToDouble(rdr["PerformanceScore"]),
                    Rating               = (string) rdr["Rating"]
                });
            return list;
        }

        // Calls dbo.ufn_TestEOQ (SQL scalar function)
        public double ExecuteTestEOQ(int annualDemand, decimal orderingCost, decimal holdingCost)
        {
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand(
                "SELECT dbo.ufn_TestEOQ(@D, @S, @H) AS EOQ", conn);
            cmd.Parameters.AddWithValue("@D", annualDemand);
            cmd.Parameters.AddWithValue("@S", orderingCost);
            cmd.Parameters.AddWithValue("@H", holdingCost);
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToDouble(result) : 0;
        }

        // Calls dbo.ufn_RunForecast (SQL table-valued function)
        public List<ForecastSpResult> ExecuteRunForecast(
            int productId, int annualDemand,
            decimal orderingCost, decimal holdingCost,
            int dailyDemand, int leadTimeDays)
        {
            var list = new List<ForecastSpResult>();
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand(
                "SELECT * FROM dbo.ufn_RunForecast(@PId,@D,@S,@H,@DD,@LT)", conn);
            cmd.Parameters.AddWithValue("@PId", productId);
            cmd.Parameters.AddWithValue("@D",   annualDemand);
            cmd.Parameters.AddWithValue("@S",   orderingCost);
            cmd.Parameters.AddWithValue("@H",   holdingCost);
            cmd.Parameters.AddWithValue("@DD",  dailyDemand);
            cmd.Parameters.AddWithValue("@LT",  leadTimeDays);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                list.Add(new ForecastSpResult
                {
                    ProductId = (int)    rdr["ProductId"],
                    EOQ       = Convert.ToDouble(rdr["EOQ"]),
                    ROP       = Convert.ToDouble(rdr["ROP"])
                });
            return list;
        }

        // ────────────────────────────────────────────────────────
        //  VENDORS
        // ────────────────────────────────────────────────────────
        public List<Vendor> GetAllVendors()
        {
            var list = new List<Vendor>();
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand("SELECT * FROM Vendors", conn);
            using var rdr  = cmd.ExecuteReader();
            while (rdr.Read())
                list.Add(new Vendor
                {
                    VendorId             = (int)    rdr["VendorId"],
                    Name                 = (string) rdr["Name"],
                    ContactEmail         = rdr["ContactEmail"] == DBNull.Value ? "" : (string)rdr["ContactEmail"],
                    ContactPhone         = rdr["ContactPhone"] == DBNull.Value ? "" : (string)rdr["ContactPhone"],
                    PromisedLeadTimeDays = (int)    rdr["PromisedLeadTimeDays"],
                    ActualLeadTimeDays   = (double) rdr["ActualLeadTimeDays"]
                });
            return list;
        }

        public Vendor? GetVendorById(int vendorId)
        {
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand(
                "SELECT * FROM Vendors WHERE VendorId = @id", conn);
            cmd.Parameters.AddWithValue("@id", vendorId);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return null;
            return new Vendor
            {
                VendorId             = (int)    rdr["VendorId"],
                Name                 = (string) rdr["Name"],
                PromisedLeadTimeDays = (int)    rdr["PromisedLeadTimeDays"],
                ActualLeadTimeDays   = (double) rdr["ActualLeadTimeDays"]
            };
        }


        public List<SalesRecord> GetSalesRecords(int productId, int monthsBack)
        {
            var list   = new List<SalesRecord>();
            var cutoff = DateTime.Today.AddMonths(-monthsBack);
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand(
                "SELECT * FROM SalesHistory WHERE ProductId=@pid AND SaleDate>=@cutoff ORDER BY SaleDate",
                conn);
            cmd.Parameters.AddWithValue("@pid",    productId);
            cmd.Parameters.AddWithValue("@cutoff", cutoff);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                list.Add(new SalesRecord
                {
                    SalesId      = (int)      rdr["SalesId"],
                    ProductId    = (int)      rdr["ProductId"],
                    SaleDate     = (DateTime) rdr["SaleDate"],
                    QuantitySold = (int)      rdr["QuantitySold"],
                    Revenue      = (decimal)  rdr["Revenue"]
                });
            return list;
        }

        // Calls usp_GetMonthlySalesHistory
        public List<double> GetMonthlySalesHistory(int productId, int months)
        {
            var result = new List<double>();
            using var conn = OpenConnection();
            using var cmd  = new SqlCommand("usp_GetMonthlySalesHistory", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ProductId", productId);
            cmd.Parameters.AddWithValue("@Months",    months);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                result.Add(Convert.ToDouble(rdr["TotalQty"]));
            return result;
        }


        
        private static Product MapProduct(IDataReader rdr)
        {
            string type = rdr["ProductType"]?.ToString() ?? "NonPerishable";

            Product p = type == "Perishable"
                ? new PerishableProduct
                  {
                      ExpiryDate    = rdr["ExpiryDate"] == DBNull.Value
                                        ? DateTime.MaxValue
                                        : (DateTime)rdr["ExpiryDate"],
                      ShelfLifeDays = rdr["ShelfLifeDays"] == DBNull.Value
                                        ? 365 : (int)rdr["ShelfLifeDays"]
                  }
                : new NonPerishableProduct
                  {
                      WeightKg = rdr["WeightKg"] == DBNull.Value ? 0 : (double)rdr["WeightKg"],
                      VolumeM3 = rdr["VolumeM3"] == DBNull.Value ? 0 : (double)rdr["VolumeM3"]
                  };

            p.ProductId            = (int)    rdr["ProductId"];
            p.SKU                  = (string) rdr["SKU"];
            p.Name                 = (string) rdr["Name"];
            p.Category             = (string) rdr["Category"];
            p.VendorId             = (int)    rdr["VendorId"];
            p.CurrentStock         = (int)    rdr["CurrentStock"];
            p.AverageDailyDemand   = Convert.ToDouble(rdr["AverageDailyDemand"]);
            p.UnitCost             = Convert.ToDouble(rdr["UnitCost"]);
            p.OrderingCostPerOrder = Convert.ToDouble(rdr["OrderingCost"]);
            p.HoldingCostPerUnit   = Convert.ToDouble(rdr["HoldingCost"]);
            p.LeadTimeDays         = (int)    rdr["LeadTimeDays"];
            return p;
        }
    }
}
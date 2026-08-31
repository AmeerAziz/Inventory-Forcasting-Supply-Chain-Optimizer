# Inventory-Forcasting-Supply-Chain-Optimizer
Inventory Forecasting and Supply Chain Optimizer
A C#/.NET console application built as a final project for an OOP + SQL course. It manages inventory, forecasts demand using the Strategy design pattern, and persists data through SQL Server stored procedures. A Windows Forms UI variant is also included.
Features
Inventory management for both perishable and non-perishable products
Demand forecasting via interchangeable strategies (Strategy design pattern)
SQL Server backend using stored procedures for data access
3-tier architecture separating presentation, business logic, and data access
Console UI, with a Windows Forms variant available
Project Structure
```
yyyy/final project oop and sql/
├── final project oop and sql.sln
└── finalproject/
    ├── Program.cs                 # Entry point
    ├── Inventoryservice.cs        # Business logic / service layer
    ├── Inventoryrepository.cs     # Data access layer
    ├── Forecaststrategies.cs      # Strategy pattern forecasting implementations
    ├── Product.cs                 # Base product model
    ├── Nonperishableproduct.cs    # Non-perishable product model
    ├── Perishableproduct.cs       # Perishable product model
    ├── Supportingmodels.cs        # Supporting data models
    ├── Appconfig.cs                # Application configuration
    ├── appsettings.json           # Connection strings / settings (not committed with real credentials)
    └── finalproject.csproj
```
Prerequisites
.NET SDK (compatible with the project's target framework)
SQL Server (local instance or remote)
Setup
Clone the repository:
```powershell
   git clone https://github.com/AmeerAziz/Inventory-Forcasting-Supply-Chain-Optimizer.git
   cd Inventory-Forcasting-Supply-Chain-Optimizer
   ```
Configure your database connection in `appsettings.json` (inside the `finalproject` folder) with your own SQL Server instance details.
Run the database schema and stored procedure scripts against your SQL Server instance, if included in the repo.
Build and run:
```powershell
   cd "yyyy/final project oop and sql/finalproject"
   dotnet build
   dotnet run
   ```
Architecture Overview
The application follows a 3-tier architecture:
Presentation layer — console (and Windows Forms) UI for user interaction
Service layer (`Inventoryservice.cs`) — business logic, including forecasting orchestration
Repository layer (`Inventoryrepository.cs`) — data access against SQL Server via stored procedures
Forecasting is implemented with the Strategy pattern (`Forecaststrategies.cs`), allowing different forecasting algorithms to be swapped in without changing the service layer.

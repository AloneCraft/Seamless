// @Optimize
using System.Buffers;
using MemoryPack;
#pragma warning disable CS8981 // 型名には、小文字の ASCII 文字のみが含まれています。このような名前は、プログラミング言語用に予約されている可能性があります。
namespace AdventureWorks2008R2
{
    //データベースコンテナ: 各スキーマへの参照を保持する
    public class Container : IMemoryPackable<Container>
    {
        public Schemas.dbo dbo { get; }
        public Schemas.HumanResources HumanResources { get; } = default!;
        public Schemas.Person Person { get; } = default!;
        public Schemas.Production Production { get; } = default!;
        public Schemas.Purchasing Purchasing { get; } = default!;
        public Schemas.Sales Sales { get; } = default!;
        public static void RegisterFormatter()
        {
            throw new NotImplementedException();
        }
        public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Container? value)
            where TBufferWriter : IBufferWriter<byte>
        {
            throw new NotImplementedException();
        }
        public static void Deserialize(ref MemoryPackReader reader, scoped ref Container? value)
        {
            throw new NotImplementedException();
        }
    }
    //スキーマ定義: 各スキーマが所有するテーブル・ビュー・関数への参照
    namespace Schemas
    {
        public struct dbo
        {
            //テーブル
            public Tables.AWBuildVersion AWBuildVersion { get; }
            public Tables.DatabaseLog DatabaseLog { get; }
            public Tables.ErrorLog ErrorLog { get; }
            //テーブル関数
            public Functions.ufnGetContactInformation ufnGetContactInformation { get; }
        }
        public class HumanResources
        {
            //テーブル
            public Tables.Department Department { get; }
            public Tables.Employee Employee { get; }
            public Tables.EmployeeDepartmentHistory EmployeeDepartmentHistory { get; }
            public Tables.EmployeePayHistory EmployeePayHistory { get; }
            public Tables.JobCandidate JobCandidate { get; }
            public Tables.Shift Shift { get; }
            //ビュー
            public Views.vEmployee vEmployee { get; }
            public Views.vEmployeeDepartment vEmployeeDepartment { get; }
            public Views.vEmployeeDepartmentHistory vEmployeeDepartmentHistory { get; }
            public Views.vJobCandidate vJobCandidate { get; }
            public Views.vJobCandidateEducation vJobCandidateEducation { get; }
            public Views.vJobCandidateEmployment vJobCandidateEmployment { get; }
        }
        public class Person
        {
            //テーブル
            public Tables.Address Address { get; }
            public Tables.AddressType AddressType { get; }
            public Tables.BusinessEntity BusinessEntity { get; }
            public Tables.BusinessEntityAddress BusinessEntityAddress { get; }
            public Tables.BusinessEntityContact BusinessEntityContact { get; }
            public Tables.ContactType ContactType { get; }
            public Tables.CountryRegion CountryRegion { get; }
            public Tables.EmailAddress EmailAddress { get; }
            public Tables.Password Password { get; }
            public Tables.Person PersonTable { get; }
            public Tables.PersonPhone PersonPhone { get; }
            public Tables.PhoneNumberType PhoneNumberType { get; }
            public Tables.StateProvince StateProvince { get; }
            //ビュー
            public Views.vAdditionalContactInfo vAdditionalContactInfo { get; }
            public Views.vStateProvinceCountryRegion vStateProvinceCountryRegion { get; }
        }
        public class Production
        {
            //テーブル
            public Tables.BillOfMaterials BillOfMaterials { get; }
            public Tables.Culture Culture { get; }
            public Tables.Document Document { get; }
            public Tables.Illustration Illustration { get; }
            public Tables.Location Location { get; }
            public Tables.Product Product { get; }
            public Tables.ProductCategory ProductCategory { get; }
            public Tables.ProductCostHistory ProductCostHistory { get; }
            public Tables.ProductDescription ProductDescription { get; }
            public Tables.ProductDocument ProductDocument { get; }
            public Tables.ProductInventory ProductInventory { get; }
            public Tables.ProductListPriceHistory ProductListPriceHistory { get; }
            public Tables.ProductModel ProductModel { get; }
            public Tables.ProductModelIllustration ProductModelIllustration { get; }
            public Tables.ProductModelProductDescriptionCulture ProductModelProductDescriptionCulture { get; }
            public Tables.ProductPhoto ProductPhoto { get; }
            public Tables.ProductProductPhoto ProductProductPhoto { get; }
            public Tables.ProductReview ProductReview { get; }
            public Tables.ProductSubcategory ProductSubcategory { get; }
            public Tables.ScrapReason ScrapReason { get; }
            public Tables.TransactionHistory TransactionHistory { get; }
            public Tables.TransactionHistoryArchive TransactionHistoryArchive { get; }
            public Tables.UnitMeasure UnitMeasure { get; }
            public Tables.WorkOrder WorkOrder { get; }
            public Tables.WorkOrderRouting WorkOrderRouting { get; }
            //ビュー
            public Views.vProductAndDescription vProductAndDescription { get; }
            public Views.vProductModelCatalogDescription vProductModelCatalogDescription { get; }
            public Views.vProductModelInstructions vProductModelInstructions { get; }
        }
        public class Purchasing
        {
            //テーブル
            public Tables.ProductVendor ProductVendor { get; }
            public Tables.PurchaseOrderDetail PurchaseOrderDetail { get; }
            public Tables.PurchaseOrderHeader PurchaseOrderHeader { get; }
            public Tables.ShipMethod ShipMethod { get; }
            public Tables.Vendor Vendor { get; }
            //ビュー
            public Views.vVendorWithAddresses vVendorWithAddresses { get; }
            public Views.vVendorWithContacts vVendorWithContacts { get; }
        }
        public class Sales
        {
            //テーブル
            public Tables.CountryRegionCurrency CountryRegionCurrency { get; }
            public Tables.CreditCard CreditCard { get; }
            public Tables.Currency Currency { get; }
            public Tables.CurrencyRate CurrencyRate { get; }
            public Tables.Customer Customer { get; }
            public Tables.PersonCreditCard PersonCreditCard { get; }
            public Tables.SalesOrderDetail SalesOrderDetail { get; }
            public Tables.SalesOrderHeader SalesOrderHeader { get; }
            public Tables.SalesOrderHeaderSalesReason SalesOrderHeaderSalesReason { get; }
            public Tables.SalesPerson SalesPerson { get; }
            public Tables.SalesPersonQuotaHistory SalesPersonQuotaHistory { get; }
            public Tables.SalesReason SalesReason { get; }
            public Tables.SalesTaxRate SalesTaxRate { get; }
            public Tables.SalesTerritory SalesTerritory { get; }
            public Tables.SalesTerritoryHistory SalesTerritoryHistory { get; }
            public Tables.ShoppingCartItem ShoppingCartItem { get; }
            public Tables.SpecialOffer SpecialOffer { get; }
            public Tables.SpecialOfferProduct SpecialOfferProduct { get; }
            public Tables.Store Store { get; }
            //ビュー
            public Views.vIndividualCustomer vIndividualCustomer { get; }
            public Views.vPersonDemographics vPersonDemographics { get; }
            public Views.vSalesPerson vSalesPerson { get; }
            public Views.vSalesPersonSalesByFiscalYears vSalesPersonSalesByFiscalYears { get; }
            public Views.vStoreWithAddresses vStoreWithAddresses { get; }
            public Views.vStoreWithContacts vStoreWithContacts { get; }
            public Views.vStoreWithDemographics vStoreWithDemographics { get; }
        }
    }
    //テーブル定義: 各テーブルのカラムをプロパティとして定義
    namespace Tables
    {
        //dbo.AWBuildVersion
        public struct AWBuildVersion
        {
            public int SystemInformationID { get; }
            public string DatabaseVersion { get; }
            public DateTime VersionDate { get; }
            public DateTime ModifiedDate { get; }
        }
        //dbo.DatabaseLog
        public struct DatabaseLog
        {
            public int DatabaseLogID { get; }
            public DateTime PostTime { get; }
            public string DatabaseUser { get; }
            public string Event { get; }
            public string? Schema { get; }
            public string? Object { get; }
            public string TSQL { get; }
            public string XmlEvent { get; }
        }
        //dbo.ErrorLog
        public struct ErrorLog
        {
            public int ErrorLogID { get; }
            public DateTime ErrorTime { get; }
            public string UserName { get; }
            public int ErrorNumber { get; }
            public int? ErrorSeverity { get; }
            public int? ErrorState { get; }
            public string? ErrorProcedure { get; }
            public int? ErrorLine { get; }
            public string ErrorMessage { get; }
        }
        //HumanResources.Department
        public struct Department
        {
            public short DepartmentID { get; }
            public string Name { get; }
            public string GroupName { get; }
            public DateTime ModifiedDate { get; }
        }
        //HumanResources.Employee
        public struct Employee
        {
            public int BusinessEntityID { get; }
            public string NationalIDNumber { get; }
            public string LoginID { get; }
            public string? OrganizationNode { get; }
            public short? OrganizationLevel { get; }
            public string JobTitle { get; }
            public DateOnly BirthDate { get; }
            public string MaritalStatus { get; }
            public string Gender { get; }
            public DateOnly HireDate { get; }
            public bool SalariedFlag { get; }
            public short VacationHours { get; }
            public short SickLeaveHours { get; }
            public bool CurrentFlag { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //HumanResources.EmployeeDepartmentHistory
        public struct EmployeeDepartmentHistory
        {
            public int BusinessEntityID { get; }
            public short DepartmentID { get; }
            public byte ShiftID { get; }
            public DateOnly StartDate { get; }
            public DateOnly? EndDate { get; }
            public DateTime ModifiedDate { get; }
        }
        //HumanResources.EmployeePayHistory
        public struct EmployeePayHistory
        {
            public int BusinessEntityID { get; }
            public DateTime RateChangeDate { get; }
            public decimal Rate { get; }
            public byte PayFrequency { get; }
            public DateTime ModifiedDate { get; }
        }
        //HumanResources.JobCandidate
        public struct JobCandidate
        {
            public int JobCandidateID { get; }
            public int? BusinessEntityID { get; }
            public string? Resume { get; }
            public DateTime ModifiedDate { get; }
        }
        //HumanResources.Shift
        public struct Shift
        {
            public byte ShiftID { get; }
            public string Name { get; }
            public TimeSpan StartTime { get; }
            public TimeSpan EndTime { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.Address
        public struct Address
        {
            public int AddressID { get; }
            public string AddressLine1 { get; }
            public string? AddressLine2 { get; }
            public string City { get; }
            public int StateProvinceID { get; }
            public string PostalCode { get; }
            public string? SpatialLocation { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.AddressType
        public struct AddressType
        {
            public int AddressTypeID { get; }
            public string Name { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.BusinessEntity
        public struct BusinessEntity
        {
            public int BusinessEntityID { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.BusinessEntityAddress
        public struct BusinessEntityAddress
        {
            public int BusinessEntityID { get; }
            public int AddressID { get; }
            public int AddressTypeID { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.BusinessEntityContact
        public struct BusinessEntityContact
        {
            public int BusinessEntityID { get; }
            public int PersonID { get; }
            public int ContactTypeID { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.ContactType
        public struct ContactType
        {
            public int ContactTypeID { get; }
            public string Name { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.CountryRegion
        public struct CountryRegion
        {
            public string CountryRegionCode { get; }
            public string Name { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.EmailAddress
        public struct EmailAddress
        {
            public int BusinessEntityID { get; }
            public int EmailAddressID { get; }
            public string? EmailAddress1 { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.Password
        public struct Password
        {
            public int BusinessEntityID { get; }
            public string PasswordHash { get; }
            public string PasswordSalt { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.Person
        public struct Person
        {
            public int BusinessEntityID { get; }
            public string PersonType { get; }
            public bool NameStyle { get; }
            public string? Title { get; }
            public string FirstName { get; }
            public string? MiddleName { get; }
            public string LastName { get; }
            public string? Suffix { get; }
            public int EmailPromotion { get; }
            public string? AdditionalContactInfo { get; }
            public string? Demographics { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.PersonPhone
        public struct PersonPhone
        {
            public int BusinessEntityID { get; }
            public string PhoneNumber { get; }
            public int PhoneNumberTypeID { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.PhoneNumberType
        public struct PhoneNumberType
        {
            public int PhoneNumberTypeID { get; }
            public string Name { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.StateProvince
        public struct StateProvince
        {
            public int StateProvinceID { get; }
            public string StateProvinceCode { get; }
            public string CountryRegionCode { get; }
            public bool IsOnlyStateProvinceFlag { get; }
            public string Name { get; }
            public int TerritoryID { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.BillOfMaterials
        public struct BillOfMaterials
        {
            public int BillOfMaterialsID { get; }
            public int? ProductAssemblyID { get; }
            public int ComponentID { get; }
            public DateTime StartDate { get; }
            public DateTime? EndDate { get; }
            public string UnitMeasureCode { get; }
            public short BOMLevel { get; }
            public decimal PerAssemblyQty { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.Culture
        public struct Culture
        {
            public string CultureID { get; }
            public string Name { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.Document
        public struct Document
        {
            public string DocumentNode { get; }
            public short? DocumentLevel { get; }
            public string Title { get; }
            public int Owner { get; }
            public bool FolderFlag { get; }
            public string FileName { get; }
            public string FileExtension { get; }
            public string Revision { get; }
            public int ChangeNumber { get; }
            public byte Status { get; }
            public string? DocumentSummary { get; }
            public byte[]? DocumentData { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.Illustration
        public struct Illustration
        {
            public int IllustrationID { get; }
            public string? Diagram { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.Location
        public struct Location
        {
            public short LocationID { get; }
            public string Name { get; }
            public decimal CostRate { get; }
            public decimal Availability { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.Product
        public struct Product
        {
            public int ProductID { get; }
            public string Name { get; }
            public string ProductNumber { get; }
            public bool MakeFlag { get; }
            public bool FinishedGoodsFlag { get; }
            public string? Color { get; }
            public short SafetyStockLevel { get; }
            public short ReorderPoint { get; }
            public decimal StandardCost { get; }
            public decimal ListPrice { get; }
            public string? Size { get; }
            public string? SizeUnitMeasureCode { get; }
            public string? WeightUnitMeasureCode { get; }
            public decimal? Weight { get; }
            public int DaysToManufacture { get; }
            public string? ProductLine { get; }
            public string? Class { get; }
            public string? Style { get; }
            public int? ProductSubcategoryID { get; }
            public int? ProductModelID { get; }
            public DateTime SellStartDate { get; }
            public DateTime? SellEndDate { get; }
            public DateTime? DiscontinuedDate { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductCategory
        public struct ProductCategory
        {
            public int ProductCategoryID { get; }
            public string Name { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductCostHistory
        public struct ProductCostHistory
        {
            public int ProductID { get; }
            public DateTime StartDate { get; }
            public DateTime? EndDate { get; }
            public decimal StandardCost { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductDescription
        public struct ProductDescription
        {
            public int ProductDescriptionID { get; }
            public string Description { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductDocument
        public struct ProductDocument
        {
            public int ProductID { get; }
            public string DocumentNode { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductInventory
        public struct ProductInventory
        {
            public int ProductID { get; }
            public short LocationID { get; }
            public string Shelf { get; }
            public byte Bin { get; }
            public short Quantity { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductListPriceHistory
        public struct ProductListPriceHistory
        {
            public int ProductID { get; }
            public DateTime StartDate { get; }
            public DateTime? EndDate { get; }
            public decimal ListPrice { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductModel
        public struct ProductModel
        {
            public int ProductModelID { get; }
            public string Name { get; }
            public string? CatalogDescription { get; }
            public string? Instructions { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductModelIllustration
        public struct ProductModelIllustration
        {
            public int ProductModelID { get; }
            public int IllustrationID { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductModelProductDescriptionCulture
        public struct ProductModelProductDescriptionCulture
        {
            public int ProductModelID { get; }
            public int ProductDescriptionID { get; }
            public string CultureID { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductPhoto
        public struct ProductPhoto
        {
            public int ProductPhotoID { get; }
            public byte[]? ThumbNailPhoto { get; }
            public string? ThumbnailPhotoFileName { get; }
            public byte[]? LargePhoto { get; }
            public string? LargePhotoFileName { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductProductPhoto
        public struct ProductProductPhoto
        {
            public int ProductID { get; }
            public int ProductPhotoID { get; }
            public bool Primary { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductReview
        public struct ProductReview
        {
            public int ProductReviewID { get; }
            public int ProductID { get; }
            public string ReviewerName { get; }
            public DateTime ReviewDate { get; }
            public string EmailAddress { get; }
            public int Rating { get; }
            public string? Comments { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ProductSubcategory
        public struct ProductSubcategory
        {
            public int ProductSubcategoryID { get; }
            public int ProductCategoryID { get; }
            public string Name { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.ScrapReason
        public struct ScrapReason
        {
            public short ScrapReasonID { get; }
            public string Name { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.TransactionHistory
        public struct TransactionHistory
        {
            public int TransactionID { get; }
            public int ProductID { get; }
            public int ReferenceOrderID { get; }
            public int ReferenceOrderLineID { get; }
            public DateTime TransactionDate { get; }
            public string TransactionType { get; }
            public int Quantity { get; }
            public decimal ActualCost { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.TransactionHistoryArchive
        public struct TransactionHistoryArchive
        {
            public int TransactionID { get; }
            public int ProductID { get; }
            public int ReferenceOrderID { get; }
            public int ReferenceOrderLineID { get; }
            public DateTime TransactionDate { get; }
            public string TransactionType { get; }
            public int Quantity { get; }
            public decimal ActualCost { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.UnitMeasure
        public struct UnitMeasure
        {
            public string UnitMeasureCode { get; }
            public string Name { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.WorkOrder
        public struct WorkOrder
        {
            public int WorkOrderID { get; }
            public int ProductID { get; }
            public int OrderQty { get; }
            public int StockedQty { get; }
            public short ScrappedQty { get; }
            public DateTime StartDate { get; }
            public DateTime? EndDate { get; }
            public DateTime DueDate { get; }
            public short? ScrapReasonID { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.WorkOrderRouting
        public struct WorkOrderRouting
        {
            public int WorkOrderID { get; }
            public int ProductID { get; }
            public short OperationSequence { get; }
            public short LocationID { get; }
            public DateTime ScheduledStartDate { get; }
            public DateTime ScheduledEndDate { get; }
            public DateTime? ActualStartDate { get; }
            public DateTime? ActualEndDate { get; }
            public decimal? ActualResourceHrs { get; }
            public decimal PlannedCost { get; }
            public decimal? ActualCost { get; }
            public DateTime ModifiedDate { get; }
        }
        //Purchasing.ProductVendor
        public struct ProductVendor
        {
            public int ProductID { get; }
            public int BusinessEntityID { get; }
            public int AverageLeadTime { get; }
            public decimal StandardPrice { get; }
            public decimal? LastReceiptCost { get; }
            public DateTime? LastReceiptDate { get; }
            public int MinOrderQty { get; }
            public int MaxOrderQty { get; }
            public int? OnOrderQty { get; }
            public string UnitMeasureCode { get; }
            public DateTime ModifiedDate { get; }
        }
        //Purchasing.PurchaseOrderDetail
        public struct PurchaseOrderDetail
        {
            public int PurchaseOrderID { get; }
            public int PurchaseOrderDetailID { get; }
            public DateTime DueDate { get; }
            public short OrderQty { get; }
            public int ProductID { get; }
            public decimal UnitPrice { get; }
            public decimal LineTotal { get; }
            public decimal ReceivedQty { get; }
            public decimal RejectedQty { get; }
            public decimal StockedQty { get; }
            public DateTime ModifiedDate { get; }
        }
        //Purchasing.PurchaseOrderHeader
        public struct PurchaseOrderHeader
        {
            public int PurchaseOrderID { get; }
            public byte RevisionNumber { get; }
            public byte Status { get; }
            public int EmployeeID { get; }
            public int VendorID { get; }
            public int ShipMethodID { get; }
            public DateTime OrderDate { get; }
            public DateTime? ShipDate { get; }
            public decimal SubTotal { get; }
            public decimal TaxAmt { get; }
            public decimal Freight { get; }
            public decimal TotalDue { get; }
            public DateTime ModifiedDate { get; }
        }
        //Purchasing.ShipMethod
        public struct ShipMethod
        {
            public int ShipMethodID { get; }
            public string Name { get; }
            public decimal ShipBase { get; }
            public decimal ShipRate { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Purchasing.Vendor
        public struct Vendor
        {
            public int BusinessEntityID { get; }
            public string AccountNumber { get; }
            public string Name { get; }
            public byte CreditRating { get; }
            public bool PreferredVendorStatus { get; }
            public bool ActiveFlag { get; }
            public string? PurchasingWebServiceURL { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.CountryRegionCurrency
        public struct CountryRegionCurrency
        {
            public string CountryRegionCode { get; }
            public string CurrencyCode { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.CreditCard
        public struct CreditCard
        {
            public int CreditCardID { get; }
            public string CardType { get; }
            public string CardNumber { get; }
            public byte ExpMonth { get; }
            public short ExpYear { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.Currency
        public struct Currency
        {
            public string CurrencyCode { get; }
            public string Name { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.CurrencyRate
        public struct CurrencyRate
        {
            public int CurrencyRateID { get; }
            public DateTime CurrencyRateDate { get; }
            public string FromCurrencyCode { get; }
            public string ToCurrencyCode { get; }
            public decimal AverageRate { get; }
            public decimal EndOfDayRate { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.Customer
        public struct Customer
        {
            public int CustomerID { get; }
            public int? PersonID { get; }
            public int? StoreID { get; }
            public int? TerritoryID { get; }
            public string AccountNumber { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.PersonCreditCard
        public struct PersonCreditCard
        {
            public int BusinessEntityID { get; }
            public int CreditCardID { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SalesOrderDetail
        public struct SalesOrderDetail
        {
            public int SalesOrderID { get; }
            public int SalesOrderDetailID { get; }
            public string? CarrierTrackingNumber { get; }
            public short OrderQty { get; }
            public int ProductID { get; }
            public int SpecialOfferID { get; }
            public decimal UnitPrice { get; }
            public decimal UnitPriceDiscount { get; }
            public decimal LineTotal { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SalesOrderHeader
        public struct SalesOrderHeader
        {
            public int SalesOrderID { get; }
            public byte RevisionNumber { get; }
            public DateTime OrderDate { get; }
            public DateTime DueDate { get; }
            public DateTime? ShipDate { get; }
            public byte Status { get; }
            public bool OnlineOrderFlag { get; }
            public string SalesOrderNumber { get; }
            public string? PurchaseOrderNumber { get; }
            public string? AccountNumber { get; }
            public int CustomerID { get; }
            public int? SalesPersonID { get; }
            public int? TerritoryID { get; }
            public int BillToAddressID { get; }
            public int ShipToAddressID { get; }
            public int ShipMethodID { get; }
            public int? CreditCardID { get; }
            public string? CreditCardApprovalCode { get; }
            public int? CurrencyRateID { get; }
            public decimal SubTotal { get; }
            public decimal TaxAmt { get; }
            public decimal Freight { get; }
            public decimal TotalDue { get; }
            public string? Comment { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SalesOrderHeaderSalesReason
        public struct SalesOrderHeaderSalesReason
        {
            public int SalesOrderID { get; }
            public int SalesReasonID { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SalesPerson
        public struct SalesPerson
        {
            public int BusinessEntityID { get; }
            public int? TerritoryID { get; }
            public decimal? SalesQuota { get; }
            public decimal Bonus { get; }
            public decimal CommissionPct { get; }
            public decimal SalesYTD { get; }
            public decimal SalesLastYear { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SalesPersonQuotaHistory
        public struct SalesPersonQuotaHistory
        {
            public int BusinessEntityID { get; }
            public DateTime QuotaDate { get; }
            public decimal SalesQuota { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SalesReason
        public struct SalesReason
        {
            public int SalesReasonID { get; }
            public string Name { get; }
            public string ReasonType { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SalesTaxRate
        public struct SalesTaxRate
        {
            public int SalesTaxRateID { get; }
            public int StateProvinceID { get; }
            public byte TaxType { get; }
            public decimal TaxRate { get; }
            public string Name { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SalesTerritory
        public struct SalesTerritory
        {
            public int TerritoryID { get; }
            public string Name { get; }
            public string CountryRegionCode { get; }
            public string Group { get; }
            public decimal SalesYTD { get; }
            public decimal SalesLastYear { get; }
            public decimal CostYTD { get; }
            public decimal CostLastYear { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SalesTerritoryHistory
        public struct SalesTerritoryHistory
        {
            public int BusinessEntityID { get; }
            public int TerritoryID { get; }
            public DateTime StartDate { get; }
            public DateTime? EndDate { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.ShoppingCartItem
        public struct ShoppingCartItem
        {
            public int ShoppingCartItemID { get; }
            public string ShoppingCartID { get; }
            public int Quantity { get; }
            public int ProductID { get; }
            public DateTime DateCreated { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SpecialOffer
        public struct SpecialOffer
        {
            public int SpecialOfferID { get; }
            public string Description { get; }
            public decimal DiscountPct { get; }
            public string Type { get; }
            public string Category { get; }
            public DateTime StartDate { get; }
            public DateTime EndDate { get; }
            public int MinQty { get; }
            public int? MaxQty { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.SpecialOfferProduct
        public struct SpecialOfferProduct
        {
            public int SpecialOfferID { get; }
            public int ProductID { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Sales.Store
        public struct Store
        {
            public int BusinessEntityID { get; }
            public string Name { get; }
            public int? SalesPersonID { get; }
            public string? Demographics { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
    }
    //ビュー定義: 各ビューのカラムをプロパティとして定義
    namespace Views
    {
        //HumanResources.vEmployee
        public struct vEmployee
        {
            public int BusinessEntityID { get; }
            public string? Title { get; }
            public string FirstName { get; }
            public string? MiddleName { get; }
            public string LastName { get; }
            public string? Suffix { get; }
            public string JobTitle { get; }
            public string? PhoneNumber { get; }
            public string? PhoneNumberType { get; }
            public string? EmailAddress { get; }
            public int EmailPromotion { get; }
            public string AddressLine1 { get; }
            public string? AddressLine2 { get; }
            public string City { get; }
            public string StateProvinceName { get; }
            public string PostalCode { get; }
            public string CountryRegionName { get; }
            public string? AdditionalContactInfo { get; }
        }
        //HumanResources.vEmployeeDepartment
        public struct vEmployeeDepartment
        {
            public int BusinessEntityID { get; }
            public string? Title { get; }
            public string FirstName { get; }
            public string? MiddleName { get; }
            public string LastName { get; }
            public string? Suffix { get; }
            public string JobTitle { get; }
            public string Department { get; }
            public string GroupName { get; }
            public DateOnly StartDate { get; }
        }
        //HumanResources.vEmployeeDepartmentHistory
        public struct vEmployeeDepartmentHistory
        {
            public int BusinessEntityID { get; }
            public string? Title { get; }
            public string FirstName { get; }
            public string? MiddleName { get; }
            public string LastName { get; }
            public string? Suffix { get; }
            public string Shift { get; }
            public string Department { get; }
            public string GroupName { get; }
            public DateOnly StartDate { get; }
            public DateOnly? EndDate { get; }
        }
        //HumanResources.vJobCandidate
        public struct vJobCandidate
        {
            public int JobCandidateID { get; }
            public int? BusinessEntityID { get; }
            public string? Name_Prefix { get; }
            public string? Name_First { get; }
            public string? Name_Middle { get; }
            public string? Name_Last { get; }
            public string? Name_Suffix { get; }
            public string? Skills { get; }
            public string? Addr_Type { get; }
            public string? Addr_Loc_CountryRegion { get; }
            public string? Addr_Loc_State { get; }
            public string? Addr_Loc_City { get; }
            public string? Addr_PostalCode { get; }
            public string? EMail { get; }
            public string? WebSite { get; }
            public DateTime ModifiedDate { get; }
        }
        //HumanResources.vJobCandidateEducation
        public struct vJobCandidateEducation
        {
            public int JobCandidateID { get; }
            public string? Edu_Level { get; }
            public DateTime? Edu_StartDate { get; }
            public DateTime? Edu_EndDate { get; }
            public string? Edu_Degree { get; }
            public string? Edu_Major { get; }
            public string? Edu_Minor { get; }
            public string? Edu_GPA { get; }
            public string? Edu_GPAScale { get; }
            public string? Edu_School { get; }
            public string? Edu_Loc_CountryRegion { get; }
            public string? Edu_Loc_State { get; }
            public string? Edu_Loc_City { get; }
        }
        //HumanResources.vJobCandidateEmployment
        public struct vJobCandidateEmployment
        {
            public int JobCandidateID { get; }
            public DateTime? Emp_StartDate { get; }
            public DateTime? Emp_EndDate { get; }
            public string? Emp_OrgName { get; }
            public string? Emp_JobTitle { get; }
            public string? Emp_Responsibility { get; }
            public string? Emp_FunctionCategory { get; }
            public string? Emp_IndustryCategory { get; }
            public string? Emp_Loc_CountryRegion { get; }
            public string? Emp_Loc_State { get; }
            public string? Emp_Loc_City { get; }
        }
        //Person.vAdditionalContactInfo
        public struct vAdditionalContactInfo
        {
            public int BusinessEntityID { get; }
            public string FirstName { get; }
            public string? MiddleName { get; }
            public string LastName { get; }
            public string? TelephoneNumber { get; }
            public string? TelephoneSpecialInstructions { get; }
            public string? Street { get; }
            public string? City { get; }
            public string? StateProvince { get; }
            public string? PostalCode { get; }
            public string? CountryRegion { get; }
            public string? HomeAddressSpecialInstructions { get; }
            public string? EMailAddress { get; }
            public string? EMailSpecialInstructions { get; }
            public string? EMailTelephoneNumber { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Person.vStateProvinceCountryRegion
        public struct vStateProvinceCountryRegion
        {
            public int StateProvinceID { get; }
            public string StateProvinceCode { get; }
            public bool IsOnlyStateProvinceFlag { get; }
            public string StateProvinceName { get; }
            public int TerritoryID { get; }
            public string CountryRegionCode { get; }
            public string CountryRegionName { get; }
        }
        //Production.vProductAndDescription
        public struct vProductAndDescription
        {
            public int ProductID { get; }
            public string Name { get; }
            public string ProductModel { get; }
            public string CultureID { get; }
            public string Description { get; }
        }
        //Production.vProductModelCatalogDescription
        public struct vProductModelCatalogDescription
        {
            public int ProductModelID { get; }
            public string Name { get; }
            public string? Summary { get; }
            public string? Manufacturer { get; }
            public string? Copyright { get; }
            public string? ProductURL { get; }
            public string? WarrantyPeriod { get; }
            public string? WarrantyDescription { get; }
            public string? NoOfYears { get; }
            public string? MaintenanceDescription { get; }
            public string? Wheel { get; }
            public string? Saddle { get; }
            public string? Pedal { get; }
            public string? BikeFrame { get; }
            public string? Crankset { get; }
            public string? PictureAngle { get; }
            public string? PictureSize { get; }
            public string? ProductPhotoID { get; }
            public string? Material { get; }
            public string? Color { get; }
            public string? ProductLine { get; }
            public string? Style { get; }
            public string? RiderExperience { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Production.vProductModelInstructions
        public struct vProductModelInstructions
        {
            public int ProductModelID { get; }
            public string Name { get; }
            public string? Instructions { get; }
            public int? LocationID { get; }
            public decimal? SetupHours { get; }
            public decimal? MachineHours { get; }
            public decimal? LaborHours { get; }
            public int? LotSize { get; }
            public string? Step { get; }
            public Guid rowguid { get; }
            public DateTime ModifiedDate { get; }
        }
        //Purchasing.vVendorWithAddresses
        public struct vVendorWithAddresses
        {
            public int BusinessEntityID { get; }
            public string Name { get; }
            public string AddressType { get; }
            public string AddressLine1 { get; }
            public string? AddressLine2 { get; }
            public string City { get; }
            public string StateProvinceName { get; }
            public string PostalCode { get; }
            public string CountryRegionName { get; }
        }
        //Purchasing.vVendorWithContacts
        public struct vVendorWithContacts
        {
            public int BusinessEntityID { get; }
            public string Name { get; }
            public string ContactType { get; }
            public string? Title { get; }
            public string FirstName { get; }
            public string? MiddleName { get; }
            public string LastName { get; }
            public string? Suffix { get; }
            public string? PhoneNumber { get; }
            public string? PhoneNumberType { get; }
            public string? EmailAddress { get; }
            public int EmailPromotion { get; }
        }
        //Sales.vIndividualCustomer
        public struct vIndividualCustomer
        {
            public int BusinessEntityID { get; }
            public string? Title { get; }
            public string FirstName { get; }
            public string? MiddleName { get; }
            public string LastName { get; }
            public string? Suffix { get; }
            public string? PhoneNumber { get; }
            public string? PhoneNumberType { get; }
            public string? EmailAddress { get; }
            public int EmailPromotion { get; }
            public string AddressType { get; }
            public string AddressLine1 { get; }
            public string? AddressLine2 { get; }
            public string City { get; }
            public string StateProvinceName { get; }
            public string PostalCode { get; }
            public string CountryRegionName { get; }
            public string? Demographics { get; }
        }
        //Sales.vPersonDemographics
        public struct vPersonDemographics
        {
            public int BusinessEntityID { get; }
            public decimal? TotalPurchaseYTD { get; }
            public DateTime? DateFirstPurchase { get; }
            public DateTime? BirthDate { get; }
            public string? MaritalStatus { get; }
            public string? YearlyIncome { get; }
            public string? Gender { get; }
            public int? TotalChildren { get; }
            public int? NumberChildrenAtHome { get; }
            public string? Education { get; }
            public string? Occupation { get; }
            public bool? HomeOwnerFlag { get; }
            public int? NumberCarsOwned { get; }
        }
        //Sales.vSalesPerson
        public struct vSalesPerson
        {
            public int BusinessEntityID { get; }
            public string? Title { get; }
            public string FirstName { get; }
            public string? MiddleName { get; }
            public string LastName { get; }
            public string? Suffix { get; }
            public string JobTitle { get; }
            public string? PhoneNumber { get; }
            public string? PhoneNumberType { get; }
            public string? EmailAddress { get; }
            public int EmailPromotion { get; }
            public string City { get; }
            public string StateProvinceName { get; }
            public string PostalCode { get; }
            public string CountryRegionName { get; }
            public string? TerritoryName { get; }
            public string? TerritoryGroup { get; }
            public decimal? SalesQuota { get; }
            public decimal SalesYTD { get; }
            public decimal SalesLastYear { get; }
        }
        //Sales.vSalesPersonSalesByFiscalYears
        public struct vSalesPersonSalesByFiscalYears
        {
            public int? SalesPersonID { get; }
            public string? FullName { get; }
            public string JobTitle { get; }
            public string SalesTerritory { get; }
            public decimal? SalesTotal2002 { get; }
            public decimal? SalesTotal2003 { get; }
            public decimal? SalesTotal2004 { get; }
        }
        //Sales.vStoreWithAddresses
        public struct vStoreWithAddresses
        {
            public int BusinessEntityID { get; }
            public string Name { get; }
            public string AddressType { get; }
            public string AddressLine1 { get; }
            public string? AddressLine2 { get; }
            public string City { get; }
            public string StateProvinceName { get; }
            public string PostalCode { get; }
            public string CountryRegionName { get; }
        }
        //Sales.vStoreWithContacts
        public struct vStoreWithContacts
        {
            public int BusinessEntityID { get; }
            public string Name { get; }
            public string ContactType { get; }
            public string? Title { get; }
            public string FirstName { get; }
            public string? MiddleName { get; }
            public string LastName { get; }
            public string? Suffix { get; }
            public string? PhoneNumber { get; }
            public string? PhoneNumberType { get; }
            public string? EmailAddress { get; }
            public int EmailPromotion { get; }
        }
        //Sales.vStoreWithDemographics
        public struct vStoreWithDemographics
        {
            public int BusinessEntityID { get; }
            public string Name { get; }
            public decimal? AnnualSales { get; }
            public decimal? AnnualRevenue { get; }
            public string? BankName { get; }
            public string? BusinessType { get; }
            public int? YearOpened { get; }
            public string? Specialty { get; }
            public int? SquareFeet { get; }
            public string? Brands { get; }
            public string? Internet { get; }
            public int? NumberEmployees { get; }
        }
    }
    //テーブル関数定義: テーブル値関数の戻り値カラムをプロパティとして定義
    namespace Functions
    {
        //dbo.ufnGetContactInformation(@PersonID int)
        public struct ufnGetContactInformation
        {
            public int PersonID { get; }
            public string? FirstName { get; }
            public string? LastName { get; }
            public string? JobTitle { get; }
            public string? BusinessEntityType { get; }
        }
    }
}

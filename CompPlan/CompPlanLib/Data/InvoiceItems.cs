using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompPlanLib.Data
{
    public class InvoiceItems
    {
        public int ChannelID { get; set; }
        public int RegionID { get; set; }
        public int DistrictID { get; set; }
        public int StoreID { get; set; }
        public int EmployeeID { get; set; }
        public int TenderEmployeeID { get; set; }
        public int SaleInvoiceID { get; set; }
        public string CategoryNumber { get; set; }
        public int GlobalProductID { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public int Quantity { get; set; }
        public DateTime DateCreated { get; set; }
        public string Comments { get; set; }
        public int? OriginalSaleInvoiceID { get; set; }
        public string Sku { get; set; }
        public string InvoiceIDByStore { get; set; }
        public string SerialNumber { get; set; }
    }
}

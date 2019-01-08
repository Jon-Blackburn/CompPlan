using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompPlanLib.Data
{
    public class EmployeeItems
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmployeeName { get; set; }
        public int IdNumber { get; set; }
        public string SpecialIdentifier { get; set; }
        public int? DefaultLocation { get; set; }
        public bool AccountDisabled { get; set; }
        public DateTime? startDate { get; set; }
        public int ChannelID { get; set; }
        public string ChannelName { get; set; }
        public int RegionID { get; set; }
        public string RegionName { get; set; }
        public int DistrictID { get; set; }
        public string DistrictName { get; set; }
        public int StoreID { get; set; }
        public string StoreName { get; set; }
        public int RQ4CommissionGroupID { get; set; }
        public DateTime LastCompUpdate { get; set; }
    }
}

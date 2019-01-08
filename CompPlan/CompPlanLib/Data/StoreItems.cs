using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompPlanLib.Data
{
    public class StoreItems
    {
        public int StoreID { get; set; }
        public int StoreManagerID { get; set; }
        public string StoreAbbrev { get; set; }
        public string StoreName { get; set; }
        public int DistrictID { get; set; }
        public int? DistrictManagerID { get; set; }
        public string DistrictName { get; set; }
        public int RegionID { get; set; }
        public int? RegionManagerID { get; set; }
        public string RegionName { get; set; }
        public int ChannelID { get; set; }
        public string ChannelName { get; set; }
        public int ChannelLeaderID { get; set; }
        public DateTime? OpenDate { get; set; }
        public DateTime? CloseDate { get; set; }
        public int totalMonths { get; set; }
        public int? StoreManagerCommissionGroupID { get; set; }
        public int? DistrictManagerCommissionGroupID { get; set; }
        public int? RegionManagerCommissionGroupID { get; set; }
        public int? AreaManagerCommissionGroupID { get; set; }
        public int StoreTypeID { get; set; }
    }

    public class HitTargetStore
    {
        public int StoreID { get; set; }
        public int DistrictID { get; set; }
        public int RegionID { get; set; }
        public int PerformanceTargetMetricID { get; set; }
    }

}

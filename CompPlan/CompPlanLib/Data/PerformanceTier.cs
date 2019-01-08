using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompPlanLib.Data
{
    public class PerformanceTierDataItem
    {
        public int performance_tiers_id { get; set; }
        public int pay_period_id { get; set; }
        public int performance_target_level { get; set; }
        public string tier_code { get; set; }
        public string tier_description { get; set; }
        public int performance_tier_items_id { get; set; }
        public int item_id { get; set; }
        public decimal gross_profit_margin { get; set; }
        public decimal commission_cap { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompPlanLib.Data {
    public class Payout {
        public int target_level { get; set; }
        public int entity_id { get; set; }
        public int payout_id { get; set; }
        public string category_id { get; set; }
        public string sku { get; set; }
        public int quantity { get; set; }
        public decimal value { get; set; }
        public bool goes_to_GP { get; set; }
        public int min_performance_target_metric_id { get; set; }
        public int metric_id_for_range_values { get; set; }
        public bool ignore_min_checks { get; set; }
        public decimal item_gp { get; set; }
        public bool value_is_pct_of_item_gp { get; set; }
    }

    public class TargetPayoutResult {
        public bool has_data { get; set; }
        public decimal total { get; set; }
        public decimal box_attain_percent { get; set; }
    }
}

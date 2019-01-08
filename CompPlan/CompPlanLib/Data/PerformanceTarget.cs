using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompPlanLib.Data
{
    public class PerformanceTarget
    {
        public int pay_period_id { get; set; }
        public int performance_target_id { get; set; }
        public int performance_target_metric_id { get; set; }
        public int metric_ID { get; set; }
        public decimal target_value { get; set; }
        public int target_level_id { get; set; }
        public int store_id { get; set; }
        public string store_name { get; set; }
        public int district_id { get; set; }
        public string district_name { get; set; }
        public int region_id { get; set; }
        public string region_name { get; set; }
        public int channel_id { get; set; }
        public string channel_name { get; set; }
        public int employee_id { get; set; }
        public string employee_name { get; set; }
        public bool roll_up { get; set; }
        public bool used_for_min_check { get; set; }
        public bool for_general_targets { get; set; }
        public bool for_employee_targets { get; set; }
        public int? min_check_metric_ID { get; set; }
    }
}

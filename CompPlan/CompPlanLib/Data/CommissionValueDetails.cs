using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompPlanLib.Data
{
    public class CommissionValueDetails
    {
        public int pay_period_id;
        public int entity_id;
        public int commissions_type_id;
        public int performance_target_id;
        public int min_metric_id = 0;
        public decimal min_metric_value = 0;
        public int metric_id;
        public decimal target_value = 0;
        public decimal metric_value = 0;
        public decimal percent_to_target = 0;
        public decimal accelerator_value = 0;
        public decimal performance_total = 0;
        public int payout_id = 0;
        public string payout_category = "";
        public string payout_sku = "";
        public decimal payout_amount = 0;
        public decimal payout_count = 0;
        public decimal payout_total = 0;
        public int commissions_participation_id = 0;
        public int participation_location_count = 0;
        public int participation_target_count = 0;
        public decimal participation_index_value = 0;
        public int kpi_id = 0;
        public int kpi_points = 0;
        public string mso_serial_number;
        public int payout_location_id = 0;
    }
}

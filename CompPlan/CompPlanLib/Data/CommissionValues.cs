using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompPlanLib.Data
{
    public class CommissionValues
    {
        public int pay_period_id;
        public int entity_id;
        public int channel_id = 0;
        public string channel_name = "";
        public int region_id = 0;
        public string region_name = "";
        public int district_id = 0;
        public string district_name = "";
        public int store_id = 0;
        public int employee_id = 0;
        public string store_name = "";
        public decimal base_gross_profit = 0;
        public decimal commission_gross_profit = 0;
        public decimal gross_profit_margin_percent = 0;
        public decimal gross_profit_margin = 0;
        public decimal performance_total = 0;
        public decimal payout_total = 0;
        public decimal commission_total = 0;
        public decimal participation_total = 0;
        public decimal manual_adjustment = 0;
        public decimal current_boxes_total = 0;
        public decimal min_boxes_total = 0;
        public decimal coupons = 0;
        public decimal kpi_total = 0;
    }
}

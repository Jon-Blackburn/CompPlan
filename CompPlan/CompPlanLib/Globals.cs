using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompPlanLib {
    public class Globals {
        public static class defaults {
            public static decimal default_target_val = 9999.0M;
        }

        // performance target levels
        public enum performance_target_level {
            Unknown = -1,
            All = 0,
            Company = 1,
            Region = 2,
            District = 3,
            Store = 4,
            Employee = 5,
            Employee_Summary = 6,
            Channel = 7
        }

        // BSR params
        public enum BSR_report_level {
            Company = 1,
            Region = 2,
            District = 3,
            Store = 4,
            Employee = 5,
            Employee_Summary = 6,
            Channel = 7
        }

        // metric IDs for BSR values
        public enum BSR_metrics {
            box_metric_id = 119,
            ga_metric_id = 3,
            hpc_metric_id = 10,
            apb_metric_id = 23,
            esec_metric_id = 24,
            GP_metric_id = 26,
            box_target_min_id = 50,
            eSec_target_min_id = 51,
            full_box_metric_id = 64
        }

        // payout spiff levels
        public enum spiff_level {
            Consultant = 1,
            Store = 2,
            MultiStore = 3,
            District = 4,
            Region = 5
        }

        public enum payout_sku_type {
            all_types = 1,
            obey_min_checks = 2,
            ignore_min_checks = 3
        }

        public enum comp_plan_log_message_type {
            error = 1,
            info = 2,
            notification = 3
        }

        public enum rq4_commission_groups {
            sales_consultant = 3,
            sales_consultant_ii = 4,
            home_services_expert = 6,
            z_sales_consultant = 7,
            non_selling_store_manager = 9,
            aw_sales_consultant_north = 10,
            selling_store_manager = 11,
            z_sales_consultant_north = 12,
            z_store_manager = 13,
            store_manager = 14,
            business_consultant = 15,
            area_vice_president = 16,
            regional_sales_director = 17,
            district_sales_manager = 18,
            business_territory_manager = 20,
            business_sales_director = 21,
            trial_regional_sales_director = 22,
            trial_district_sales_manager = 23,
            trial_store_manager = 24,
            trial_selling_store_manager = 25,
            east_west_area_vice_president = 36
        }

        public enum metric_source_type {
            internal_calculated = 0,
            performance_target = 1,
            bsr_metric = 2,
            custom_data = 3
        }

        public enum store_types {
            silver = 1,
            bronze = 2,
            gold = 3,
            platinum = 4,
            not_open = 5,
            other = 6,
            department = 7,
            diamond = 8,
            business = 14
        }

        public enum accelerator_source_types {
            gross_profit = 1,
            commissions_total_month = 2,
            commissions_total_payperiod = 3,
        }

        public class accelerator_source_values {
            public decimal gross_profit { get; set; }
            public decimal commission_total_month { get; set; }
            public decimal commission_total_payperiod { get; set; }
            public decimal amount_to_pay_out_on { get; set; }
            public List<Data.BSRitems> BSRDataItems { get; set; }
            public List<Data.BSRitems> BSRDataItemsMonthly { get; set; }
        }

        // commission types - matches the "code" field in the commissions_type table to get the ID and description
        public static class commission_types {
            public static string Accelerators = "acc";
            public static string Participations = "part";
            public static string Payouts_SKU = "pay_sku";
            public static string Payouts_cat = "pay_cat";
            public static string Payouts_min = "pay_min";
            public static string KPI = "kpi";
            public static string Payouts_MSO = "pay_mso";
            public static string TargetPayouts = "target_pay";
            public static string TargetPayoutTeamBonus = "target_pay_tb";
            public static string TargetPayoutMonthlyBonus = "target_pay_mb";
        }

        // special payout category types - these are used to link a payout detail row for min payouts to a category for description display, which requires special categories to be defined with these codes for the payout to be created
        public static class payout_special_categories {
            public static string MinimumPayoutSC = "MIN5";
            public static string MinimumPayoutSL = "MIN4";
            public static string MinimumPayoutDL = "MIN3";
            public static string MinimumPayoutRL = "MIN2";
            public static string MSOPayoutSC = "MSO5";
            public static string MSOPayoutSL = "MSO4";
        }

        public static int MonthDifference(DateTime lValue, DateTime rValue) {
            return (lValue.Month - rValue.Month) + 12 * (lValue.Year - rValue.Year);
        }

        public class override_parameters {
            private int _comp_group_id;
            private int _location_id;

            public int commission_group_id { get { return _comp_group_id; } set { _comp_group_id = value; } }
            public int location_id { get { return _location_id; } set { _location_id = value; } }

            public override_parameters() {
                _comp_group_id = -1;
                _location_id = -1;
            }
        }
    }
}

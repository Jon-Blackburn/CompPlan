using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace CompPlanLib {
    public class CompPlanLogic {
        #region Global variables

        // data access layer class
        Data.DAL DAL;

        // Global Shared Objects layer class
        Data.GOL GOL;

        // runtime config
        private Data.commissions_configuration commissions_config;

        private List<string> double_play_skus = new List<string> { "CSBNRB000001", "CSBNRB000002", "CSCORB000001", "CSCORB000003", "CSCORB000005", "CSCORB000006", "CSFIRB000023", "CSTWRB000001" };
        private List<string> triple_play_skus = new List<string> { "CSBNRB000001", "CSCORB000004", "CSBNRB000002", "CSCORB000001", "CSCORB000002", "CSCORB000003", "CSCORB000005", "CSCXRB000001", "CSCORB000006", "CSFIRB000023", "CSTWRB000001", "CSTWRB000002" };
        private List<string> mso_RQ4_categories = new List<string> { "10101113", "10101114", "10101115", "10101116" };

        // message lists
        private List<string> info;
        private List<string> errors;
        private List<string> notify;
        private List<string> debug;
        private List<Data.commissions_compplan_log> log_list;

        #endregion

        #region Constructor/Destructor

        public CompPlanLogic(Data.DAL DataAccessLayer, Data.GOL GlobalObjectsLayer = null) {
            DAL = DataAccessLayer;   // just the DAL passed by the callee

            if (GlobalObjectsLayer != null)
                GOL = GlobalObjectsLayer;
            else
                GOL = new Data.GOL(DataAccessLayer);
        }

        #endregion

        #region Data loading and clearing routines

        public void LoadCommissionsConfig(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd) {
            commissions_config = DAL.GetCommissionsConfig(target_level, ppd.pay_period_id);
        }

        public bool ReloadLoadTiersForID(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id) {
            bool retval = false;

            // remove existing
            GOL.performance_tiers_data.RemoveAll(c => c.pay_period_id == ppd.pay_period_id && c.performance_target_level == (int)target_level && c.item_id == entity_id);

            // now re-load with new data
            if (GOL.performance_tiers_data.Where(c => c.pay_period_id == ppd.pay_period_id && c.performance_target_level == (int)target_level && c.item_id == entity_id).FirstOrDefault() == null) {
                GOL.performance_tiers_data.AddRange(DAL.GetPerformanceTiersDataForID(target_level, ppd.pay_period_id, entity_id));
                retval = true;
            }

            return retval;
        }

        #endregion

        #region Global List access routines

        public Data.commissions_configuration GetCurrentCommissionsConfig() {
            return commissions_config;
        }

        public Data.commissions_type GetCommissionsType(string comm_type) {
            return GOL.commissions_types.Where(c => c.code == comm_type).FirstOrDefault();
        }

        public Data.commissions_payouts_category GetCategoryByID(string category_id) {
            return GOL.payouts_category_list.Where(c => c.category_id == category_id).FirstOrDefault();
        }

        public bool MeetsMinBoxTargetValue(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, List<Data.BSRitems> BSRData, out decimal current_boxes_total, out decimal min_boxes_total) {
            // try to get the min value from the performance targets - there should be just one for each SC/store/district/region ID
            int got_min_bsr_target_metric_id = (int)Globals.BSR_metrics.box_target_min_id;

            Data.PerformanceTarget min_performance_target = null;

            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    min_performance_target = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.employee_id == entity_id && p.for_employee_targets == true && p.metric_ID == got_min_bsr_target_metric_id && p.used_for_min_check == true).FirstOrDefault();
                    break;
                case Globals.performance_target_level.Store:
                    min_performance_target = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.store_id == entity_id && p.for_employee_targets == false && p.metric_ID == got_min_bsr_target_metric_id && p.used_for_min_check == true).FirstOrDefault();
                    break;
                case Globals.performance_target_level.District:
                    min_performance_target = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.district_id == entity_id && p.for_employee_targets == false && p.metric_ID == got_min_bsr_target_metric_id && p.used_for_min_check == true).FirstOrDefault();
                    break;
                case Globals.performance_target_level.Region:
                    min_performance_target = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.region_id == entity_id && p.for_employee_targets == false && p.metric_ID == got_min_bsr_target_metric_id && p.used_for_min_check == true).FirstOrDefault();
                    break;
            }

            decimal got_min_target_value = 0.00m;
            decimal got_min_target_BSR_value = 0.00m;

            if (min_performance_target != null) {
                // got a min value to meet, now get the total value from BSR that we need to compare against
                got_min_target_value = min_performance_target.target_value;
                got_min_target_BSR_value = BSRData.Where(b => b.metric_id == min_performance_target.min_check_metric_ID).Sum(b => b.total);
            }

            current_boxes_total = got_min_target_BSR_value;
            min_boxes_total = got_min_target_value;

            // only proceed if either a target isn't set, or it is and we've met it
            return (min_performance_target == null || got_min_target_BSR_value >= got_min_target_value);
        }

        public Data.PerformanceTierDataItem GetPerformanceTier(Globals.performance_target_level target_level, int entity_id) {
            // load the tier for this level, which contains the GP margin %
            return GOL.performance_tiers_data.Where(pt => pt.performance_target_level == (int)target_level && pt.item_id == entity_id).FirstOrDefault();
        }

        public decimal GetManualAdjustment(Globals.performance_target_level target_level, int entity_id) {
            decimal retval = 0.00m;

            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    retval = GOL.manual_adjustments.Where(ma => ma.EmployeeID1 == entity_id).Sum(s => (s.Quantity * s.UnitCommission));
                    break;
                case Globals.performance_target_level.Store:
                    retval = GOL.manual_adjustments.Where(ma => ma.StoreID == entity_id).Sum(s => (s.Quantity * s.UnitCommission));
                    break;
                case Globals.performance_target_level.District:
                    retval = GOL.manual_adjustments.Where(ma => ma.districtID == entity_id).Sum(s => (s.Quantity * s.UnitCommission));
                    break;
                case Globals.performance_target_level.Region:
                    retval = GOL.manual_adjustments.Where(ma => ma.regionID == entity_id).Sum(s => (s.Quantity * s.UnitCommission));
                    break;
                case Globals.performance_target_level.Channel:
                    retval = GOL.manual_adjustments.Where(ma => ma.ChannelID == entity_id).Sum(s => (s.Quantity * s.UnitCommission));
                    break;
            }

            return retval;
        }

        public decimal GetCoupons(List<Data.InvoiceItems> RQ4DataItems) {
            decimal retval = 0.00m;

            List<int> saleinvoiceids = RQ4DataItems.Select(r => r.SaleInvoiceID).Distinct().ToList();

            if (GOL.coupons.Where(cp => saleinvoiceids.Contains(cp.SaleInvoiceID)).Any())
                retval = GOL.coupons.Where(cp => saleinvoiceids.Contains(cp.SaleInvoiceID)).Sum(s => s.Amount);

            return retval;
        }

        public List<Data.EmployeeItems> GetActiveEmployees(List<Data.BSRitems> BSRData = null) {
            // if BSR data passed in, filter down the global store list to just those active in BSR and the locations where they were in BSR for the pay period
            if (BSRData != null) {
                List<Data.EmployeeItems> filtered_list = new List<Data.EmployeeItems>();

                BSRData.ForEach(b => {
                    Data.EmployeeItems emp = GOL.rq4_sales_consultants.Where(e => e.IdNumber == b.id_field).FirstOrDefault();

                    if (emp != null && !filtered_list.Contains(emp)) {
                        emp.ChannelID = b.channel_id;
                        emp.ChannelName = b.channel_name;
                        emp.RegionID = b.region_id;
                        emp.RegionName = b.region_name;
                        emp.DistrictID = b.district_id;
                        emp.DistrictName = b.district_name;
                        emp.StoreID = b.store_id;
                        emp.StoreName = b.store_name;
                        filtered_list.Add(emp);
                    }
                });

                GOL.rq4_sales_consultants = filtered_list;
            }

            return GOL.rq4_sales_consultants.ToList();
        }

        public List<Data.StoreItems> GetActiveStores(Globals.performance_target_level target_level, List<Data.BSRitems> BSRData = null) {
            // if BSR data passed in, filter down the global store list to just those active in BSR and the locations where they were in BSR for the pay period
            if (BSRData != null) {
                List<Data.StoreItems> filtered_list = new List<Data.StoreItems>();

                BSRData.ForEach(b => {
                    Data.StoreItems st = null;

                    switch (target_level) {
                        case Globals.performance_target_level.Store:
                            st = GOL.rq4_stores.Where(r => r.StoreID == b.store_id).FirstOrDefault();
                            break;
                        case Globals.performance_target_level.District:
                            st = GOL.rq4_stores.Where(r => r.DistrictID == b.district_id).FirstOrDefault();
                            break;
                        case Globals.performance_target_level.Region:
                            st = GOL.rq4_stores.Where(r => r.RegionID == b.region_id).FirstOrDefault();
                            break;
                        case Globals.performance_target_level.Channel:
                            st = GOL.rq4_stores.Where(r => r.ChannelID == b.channel_id).FirstOrDefault();
                            break;
                    }

                    if (st != null && !filtered_list.Contains(st)) {
                        st.ChannelID = b.channel_id;
                        st.ChannelName = b.channel_name;
                        st.RegionID = b.region_id;
                        st.RegionName = b.region_name;
                        st.DistrictID = b.district_id;
                        st.DistrictName = b.district_name;
                        st.StoreID = b.store_id;
                        st.StoreName = b.store_name;
                        // since RQ4 re-assignments could happen at any time-for the correct level, get the leader ID from BSR instead of RQ4 (since that is cached and should be a true representation of the store at the required point in time) along with the correct commission group that leader is in
                        switch (target_level) {
                            case Globals.performance_target_level.Store:
                                st.StoreManagerID = b.info_id;
                                if (GOL.iqmetrix_employees.Any(e => e.Id_Number == st.StoreManagerID))
                                    st.StoreManagerCommissionGroupID = GOL.iqmetrix_employees.First(e => e.Id_Number == st.StoreManagerID).CommissionGroupID;
                                break;
                            case Globals.performance_target_level.District:
                                st.DistrictManagerID = b.info_id;
                                if (GOL.iqmetrix_employees.Any(e => e.Id_Number == st.DistrictManagerID))
                                    st.DistrictManagerCommissionGroupID = GOL.iqmetrix_employees.First(e => e.Id_Number == st.DistrictManagerID).CommissionGroupID;
                                break;
                            case Globals.performance_target_level.Region:
                                st.RegionManagerID = b.info_id;
                                if (GOL.iqmetrix_employees.Any(e => e.Id_Number == st.RegionManagerID))
                                    st.RegionManagerCommissionGroupID = GOL.iqmetrix_employees.First(e => e.Id_Number == st.RegionManagerID).CommissionGroupID;
                                break;
                            case Globals.performance_target_level.Channel:
                                st.ChannelLeaderID = b.info_id;
                                if (GOL.iqmetrix_employees.Any(e => e.Id_Number == st.ChannelLeaderID))
                                    st.AreaManagerCommissionGroupID = GOL.iqmetrix_employees.First(e => e.Id_Number == st.ChannelLeaderID).CommissionGroupID;
                                break;
                        }
                        filtered_list.Add(st);
                    }
                });

                GOL.rq4_stores = filtered_list;
            }

            return GOL.rq4_stores.ToList();
        }

        #endregion

        #region Logic Helpers

        public decimal HandleMinBoxPayout(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, List<Data.BSRitems> BSRData, decimal total_payout, decimal gross_profit, out decimal current_boxes_total, out decimal min_boxes_total) {
            decimal retval = 0.00m;

            bool meets_min = MeetsMinBoxTargetValue(target_level, ppd, entity_id, BSRData, out current_boxes_total, out min_boxes_total);

            if (meets_min) {
                retval = total_payout;

                // clean up any min payment details made before min was hit
                Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Payouts_min);
                if (ctype != null)
                    DAL.DeleteMinPaymentDetail(target_level, ppd, entity_id, ctype.commissions_type_id);
            }
            else
                retval = GetPayoutMinimum(target_level, ppd, entity_id, gross_profit, current_boxes_total, total_payout);

            return retval;
        }

        public bool UpdateTierAssignment(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, List<Data.BSRitems> BSRData) {
            bool retval = false;
            bool foundmatch = false;
            int old_item_id = 0;
            int old_id = 0;
            int new_id = 0;

            // total boxes
            decimal total_boxes = 0;

            if (target_level == Globals.performance_target_level.Employee)
                // only SC's have their tier auto-reallocated based on box performance
                total_boxes = BSRData.Where(b => b.metric_id == (int)Globals.BSR_metrics.box_metric_id).Sum(b => b.total);

            // get current tier asssignment (if any)
            Data.PerformanceTierDataItem pt_current = GOL.performance_tiers_data.Where(pts => pts.performance_target_level == (int)target_level && pts.pay_period_id == ppd.pay_period_id && pts.item_id == entity_id).FirstOrDefault();

            // loop all defined assignments and try to re-allocate based on box values - if no loop exists we'll fall to the end of this proc and handle possible auto-creation 
            GOL.tier_assigments.Where(tas => tas.performance_target_level == (int)target_level && tas.pay_period_id == ppd.pay_period_id).ToList().ForEach(ta => {
                if (ta != null && foundmatch == false) {
                    Data.performance_tiers pt_new = GOL.performance_tiers.Where(p => p.performance_target_level == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.tier_code == ta.tier_code).FirstOrDefault();

                    if (pt_new != null && !foundmatch) {
                        old_item_id = (pt_current != null) ? pt_current.performance_tier_items_id : 0;
                        old_id = (pt_current != null) ? pt_current.performance_tiers_id : 0;

                        switch (ta.@operator) {
                            case "<":
                                if (total_boxes < ta.box_count) {
                                    new_id = pt_new.performance_tiers_id;
                                    foundmatch = true;
                                }
                                break;
                            case "<=":
                                if (total_boxes <= ta.box_count) {
                                    new_id = pt_new.performance_tiers_id;
                                    foundmatch = true;
                                }
                                break;
                            case ">=":
                                if (total_boxes >= ta.box_count) {
                                    new_id = pt_new.performance_tiers_id;
                                    foundmatch = true;
                                }
                                break;
                            case ">":
                                if (total_boxes > ta.box_count) {
                                    new_id = pt_new.performance_tiers_id;
                                    foundmatch = true;
                                }
                                break;
                        }
                    }
                }
            });

            // no matches found, see if we can put this in a default tier
            if (foundmatch == false && pt_current == null && commissions_config != null && commissions_config.default_tier_code != "") {
                foundmatch = true;  // so the auto-create code below will fire, providing we get a valid ID for the default tier
                Data.performance_tiers pt_new = GOL.performance_tiers.Where(p => p.performance_target_level == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.tier_code == commissions_config.default_tier_code).FirstOrDefault();
                new_id = (pt_new != null) ? pt_new.performance_tiers_id : 0;

                if (new_id > 0)
                    AddToNotify("No tier assigned to " + GetLevelDescription(target_level) + " " + GetEntityDescription(BSRData) + " - assigning to default tier level " + commissions_config.default_tier_code);
                else
                    AddToErrors("UpdateTierAssignment error: No Default Tier set for " + GetLevelDescription(target_level) + " " + GetEntityDescription(BSRData) + " for default code " + commissions_config.default_tier_code);
            }

            if (foundmatch == true && new_id > 0 && new_id != old_id) {
                Data.performance_tiers_items pti = new Data.performance_tiers_items();
                pti.performance_tiers_items_id = old_item_id;
                pti.performance_tiers_id = new_id;
                pti.item_id = entity_id;

                retval = DAL.SavePerformanceTierItem(pti);
            }

            return retval;
        }

        public string GetLevelDescription(Globals.performance_target_level target_level) {
            string lev = "unknown";

            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    lev = "Employee";
                    break;
                case Globals.performance_target_level.Store:
                    lev = "Store";
                    break;
                case Globals.performance_target_level.District:
                    lev = "District";
                    break;
                case Globals.performance_target_level.Region:
                    lev = "Region";
                    break;
            }

            return lev;
        }

        public string GetEntityDescription(List<Data.BSRitems> BSRDataItems) {
            return BSRDataItems.Select(b => b.description_field).Any() ? BSRDataItems.Select(b => b.description_field).First() : "unknown";
        }

        public decimal GetMonthlyComissionableGP(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, decimal default_gp) {
            decimal retval = default_gp;

            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    Data.commissions_sales_consultant sc1 = DAL.GetSalesConsultantCommissions(ppd.pay_period_id, entity_id);
                    Data.commissions_sales_consultant sc2 = DAL.GetSalesConsultantCommissions(GOL.ppd_previous.pay_period_id, entity_id);

                    if (GOL.ppd_previous.start_date.Month == ppd.start_date.Month)  // inside current pay period for both halves, so get total from both
                    {
                        // we have saved commissions data for both pay period, add them together
                        if (sc1 != null && sc2 != null) {
                            retval = sc2.commission_gross_profit + sc1.commission_gross_profit;
                        }
                        else
                            // we only have data for the previous pay period, assume the current pay period is new data and use the running default instead
                            if (sc1 == null && sc2 != null)
                                retval = sc2.commission_gross_profit + default_gp;
                    }
                    else
                        retval = (sc1 != null) ? sc1.commission_gross_profit : default_gp;  // just get total for current pay period
                    break;
                case Globals.performance_target_level.Store:
                    Data.commissions_store_leader sl = DAL.GetStoreLeaderCommissions(ppd.pay_period_id, entity_id);
                    retval = (sl != null) ? sl.commission_gross_profit : default_gp;  // just get total for current pay period
                    break;
                case Globals.performance_target_level.District:
                    Data.commissions_district_leader dl = DAL.GetDistrictLeaderCommissions(ppd.pay_period_id, entity_id);
                    retval = (dl != null) ? dl.commission_gross_profit : default_gp;  // just get total for current pay period
                    break;
                case Globals.performance_target_level.Region:
                    Data.commissions_region_leader rl = DAL.GetRegionLeaderCommissions(ppd.pay_period_id, entity_id);
                    retval = (rl != null) ? rl.commission_gross_profit : default_gp;  // just get total for current pay period
                    break;
            }

            return retval;
        }

        public decimal GetMonthlyComissionTotal(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, decimal current_ppd_total) {
            // for reps in the first-half of the PPD, or anyone higher, just use the current commission running total passed in
            if (ppd.start_date.Day == 1 || target_level != Globals.performance_target_level.Employee)
                return current_ppd_total;

            // for reps if we're in the second-half of the pay period, we need to go get the previous pay period total first and then add that to the current running total to return total comp for the whole month
            Data.commissions_sales_consultant sc_prev = DAL.GetSalesConsultantCommissions(GOL.ppd_previous.pay_period_id, entity_id);
            return (sc_prev != null) ? sc_prev.commission_total + current_ppd_total : current_ppd_total;
        }

        public bool AcceleratorsExistForPayPeriod(Data.commissions_pay_periods ppd) {
            return (GOL.commissions_accelerators != null && GOL.commissions_accelerators.Any(ca => ca.pay_period_id == ppd.pay_period_id));
        }

        #endregion

        #region Accelerator Processing

        public decimal AccelerateValue(Data.commissions_accelerator got_accelerator, decimal source_value, List<Data.BSRitems> BSRDataItems, ref decimal percent_to_target, out decimal accelerator_val) {
            decimal retval = 0.00m;
            accelerator_val = 0.00m;
            decimal onehundred_pct = 1.00m;

            if (got_accelerator != null) {
                // got header, try to find a value for it

                // first check if cap % is set and if % target difference is greater default to the cap % value instead
                decimal cap_pct = got_accelerator.cap_percent;
                if (cap_pct > 0 && percent_to_target > cap_pct) {
                    percent_to_target = cap_pct;
                }

                decimal got_pct_to_target = Math.Round(percent_to_target, 4);  // GJK 6/10/2014 - the % start and end are saved as four decimal places, so round the % target to four for better match

                Data.commissions_accelerator_values got_accelerator_val = GOL.commissions_accelerator_values.FirstOrDefault(m => m.commissions_accelerator_id == got_accelerator.commissions_accelerator_id
                                                                                            && (got_pct_to_target >= m.percentage_start && got_pct_to_target <= m.percentage_end));
                if (got_accelerator_val != null && got_accelerator_val.accelerator != 0) {
                    // get the lowest accelerator value for this accelerator - this will tell us if we have a decelerator in play or if it only accelerates
                    Data.commissions_accelerator_values lowest_acc_val = GOL.commissions_accelerator_values.Where(m => m.commissions_accelerator_id == got_accelerator.commissions_accelerator_id).OrderBy(o => o.percentage_start).FirstOrDefault();
                    bool has_decelerator = (lowest_acc_val != null && lowest_acc_val.percentage_start < 1.00m);

                    // get the base % we start accelerating from
                    decimal acc_pct_start = got_accelerator.start_accelerating_percent;

                    // check if a min metric must be met to start accelerating
                    Data.PerformanceTarget pt_min = null;
                    decimal min_target_value = 0.00m;
                    decimal min_target_BSR_value = 0.00m;

                    /*  not used right now
                    int got_min_metric_id = got_accelerator.start_accelerating_min_target_metrics_id.HasValue ? got_accelerator.start_accelerating_min_target_metrics_id.Value : -1;
                    
                    if (got_min_metric_id > 0 && performance_targets != null && BSRDataItems != null)
                    {
                        pt_min = performance_targets.Where(pt => pt.performance_target_metric_id == got_min_metric_id).FirstOrDefault();

                        if (pt_min != null)
                        {
                            // get the min target value
                            min_target_value = pt_min.target_value;
                            min_target_BSR_value = BSRDataItems.Where(b => b.metric_id == pt_min.min_check_metric_ID).Sum(b => b.total);
                        }
                    }
                    */

                    // get the actual accelerator amount for the % range
                    accelerator_val = got_accelerator_val.accelerator;

                    if (has_decelerator) {  // lots of fun logic to handle negative values and the like
                        switch (got_accelerator.metric_source) {
                            case (int)Globals.metric_source_type.performance_target:
                                // work out if their GP should increase or decrease based off their accelerator, by checking if the difference between the accelerator value against % to target is a positive or negative %
                                decimal target_diff = (percent_to_target - acc_pct_start);

                                // if a min value is set, only accelerate if we have met or exceeded it - but always decelerate
                                if (pt_min == null || (pt_min != null && percent_to_target < acc_pct_start) || (pt_min != null && percent_to_target >= acc_pct_start && min_target_BSR_value >= min_target_value)) {
                                    decimal diff_with_accelerator = (target_diff * accelerator_val) + onehundred_pct;

                                    retval = (diff_with_accelerator * source_value);
                                }
                                break;
                            case (int)Globals.metric_source_type.bsr_metric:
                                retval = (accelerator_val * source_value);
                                break;
                        }
                    }
                    else  // straight multiplier for accelerators only
                        retval = (accelerator_val * source_value);
                }
            }

            return retval;
        }

        public decimal ProcessAccelerators(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, Globals.accelerator_source_values source_values, int commission_group_id, bool whatif_only = false, List<Data.CommissionValueDetails> results = null) {
            decimal retval = 0.00M;
            bool has_updates = false;

            // this type has to be defined otherwise we can't create a detail record
            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Accelerators);

            if (ctype == null) {
                AddToErrors("ProcessAccelerators cannot proceed because Commissions Payout Accelerator type " + Globals.commission_types.Accelerators + " is not defined in commissions_type table");
                return retval;
            }

            if (source_values == null) {
                AddToErrors("ProcessAccelerators cannot proceed because Source Values have not been defined");
                return retval;
            }

            // load the tier for this level, so the accelerators can be obtained - if one doesn't exist then no accelerator can be applied
            Data.PerformanceTierDataItem item_performance_tier = GetPerformanceTier(target_level, entity_id);

            // if we've got an accelerator, a target and a tier we're good to go
            if (GOL.commissions_accelerators != null && GOL.performance_targets != null) {
                int tier_id = (item_performance_tier != null) ? item_performance_tier.performance_tiers_id : -1;
                List<Data.commissions_accelerator> got_accelerators = new List<Data.commissions_accelerator>();

                // first try to get any accelerators for both the tier and the comp group, if they are valid
                got_accelerators = GOL.commissions_accelerators.Where(m => (tier_id > 0 && m.performance_tier_id == tier_id) && (commission_group_id > 0 && m.rq4_commission_group_id == commission_group_id)).ToList();

                // if nothing found try to get any accelerators for either the tier and the comp group, if they are valid
                if (got_accelerators.Count == 0)
                    got_accelerators = GOL.commissions_accelerators.Where(m => (tier_id > 0 && m.performance_tier_id == tier_id) || (commission_group_id > 0 && m.rq4_commission_group_id == commission_group_id)).ToList();

                // all else fails, just try to find any default accelerators with the default comp group group ID, ignoring the tiers
                if (got_accelerators.Count == 0)
                    got_accelerators = GOL.commissions_accelerators.Where(m => m.rq4_commission_group_id == -1).ToList();

                foreach (Data.commissions_accelerator got_accelerator in got_accelerators) {
                    try {
                        decimal work_val = 0.00m;
                        decimal metric_val = 0.00m;
                        decimal pct_target = 0.00m;
                        decimal target_val = 0.00m;
                        int metric_id = -1;

                        Data.commissions_accelerator_source_types source_type = GOL.commissions_accelerator_source_types.Where(cst => cst.commissions_accelerator_source_types_id == got_accelerator.source_value_type_id).FirstOrDefault();
                        if (source_type == null) {
                            AddToErrors("ProcessAccelerators issue: No Source Type defined for Accelerator ID " + got_accelerator.commissions_accelerator_id.ToString());
                            continue;
                        }

                        List<Data.BSRitems> BSRData = (source_type.use_monthly_bsr_data) ? source_values.BSRDataItemsMonthly : source_values.BSRDataItems;
                        if (BSRData == null)
                            continue;

                        int target_metric_id = -1;

                        switch (got_accelerator.metric_source) {
                            case (int)Globals.metric_source_type.performance_target:
                                // get a list of targets for the selected accelerator
                                Data.PerformanceTarget selected_target = null;
                                int performance_target_id = got_accelerator.metric_id;

                                // get target that will be calculated
                                switch (target_level) {
                                    case Globals.performance_target_level.Employee:
                                        selected_target = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.employee_id == entity_id && p.performance_target_metric_id == performance_target_id).FirstOrDefault();
                                        break;
                                    case Globals.performance_target_level.Store:
                                        selected_target = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.store_id == entity_id && p.performance_target_metric_id == performance_target_id).FirstOrDefault();
                                        break;
                                    case Globals.performance_target_level.District:
                                        selected_target = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.district_id == entity_id && p.performance_target_metric_id == performance_target_id).FirstOrDefault();
                                        break;
                                    case Globals.performance_target_level.Region:
                                        selected_target = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.region_id == entity_id && p.performance_target_metric_id == performance_target_id).FirstOrDefault();
                                        break;
                                    case Globals.performance_target_level.Channel:
                                        selected_target = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.channel_id == entity_id && p.performance_target_metric_id == performance_target_id).FirstOrDefault();
                                        break;
                                }

                                // validate we have a target
                                if (selected_target == null) {
                                    AddToNotify("ProcessAccelerators issue: No Target set for " + GetLevelDescription(target_level) + " " + GetEntityDescription(BSRData));
                                    continue;
                                }

                                // grab the value for the metric - if this is a min check target we need to get the metric is checking against
                                if (selected_target.used_for_min_check == true && selected_target.min_check_metric_ID.HasValue)
                                    target_metric_id = selected_target.min_check_metric_ID.Value;
                                else
                                    target_metric_id = selected_target.metric_ID;

                                metric_id = selected_target.metric_ID;
                                metric_val = BSRData.Where(b => b.metric_id == target_metric_id).Sum(b => b.total);

                                // grab the set target
                                target_val = selected_target.target_value;

                                // only proceed if the target is greater than zero and not the auto-created default
                                if (target_val > 0 && target_val != Globals.defaults.default_target_val)
                                    // calculate % to that target but hold as decimal for further calcs
                                    pct_target = (metric_val / target_val);

                                break;

                            case (int)Globals.metric_source_type.bsr_metric:
                                metric_id = got_accelerator.metric_id;
                                metric_val = BSRData.Where(b => b.metric_id == metric_id).Sum(b => b.total);
                                pct_target = metric_val;
                                target_val = metric_val;
                                target_metric_id = metric_id;
                                break;

                            case (int)Globals.metric_source_type.custom_data:
                                metric_id = got_accelerator.metric_id;
                                Data.sp_get_commissions_custom_dataResult got_custom_data = GOL.custom_commissions_data.FirstOrDefault(ccd => ccd.custom_data_type_id == metric_id && ccd.performance_target_level == (int)target_level && ccd.rq_id == entity_id);
                                metric_val = (got_custom_data != null) ? got_custom_data.value : 0.00m;
                                pct_target = metric_val;
                                target_val = metric_val;
                                target_metric_id = metric_id;
                                // GJK 4/19/2017 - if a different metric ID is needed for the display label in My Commissions, set that here after we've used the ID to get the custom data values
                                if (got_accelerator.display_metric_id > 0)
                                    metric_id = got_accelerator.display_metric_id;
                                break;

                            default:
                                continue;
                        }

                        // now try to find an accelerator for the % to target for the selected tier
                        decimal source_val = 0.00m;

                        switch (got_accelerator.source_value_type_id) {
                            case (int)Globals.accelerator_source_types.gross_profit:
                                source_val = source_values.gross_profit;
                                break;
                            case (int)Globals.accelerator_source_types.commissions_total_month:
                                source_val = source_values.commission_total_month;
                                break;
                            case (int)Globals.accelerator_source_types.commissions_total_payperiod:
                                source_val = source_values.commission_total_payperiod;
                                break;
                        }

                        decimal value_to_accelerate = source_val;
                        decimal accelerator_val = 0.00m;
                        decimal accelerator_amount = AccelerateValue(got_accelerator, value_to_accelerate, BSRData, ref pct_target, out accelerator_val);

                        // store the diff between base total and their amount under/over from the accelerator
                        if (accelerator_amount != 0) {
                            work_val = (value_to_accelerate += (accelerator_amount - value_to_accelerate));

                            // factor the total diff amounts into their original total, adjusting it up/down accordingly - if there was nothing to do we still return the adjusted total so it can used by other modules
                            retval += work_val;
                        }

                        // log the results
                        Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                        cvd.pay_period_id = ppd.pay_period_id;
                        cvd.entity_id = entity_id;
                        cvd.commissions_type_id = ctype.commissions_type_id;
                        cvd.performance_target_id = got_accelerator.commissions_accelerator_id;
                        cvd.target_value = target_val;
                        cvd.metric_id = metric_id;
                        cvd.metric_value = metric_val;
                        cvd.min_metric_id = target_metric_id;
                        cvd.min_metric_value = source_val;
                        cvd.percent_to_target = pct_target;
                        cvd.accelerator_value = accelerator_val;
                        cvd.performance_total = work_val;

                        if (whatif_only == false) {
                            DAL.SavePerformanceCommissionsDetails(target_level, cvd);
                            has_updates = true;
                        }
                        else {
                            if (results != null)
                                results.Add(cvd);
                        }
                    }

                    catch (Exception e) {
                        string innermsg = e.InnerException != null ? ", inner exception: " + e.InnerException.Message : "";
                        AddToErrors("Unexpected error encountered in ProcessAccelerators for Target Level " + (int)target_level + ", Entity ID " + entity_id.ToString() + " : " + e.Message + innermsg);
                    }
                }
            }

            if (whatif_only == false && has_updates)
                DAL.Commit();

            // the return value with either be the adjusted GP, or zero if nothing to do
            return retval;
        }

        #endregion

        #region Spiff Payout Processing

        public decimal ProcessPayoutsSKU(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, List<Data.BSRitems> BSRDataItems, List<Data.InvoiceItems> invoices, Globals.payout_sku_type payouts_type, bool goes_to_GP, int rq4_commissions_group_id) {
            decimal retval = 0.0m;

            // this type has to be defined otherwise we can't create a detail record
            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Payouts_SKU);

            if (ctype == null)
                AddToErrors("ProcessPayoutsSKU cannot proceed because Commissions Payout SKU type " + Globals.commission_types.Payouts_SKU + " is not defined in commissions_type table");
            else {
                decimal current_boxes_total = 0.00m;
                decimal min_boxes_total = 0.00m;

                // filter invoices to just the selected ID
                List<Data.InvoiceItems> selected_invoices = new List<Data.InvoiceItems>();

                // get list of SKUs from all defined in current date range
                List<Data.commissions_payouts_sku> skus = new List<Data.commissions_payouts_sku>();

                // list of matching invoices to SKUs
                List<Data.Payout> items = new List<Data.Payout>();

                // GJK 11/5/2015 - sku payouts can now be linked to a RQ4 commissions group, so if the current user commission group is in the payouts category we just process those otherwise default to categories not linked to any group
                List<Data.commissions_payouts_sku> payout_skus_to_process = GOL.payouts_sku_list.Where(pcl => pcl.performance_target_level == (int)target_level).ToList();

                if (rq4_commissions_group_id > 0 && payout_skus_to_process.Any(psl => psl.rq4_commission_group_id.HasValue && psl.rq4_commission_group_id.Value == rq4_commissions_group_id))
                    payout_skus_to_process = payout_skus_to_process.Where(psl => psl.rq4_commission_group_id.HasValue && psl.rq4_commission_group_id.Value == rq4_commissions_group_id).ToList();
                else
                    payout_skus_to_process = payout_skus_to_process.Where(psl => !psl.rq4_commission_group_id.HasValue).ToList();

                // GJK 9/7/2017 - axosoft task 1249: Add extra clean-up spiff code to comp engine - spiffs are being tied to comp groups and comp groups keep changing mid-pay period, so we need to make sure any old spiffs are removed that don't belong
                List<int> type_ids = new List<int> { ctype.commissions_type_id };
                List<int> payout_ids = payout_skus_to_process.Select(pc => pc.commissions_payouts_sku_id).ToList();
                DAL.DeleteExistingPayoutsNotInThisList(target_level, ppd, type_ids, entity_id, null, payout_ids, null);

                switch (payouts_type) {
                    case Globals.payout_sku_type.all_types:
                        skus = payout_skus_to_process.Where(cs => cs.performance_target_level == (int)target_level && cs.goes_to_GP == goes_to_GP).ToList();
                        break;
                    case Globals.payout_sku_type.ignore_min_checks:
                        skus = payout_skus_to_process.Where(cs => cs.performance_target_level == (int)target_level && cs.ignore_min_checks == true && cs.goes_to_GP == goes_to_GP).ToList();
                        break;
                    case Globals.payout_sku_type.obey_min_checks:
                        skus = payout_skus_to_process.Where(cs => cs.performance_target_level == (int)target_level && cs.ignore_min_checks == false && cs.goes_to_GP == goes_to_GP).ToList();
                        break;
                    default:
                        skus.Clear();
                        break;
                }

                // load all invoices going back to the start of the return period window - this is so we can check for returns on or after a promotion start and only include those returns on sales that happened inside the returns window
                if (GOL.RQ4Data_payout_sku_invoices_for_returns == null || GOL.RQ4Data_payout_sku_invoices_for_returns.Count == 0) {
                    DateTime start_of_return_period = DateTime.Now.Date.AddDays(-GOL.returns_cutoffday);
                    GOL.RQ4Data_payout_sku_invoices_for_returns = DAL.GetRQ4InvoicesForDate(start_of_return_period, DateTime.Now);
                }

                switch (target_level) {
                    // a qty of -1 is a refund, dont include a payout for these
                    case Globals.performance_target_level.Employee:
                        selected_invoices = invoices.Where(inv => inv.EmployeeID == entity_id && inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date).ToList();

                        // get all applicable SKUs for this target level and type - either the SKUs that will be overriden by the min check by the callee, or the SKUs that return a value that will always be saved
                        skus.ForEach(sku_payout => {
                            // first remove any returns for this SKU where the original sale occured outside the return window - so we don't ding people a negative amount!
                            selected_invoices.Where(inv => inv.Sku == sku_payout.sku && inv.Quantity == -1 && inv.OriginalSaleInvoiceID.HasValue).ToList().ForEach(return_inv => {
                                if (!GOL.RQ4Data_payout_sku_invoices_for_returns.Any(inv => inv.DateCreated >= sku_payout.start_date && inv.SaleInvoiceID == return_inv.OriginalSaleInvoiceID && inv.Sku == return_inv.Sku && inv.Quantity > 0))
                                    selected_invoices.Remove(return_inv);
                            });

                            // now load all matching remaining invoices to the spiff date range and SKU
                            items.AddRange(selected_invoices.Where(inv => (inv.DateCreated >= sku_payout.start_date && inv.DateCreated <= sku_payout.end_date) && inv.Sku == sku_payout.sku)  // filter by date range, as a SKU might have a start/end date range 
                                .Select(invoice => new Data.Payout {
                                    payout_id = sku_payout.commissions_payouts_sku_id,
                                    target_level = sku_payout.performance_target_level,
                                    quantity = invoice.Quantity,
                                    value = sku_payout.amount,
                                    entity_id = invoice.EmployeeID,
                                    sku = invoice.Sku,
                                    goes_to_GP = sku_payout.goes_to_GP,
                                    ignore_min_checks = sku_payout.ignore_min_checks,
                                    item_gp = ((invoice.UnitPrice - invoice.UnitCost) * invoice.Quantity),
                                    value_is_pct_of_item_gp = sku_payout.amount_is_percent_of_item_GP
                                }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                        });

                        break;
                    case Globals.performance_target_level.Store:
                        selected_invoices = invoices.Where(inv => inv.StoreID == entity_id && inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date).ToList();

                        // get all applicable SKUs for this target level and type - either the SKUs that will be overriden by the min check by the callee, or the SKus that return a value that will always be saved
                        skus.ForEach(sku_payout => {
                            // first remove any returns for this SKU where the original sale occured outside the return window - so we don't ding people a negative amount!
                            selected_invoices.Where(inv => inv.Sku == sku_payout.sku && inv.Quantity == -1 && inv.OriginalSaleInvoiceID.HasValue).ToList().ForEach(return_inv => {
                                if (!GOL.RQ4Data_payout_sku_invoices_for_returns.Any(inv => inv.DateCreated >= sku_payout.start_date && inv.SaleInvoiceID == return_inv.OriginalSaleInvoiceID && inv.Sku == return_inv.Sku && inv.Quantity > 0))
                                    selected_invoices.Remove(return_inv);
                            });

                            // now load all matching remaining invoices to the spiff date range and SKU
                            items.AddRange(selected_invoices.Where(inv => (inv.DateCreated >= sku_payout.start_date && inv.DateCreated <= sku_payout.end_date) && inv.Sku == sku_payout.sku)  // filter by date range, as a SKU might have a start/end date range 
                                .Select(invoice => new Data.Payout {
                                    payout_id = sku_payout.commissions_payouts_sku_id,
                                    target_level = sku_payout.performance_target_level,
                                    quantity = invoice.Quantity,
                                    value = sku_payout.amount,
                                    entity_id = invoice.StoreID,
                                    sku = invoice.Sku,
                                    goes_to_GP = sku_payout.goes_to_GP,
                                    ignore_min_checks = sku_payout.ignore_min_checks,
                                    value_is_pct_of_item_gp = sku_payout.amount_is_percent_of_item_GP
                                }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                        });

                        break;
                    case Globals.performance_target_level.District:
                        selected_invoices = invoices.Where(inv => inv.DistrictID == entity_id && inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date).ToList();

                        // get all applicable SKUs for this target level and type - either the SKUs that will be overriden by the min check by the callee, or the SKus that return a value that will always be saved
                        skus.ForEach(sku_payout => {
                            // first remove any returns for this SKU where the original sale occured outside the return window - so we don't ding people a negative amount!
                            selected_invoices.Where(inv => inv.Sku == sku_payout.sku && inv.Quantity == -1 && inv.OriginalSaleInvoiceID.HasValue).ToList().ForEach(return_inv => {
                                if (!GOL.RQ4Data_payout_sku_invoices_for_returns.Any(inv => inv.DateCreated >= sku_payout.start_date && inv.SaleInvoiceID == return_inv.OriginalSaleInvoiceID && inv.Sku == return_inv.Sku && inv.Quantity > 0))
                                    selected_invoices.Remove(return_inv);
                            });

                            // now load all matching remaining invoices to the spiff date range and SKU
                            items.AddRange(selected_invoices.Where(inv => (inv.DateCreated >= sku_payout.start_date && inv.DateCreated <= sku_payout.end_date) && inv.Sku == sku_payout.sku)  // filter by date range, as a SKU might have a start/end date range 
                                .Select(invoice => new Data.Payout {
                                    payout_id = sku_payout.commissions_payouts_sku_id,
                                    target_level = sku_payout.performance_target_level,
                                    quantity = invoice.Quantity,
                                    value = sku_payout.amount,
                                    entity_id = invoice.DistrictID,
                                    sku = invoice.Sku,
                                    goes_to_GP = sku_payout.goes_to_GP,
                                    ignore_min_checks = sku_payout.ignore_min_checks,
                                    value_is_pct_of_item_gp = sku_payout.amount_is_percent_of_item_GP
                                }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                        });

                        break;
                    case Globals.performance_target_level.Region:
                        selected_invoices = invoices.Where(inv => inv.RegionID == entity_id && inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date).ToList();

                        // get all applicable SKUs for this target level and type - either the SKUs that will be overriden by the min check by the callee, or the SKus that return a value that will always be saved
                        skus.ForEach(sku_payout => {
                            // first remove any returns for this SKU where the original sale occured outside the return window - so we don't ding people a negative amount!
                            selected_invoices.Where(inv => inv.Sku == sku_payout.sku && inv.Quantity == -1 && inv.OriginalSaleInvoiceID.HasValue).ToList().ForEach(return_inv => {
                                if (!GOL.RQ4Data_payout_sku_invoices_for_returns.Any(inv => inv.DateCreated >= sku_payout.start_date && inv.SaleInvoiceID == return_inv.OriginalSaleInvoiceID && inv.Sku == return_inv.Sku && inv.Quantity > 0))
                                    selected_invoices.Remove(return_inv);
                            });

                            // now load all matching remaining invoices to the spiff date range and SKU
                            items.AddRange(selected_invoices.Where(inv => (inv.DateCreated >= sku_payout.start_date && inv.DateCreated <= sku_payout.end_date) && inv.Sku == sku_payout.sku)  // filter by date range, as a SKU might have a start/end date range 
                                .Select(invoice => new Data.Payout {
                                    payout_id = sku_payout.commissions_payouts_sku_id,
                                    target_level = sku_payout.performance_target_level,
                                    quantity = invoice.Quantity,
                                    value = sku_payout.amount,
                                    entity_id = invoice.RegionID,
                                    sku = invoice.Sku,
                                    goes_to_GP = sku_payout.goes_to_GP,
                                    ignore_min_checks = sku_payout.ignore_min_checks,
                                    value_is_pct_of_item_gp = sku_payout.amount_is_percent_of_item_GP
                                }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                        });

                        break;

                    case Globals.performance_target_level.Channel:
                        selected_invoices = invoices.Where(inv => inv.ChannelID == entity_id && inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date).ToList();

                        // get all applicable SKUs for this target level and type - either the SKUs that will be overriden by the min check by the callee, or the SKus that return a value that will always be saved
                        skus.ForEach(sku_payout => {
                            // first remove any returns for this SKU where the original sale occured outside the return window - so we don't ding people a negative amount!
                            selected_invoices.Where(inv => inv.Sku == sku_payout.sku && inv.Quantity == -1 && inv.OriginalSaleInvoiceID.HasValue).ToList().ForEach(return_inv => {
                                if (!GOL.RQ4Data_payout_sku_invoices_for_returns.Any(inv => inv.DateCreated >= sku_payout.start_date && inv.SaleInvoiceID == return_inv.OriginalSaleInvoiceID && inv.Sku == return_inv.Sku && inv.Quantity > 0))
                                    selected_invoices.Remove(return_inv);
                            });

                            // now load all matching remaining invoices to the spiff date range and SKU
                            items.AddRange(selected_invoices.Where(inv => (inv.DateCreated >= sku_payout.start_date && inv.DateCreated <= sku_payout.end_date) && inv.Sku == sku_payout.sku)  // filter by date range, as a SKU might have a start/end date range 
                                .Select(invoice => new Data.Payout {
                                    payout_id = sku_payout.commissions_payouts_sku_id,
                                    target_level = sku_payout.performance_target_level,
                                    quantity = invoice.Quantity,
                                    value = sku_payout.amount,
                                    entity_id = invoice.RegionID,
                                    sku = invoice.Sku,
                                    goes_to_GP = sku_payout.goes_to_GP,
                                    ignore_min_checks = sku_payout.ignore_min_checks,
                                    value_is_pct_of_item_gp = sku_payout.amount_is_percent_of_item_GP
                                }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                        });

                        break;
                    default:
                        selected_invoices.Clear();
                        break;
                }

                if (items != null && items.Count() > 0) {
                    // log the results by SKU
                    var item_info = items.Where(itm => itm.entity_id == entity_id).GroupBy(i => i.sku).Select(item => new {
                        payout_id = item.Max(i => i.payout_id),
                        sku = item.Key,
                        ignore_min_check = item.Max(i => i.ignore_min_checks),
                        count = item.Sum(i => i.quantity),  // sum up the quantity to net out any returns (-1)
                        amount = item.Max(i => i.value),
                        goes_to_GP = item.Max(i => i.goes_to_GP),
                        item_gp = item.Sum(i => i.item_gp),
                        amount_is_pct_of_item_gp = item.Max(i => i.value_is_pct_of_item_gp)
                    }).ToList();

                    item_info.ForEach(item => {
                        try {
                            // calculate total using net quantity
                            decimal payout_amount = item.amount;
                            decimal item_total = (item.amount * item.count);

                            // if the payout is a % of the item line GP, the payout_amount variable will contain the % we should pay on - so we swap that out to be the actual GP $ and calculate the total to using the original value
                            if (item.amount_is_pct_of_item_gp) {
                                payout_amount = item.item_gp;
                                item_total = (item.amount * item.item_gp);
                            }
                            else
                                item_total = (payout_amount * item.count);

                            Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                            cvd.pay_period_id = ppd.pay_period_id;
                            cvd.entity_id = entity_id;
                            cvd.commissions_type_id = ctype.commissions_type_id;
                            cvd.payout_id = item.payout_id;
                            cvd.payout_sku = item.sku;
                            cvd.payout_count = item.count;
                            cvd.payout_amount = payout_amount;

                            if (item.ignore_min_check == true || (MeetsMinBoxTargetValue(target_level, ppd, entity_id, BSRDataItems, out current_boxes_total, out min_boxes_total))) {
                                cvd.payout_total = item_total;  // only save the total for this payout if it was actually paid out
                                retval += item_total;
                            }

                            DAL.SaveSkuPayoutCommissionsDetails(target_level, cvd);
                            DAL.Commit();
                        }

                        catch (Exception e) {
                            string innermsg = e.InnerException != null ? ", inner exception: " + e.InnerException.Message : "";
                            AddToErrors("Unexpected error encountered in ProcessPayoutsSKU for Target Level " + (int)target_level + ", Entity ID " + entity_id.ToString() + ", SKU " + item.sku + " : " + e.Message + innermsg);
                        }
                    });
                }
            }

            return retval;
        }

        public decimal ProcessPayoutsCategory(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, List<Data.BSRitems> BSRDataItems, List<Data.InvoiceItems> invoices, bool goes_to_GP, int rq4_commissions_group_id) {
            decimal retval = 0.0m;

            // this type has to be defined otherwise we can't create a detail record
            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Payouts_cat);

            if (ctype == null) {
                AddToErrors("ProcessPayoutsCategory cannot proceed because Commissions Payout Category type " + Globals.commission_types.Payouts_cat + " is not defined in commissions_type table");
                return retval;
            }

            // filter invoices to just the selected ID
            List<Data.InvoiceItems> selected_invoices = new List<Data.InvoiceItems>();

            // also category payouts can be controlled by a min value, so we have to get a list of targets for the selected level, pay period and ID too
            List<Data.PerformanceTarget> selected_targets = new List<Data.PerformanceTarget>();

            // list of matching invoices to SKUs
            List<Data.Payout> items = new List<Data.Payout>();

            // GJK 5/11/2015 - added sub-category level support, so all skus that contain the category ID qualify as being in the sub-category. Also when saving the category info, for sub-categories we use the parent category ID only so the link back to the payouts category table record will match

            // GJK 9/29/2015 - category payouts can now be linked to a RQ4 commissions group, so if the current user commission group is in the payouts category we just process those otherwise default to categories not linked to any group
            List<Data.commissions_payouts_category> payout_categories_to_process = GOL.payouts_category_list.Where(pcl => pcl.performance_target_level == (int)target_level).ToList();

            if (rq4_commissions_group_id > 0 && payout_categories_to_process.Any(pcl => pcl.rq4_commission_group_id.HasValue && pcl.rq4_commission_group_id.Value == rq4_commissions_group_id))
                payout_categories_to_process = payout_categories_to_process.Where(pcl => pcl.rq4_commission_group_id.HasValue && pcl.rq4_commission_group_id.Value == rq4_commissions_group_id).ToList();
            else
                payout_categories_to_process = payout_categories_to_process.Where(pcl => !pcl.rq4_commission_group_id.HasValue).ToList();

            // GJK 9/7/2017 - axosoft task 1249: Add extra clean-up spiff code to comp engine - spiffs are being tied to comp groups and comp groups keep changing mid-pay period, so we need to make sure any old spiffs are removed that don't belong
            List<int> type_ids = new List<int> { ctype.commissions_type_id };
            List<int> payout_ids = payout_categories_to_process.Select(pc => pc.commissions_payouts_category_id).ToList();
            DAL.DeleteExistingPayoutsNotInThisList(target_level, ppd, type_ids, entity_id, null, payout_ids, null);

            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    selected_invoices = invoices.Where(inv => inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date && inv.EmployeeID == entity_id).ToList();
                    selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.employee_id == entity_id && p.for_employee_targets == true).ToList();

                    payout_categories_to_process.Where(pcp => pcp.performance_target_level == (int)target_level && pcp.goes_to_GP == goes_to_GP).ToList().ForEach(payout => {
                        items.AddRange(selected_invoices.Where(inv => ((payout.include_sub_categories == false && inv.CategoryNumber == payout.category_id) || (payout.include_sub_categories == true && inv.CategoryNumber.StartsWith(payout.category_id))))
                            .Select(invoice => new Data.Payout {
                                payout_id = payout.commissions_payouts_category_id,
                                target_level = payout.performance_target_level,
                                quantity = invoice.Quantity,
                                value = payout.amount,
                                entity_id = invoice.EmployeeID,
                                category_id = (payout.include_sub_categories) ? payout.category_id : invoice.CategoryNumber,  // if this is a sub-category, don't record the sub-category ID - use the parent ID instead as that's the only record we have in the payout_category table
                                goes_to_GP = payout.goes_to_GP,
                                min_performance_target_metric_id = (payout.min_performance_target_metric_id.HasValue) ? payout.min_performance_target_metric_id.Value : 0,
                                metric_id_for_range_values = (payout.metric_id_for_range_values.HasValue) ? payout.metric_id_for_range_values.Value : 0,
                                item_gp = ((invoice.UnitPrice - invoice.UnitCost) * invoice.Quantity),
                                value_is_pct_of_item_gp = payout.amount_is_percent_of_item_GP
                            }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                    });

                    break;
                case Globals.performance_target_level.Store:
                    selected_invoices = invoices.Where(inv => inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date && inv.StoreID == entity_id).ToList();
                    selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.store_id == entity_id && p.for_employee_targets == false).ToList();

                    payout_categories_to_process.Where(pcp => pcp.performance_target_level == (int)target_level && pcp.goes_to_GP == goes_to_GP).ToList().ForEach(payout => {
                        items.AddRange(selected_invoices.Where(inv => ((payout.include_sub_categories == false && inv.CategoryNumber == payout.category_id) || (payout.include_sub_categories == true && inv.CategoryNumber.StartsWith(payout.category_id))))
                            .Select(invoice => new Data.Payout {
                                payout_id = payout.commissions_payouts_category_id,
                                target_level = payout.performance_target_level,
                                quantity = invoice.Quantity,
                                value = payout.amount,
                                entity_id = invoice.StoreID,
                                category_id = (payout.include_sub_categories) ? payout.category_id : invoice.CategoryNumber,  // if this is a sub-category, don't record the sub-category ID - use the parent ID instead as that's the only record we have in the payout_category table
                                goes_to_GP = payout.goes_to_GP,
                                min_performance_target_metric_id = (payout.min_performance_target_metric_id.HasValue) ? payout.min_performance_target_metric_id.Value : 0,
                                metric_id_for_range_values = (payout.metric_id_for_range_values.HasValue) ? payout.metric_id_for_range_values.Value : 0,
                                item_gp = ((invoice.UnitPrice - invoice.UnitCost) * invoice.Quantity),
                                value_is_pct_of_item_gp = payout.amount_is_percent_of_item_GP
                            }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                    });

                    break;
                case Globals.performance_target_level.District:
                    selected_invoices = invoices.Where(inv => inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date && inv.DistrictID == entity_id).ToList();
                    selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.district_id == entity_id && p.for_employee_targets == false).ToList();

                    payout_categories_to_process.Where(pcp => pcp.performance_target_level == (int)target_level && pcp.goes_to_GP == goes_to_GP).ToList().ForEach(payout => {
                        items.AddRange(selected_invoices.Where(inv => ((payout.include_sub_categories == false && inv.CategoryNumber == payout.category_id) || (payout.include_sub_categories == true && inv.CategoryNumber.StartsWith(payout.category_id))))
                            .Select(invoice => new Data.Payout {
                                payout_id = payout.commissions_payouts_category_id,
                                target_level = payout.performance_target_level,
                                quantity = invoice.Quantity,
                                value = payout.amount,
                                entity_id = invoice.DistrictID,
                                category_id = (payout.include_sub_categories) ? payout.category_id : invoice.CategoryNumber,  // if this is a sub-category, don't record the sub-category ID - use the parent ID instead as that's the only record we have in the payout_category table
                                goes_to_GP = payout.goes_to_GP,
                                min_performance_target_metric_id = (payout.min_performance_target_metric_id.HasValue) ? payout.min_performance_target_metric_id.Value : 0,
                                metric_id_for_range_values = (payout.metric_id_for_range_values.HasValue) ? payout.metric_id_for_range_values.Value : 0,
                                item_gp = ((invoice.UnitPrice - invoice.UnitCost) * invoice.Quantity),
                                value_is_pct_of_item_gp = payout.amount_is_percent_of_item_GP
                            }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                    });

                    break;
                case Globals.performance_target_level.Region:
                    selected_invoices = invoices.Where(inv => inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date && inv.RegionID == entity_id).ToList();
                    selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.region_id == entity_id && p.for_employee_targets == false).ToList();

                    payout_categories_to_process.Where(pcp => pcp.performance_target_level == (int)target_level && pcp.goes_to_GP == goes_to_GP).ToList().ForEach(payout => {
                        items.AddRange(selected_invoices.Where(inv => ((payout.include_sub_categories == false && inv.CategoryNumber == payout.category_id) || (payout.include_sub_categories == true && inv.CategoryNumber.StartsWith(payout.category_id))))
                            .Select(invoice => new Data.Payout {
                                payout_id = payout.commissions_payouts_category_id,
                                target_level = payout.performance_target_level,
                                quantity = invoice.Quantity,
                                value = payout.amount,
                                entity_id = invoice.RegionID,
                                category_id = (payout.include_sub_categories) ? payout.category_id : invoice.CategoryNumber,  // if this is a sub-category, don't record the sub-category ID - use the parent ID instead as that's the only record we have in the payout_category table
                                goes_to_GP = payout.goes_to_GP,
                                min_performance_target_metric_id = (payout.min_performance_target_metric_id.HasValue) ? payout.min_performance_target_metric_id.Value : 0,
                                metric_id_for_range_values = (payout.metric_id_for_range_values.HasValue) ? payout.metric_id_for_range_values.Value : 0,
                                item_gp = ((invoice.UnitPrice - invoice.UnitCost) * invoice.Quantity),
                                value_is_pct_of_item_gp = payout.amount_is_percent_of_item_GP
                            }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                    });

                    break;
                case Globals.performance_target_level.Channel:
                    selected_invoices = invoices.Where(inv => inv.DateCreated >= ppd.start_date && inv.DateCreated <= ppd.end_date && inv.ChannelID == entity_id).ToList();
                    selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.region_id == entity_id && p.for_employee_targets == false).ToList();

                    payout_categories_to_process.Where(pcp => pcp.performance_target_level == (int)target_level && pcp.goes_to_GP == goes_to_GP).ToList().ForEach(payout => {
                        items.AddRange(selected_invoices.Where(inv => ((payout.include_sub_categories == false && inv.CategoryNumber == payout.category_id) || (payout.include_sub_categories == true && inv.CategoryNumber.StartsWith(payout.category_id))))
                            .Select(invoice => new Data.Payout {
                                payout_id = payout.commissions_payouts_category_id,
                                target_level = payout.performance_target_level,
                                quantity = invoice.Quantity,
                                value = payout.amount,
                                entity_id = invoice.RegionID,
                                category_id = (payout.include_sub_categories) ? payout.category_id : invoice.CategoryNumber,  // if this is a sub-category, don't record the sub-category ID - use the parent ID instead as that's the only record we have in the payout_category table
                                goes_to_GP = payout.goes_to_GP,
                                min_performance_target_metric_id = (payout.min_performance_target_metric_id.HasValue) ? payout.min_performance_target_metric_id.Value : 0,
                                metric_id_for_range_values = (payout.metric_id_for_range_values.HasValue) ? payout.metric_id_for_range_values.Value : 0,
                                item_gp = ((invoice.UnitPrice - invoice.UnitCost) * invoice.Quantity),
                                value_is_pct_of_item_gp = payout.amount_is_percent_of_item_GP
                            }).ToList());  // Normally if a SKU has been sold multiple times it gets its own line with a qty of 1, but some SC's have put it on a single line with multiple quantity instead
                    });

                    break;
                default:
                    selected_invoices.Clear();
                    selected_targets.Clear();
                    break;
            }

            if (items != null && items.Count() > 0) {
                // log the results by category - if we are dealing with sub-categories, then the code above will have only recorded the parent category ID in the data so this grouping should still produce a single row
                var item_info = items.Where(q => q.entity_id == entity_id).GroupBy(i => i.category_id).Select(item => new {
                    payout_id = item.Max(i => i.payout_id),
                    category_id = item.Key,
                    count = item.Sum(i => i.quantity),  // sum up the quantity to net out any returns (-1)
                    amount = item.Max(i => i.value),
                    goes_to_GP = item.Max(i => i.goes_to_GP),
                    min_performance_target_metric_id = item.Max(i => i.min_performance_target_metric_id),
                    metric_id_for_range_values = item.Max(i => i.metric_id_for_range_values),
                    item_gp = item.Sum(i => i.item_gp),
                    amount_is_pct_of_item_gp = item.Max(i => i.value_is_pct_of_item_gp)
                }).ToList();

                // loop the category results and check against targets if required - for sub-categories we want to take the total for everything under the parent category in a single row so that we don't create seperate payout entries and we only match to one performance target value
                item_info.ForEach(item => {
                    try {
                        // this spiff could be linked to a min value from the performance targets, or a range value using the BSR metric value
                        Data.PerformanceTarget min_performance_target = null;
                        Data.commissions_payouts_category_values payout_cat_value = null;
                        decimal min_target_value = 0.00m;
                        decimal min_target_BSR_value = 0.00m;
                        int min_target_bsr_metric_id = 0;

                        decimal payout_amount = item.amount;  // default to amount defined in payout category

                        if (item.min_performance_target_metric_id > 0) {
                            min_performance_target = selected_targets.Where(pt => pt.performance_target_metric_id == item.min_performance_target_metric_id && pt.used_for_min_check == true).FirstOrDefault();

                            // got a min value to meet?
                            if (min_performance_target != null) {
                                // get the min target value
                                min_target_value = min_performance_target.target_value;

                                // get the metric ID for BSR
                                min_target_bsr_metric_id = (min_performance_target.min_check_metric_ID.HasValue) ? min_performance_target.min_check_metric_ID.Value : 0;

                                // now get the total value from BSR that we need to compare against
                                min_target_BSR_value = BSRDataItems.Where(b => b.metric_id == min_target_bsr_metric_id).Sum(b => b.total);
                            }
                        }
                        else
                            // could be linked to a payout range instead
                            if (item.metric_id_for_range_values > 0) {
                                // get the total value from BSR that we need to compare against
                                min_target_BSR_value = BSRDataItems.Where(b => b.metric_id == item.metric_id_for_range_values).Sum(b => b.total);

                                // if we got a payout value in range of this metric value, use that to calculate the item total
                                payout_cat_value = GOL.payouts_category_values_list.Where(pcv => pcv.commissions_payouts_category_id == item.payout_id && (min_target_BSR_value >= pcv.start_value && min_target_BSR_value <= pcv.end_value)).FirstOrDefault();

                                if (payout_cat_value != null) {
                                    min_target_value = payout_cat_value.start_value;
                                    payout_amount = payout_cat_value.payout_value;
                                }
                                else
                                    payout_amount = 0.00m;  // no range match found, reset to zero so nothing is paid out
                            }

                        // calculate total using net quantity
                        decimal item_total = 0.00m;

                        // if the payout is a % of the item line GP, the payout_amount variable will contain the % we should pay on - so we swap that out to be the actual GP $ and calculate the total to using the original value
                        if (item.amount_is_pct_of_item_gp) {
                            payout_amount = item.item_gp;
                            item_total = (item.amount * item.item_gp);
                        }
                        else
                            item_total = (payout_amount * item.count);

                        Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();

                        cvd.pay_period_id = ppd.pay_period_id;
                        cvd.entity_id = entity_id;
                        cvd.commissions_type_id = ctype.commissions_type_id;
                        cvd.target_value = min_target_value;
                        cvd.min_metric_id = min_target_bsr_metric_id;
                        cvd.min_metric_value = min_target_BSR_value;
                        cvd.payout_id = item.payout_id;
                        cvd.payout_category = item.category_id;
                        cvd.payout_count = item.count;
                        cvd.payout_amount = payout_amount;

                        // only save payout totals if: this goes to GP (so we can see what the GP would increase by), if a target isn't set, or it is and we've met it, or the metric was linked to a range of values and we matched one
                        if (goes_to_GP || min_performance_target == null || payout_cat_value != null || min_target_BSR_value >= min_target_value) {
                            cvd.payout_total = item_total;  // only save the total for this payout if it was actually paid out
                            retval += item_total;
                        }

                        DAL.SaveCategoryPayoutCommissionsDetails(target_level, cvd);
                        DAL.Commit();
                    }

                    catch (Exception e) {
                        string innermsg = e.InnerException != null ? ", inner exception: " + e.InnerException.Message : "";
                        AddToErrors("Unexpected error encountered in ProcessPayoutsCategory for " + GetLevelDescription(target_level) + " " + GetEntityDescription(BSRDataItems) + ", Category ID " + item.category_id + " : " + e.Message + innermsg);
                    }
                });
            }

            return retval;
        }

        public decimal GetPayoutMinimum(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, decimal gross_profit, decimal current_boxes_total, decimal total_payout_no_min) {
            decimal retval = total_payout_no_min;

            // this type has to be defined otherwise we can't create a detail record
            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Payouts_min);

            if (ctype == null) {
                AddToErrors("GetPayoutMinimum cannot proceed because Commissions Payout Min type " + Globals.commission_types.Payouts_min + " is not defined in commissions_type table");
            }
            else {
                // we have to have a category defined with code MIN<level> for this level to save the min against in the detail row
                Data.commissions_payouts_category pc = null;

                switch (target_level) {
                    case Globals.performance_target_level.Employee:
                        pc = GetCategoryByID(Globals.payout_special_categories.MinimumPayoutSC);
                        break;
                    case Globals.performance_target_level.Store:
                        pc = GetCategoryByID(Globals.payout_special_categories.MinimumPayoutSL);
                        break;
                    case Globals.performance_target_level.District:
                        pc = GetCategoryByID(Globals.payout_special_categories.MinimumPayoutDL);
                        break;
                    case Globals.performance_target_level.Region:
                        pc = GetCategoryByID(Globals.payout_special_categories.MinimumPayoutRL);
                        break;
                }

                if (pc != null) {
                    Data.commissions_payouts_minimum cpm = GOL.payouts_minimum_list.Where(pm => pm.performance_target_level == (int)target_level).FirstOrDefault();

                    if (cpm != null && current_boxes_total >= cpm.minimum_boxes)  // the min boxes check, can be ignored by setting the box min to zero so everyone gets paid out
                    {
                        if (cpm.percentage_of_gross_profit == true) {
                            retval = (gross_profit * cpm.payout_value);
                        }
                        else {
                            retval = cpm.payout_value;
                        }

                        Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();

                        cvd.pay_period_id = ppd.pay_period_id;
                        cvd.entity_id = entity_id;
                        cvd.commissions_type_id = ctype.commissions_type_id;
                        cvd.target_value = 0;
                        cvd.min_metric_id = 0;
                        cvd.min_metric_value = 0;
                        cvd.payout_id = cpm.commissions_payouts_minimum_id;
                        cvd.payout_category = pc.category_id;
                        cvd.payout_count = 1;
                        cvd.payout_amount = cpm.payout_value;
                        cvd.payout_total = retval;

                        DAL.SaveCategoryPayoutCommissionsDetails(target_level, cvd);
                    }
                }
            }

            return retval;
        }

        #endregion

        #region Target hit list processing

        public bool LoadHitTargetsList(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, List<Data.BSRitems> BSRData) {
            bool retval = false;

            // if we've got some BSR data, an accelerator, a target and a tier we're good to go
            if (GOL.performance_targets != null) {
                // get a list of targets for the selected level, pay period and ID
                List<Data.PerformanceTarget> selected_targets = new List<Data.PerformanceTarget>();

                // load a list of all targets that will be calculated, exluding any defined min values
                switch (target_level) {
                    case Globals.performance_target_level.Employee:
                        selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.used_for_min_check == false && p.for_employee_targets == true).ToList();
                        break;
                    case Globals.performance_target_level.Store:
                        selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.used_for_min_check == false && p.for_employee_targets == false).ToList();
                        break;
                    case Globals.performance_target_level.District:
                        selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.used_for_min_check == false && p.for_employee_targets == false).ToList();
                        break;
                    case Globals.performance_target_level.Region:
                        selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == ppd.pay_period_id && p.used_for_min_check == false && p.for_employee_targets == false).ToList();
                        break;
                    default:
                        selected_targets.Clear();
                        break;
                }

                // each target will contain a metric we have to evalute - should just be boxes for districts and regions
                selected_targets.ForEach(st => {
                    int entity_id = -1;

                    try {
                        DateTime open_date = DateTime.Now;
                        bool store_closed = false;

                        switch (target_level) {
                            case Globals.performance_target_level.Employee:
                                entity_id = st.employee_id;
                                break;
                            case Globals.performance_target_level.Store:
                                entity_id = st.store_id;
                                Data.StoreItems got_store = GetActiveStores(target_level).Where(store => store.StoreID == entity_id).FirstOrDefault();
                                open_date = got_store != null && got_store.OpenDate.HasValue ? open_date = got_store.OpenDate.Value : DateTime.Now;
                                store_closed = (got_store != null && got_store.CloseDate.HasValue);
                                break;
                            case Globals.performance_target_level.District:
                                entity_id = st.district_id;
                                break;
                            case Globals.performance_target_level.Region:
                                entity_id = st.region_id;
                                break;
                        }

                        List<Data.BSRitems> BSRDataItems = (BSRData != null) ? BSRData.Where(b => b.id_field == entity_id).ToList() : null;

                        if (BSRDataItems != null && BSRDataItems.Count > 0) {
                            // grab the value for the metric
                            decimal metric_val = BSRDataItems.Where(b => b.metric_id == st.metric_ID).Sum(b => b.total);

                            // grab the set target
                            decimal target_val = st.target_value;
                            decimal pct_target = 0.00m;

                            // only proceed if the target is greater than zero and is not the auto-created default
                            if (target_val > 0 && target_val != Globals.defaults.default_target_val) {
                                // calculate % to that target but hold as decimal for further calcs
                                pct_target = (metric_val / target_val);

                                // if we're processing at store level, and the store is open and has been open for 3 months or longer and hit or went above target, log it 
                                if ((target_level == Globals.performance_target_level.Store && !store_closed && Globals.MonthDifference(ppd.start_date, open_date) >= 3 && pct_target >= 1.00M) &&
                                     (!GOL.hit_target_stores.Any(hts => hts.StoreID == entity_id)))  // only log once!
                                {
                                    Data.HitTargetStore store_item = new Data.HitTargetStore();
                                    store_item.RegionID = st.region_id;
                                    store_item.DistrictID = st.district_id;
                                    store_item.StoreID = entity_id;
                                    store_item.PerformanceTargetMetricID = st.performance_target_metric_id;
                                    GOL.hit_target_stores.Add(store_item);
                                }

                                // if we're at the district level and the district hit target, log it
                                if ((target_level == Globals.performance_target_level.District && pct_target >= 1.00M) && (!GOL.hit_target_districts.Any(hts => hts.DistrictID == entity_id)))  // only log once!
                                {
                                    Data.HitTargetStore store_item = new Data.HitTargetStore();
                                    store_item.RegionID = st.region_id;
                                    store_item.DistrictID = entity_id;
                                    store_item.StoreID = -1;
                                    store_item.PerformanceTargetMetricID = st.performance_target_metric_id;
                                    GOL.hit_target_districts.Add(store_item);
                                }
                            }
                        }
                    }

                    catch (Exception e) {
                        string innermsg = e.InnerException != null ? ", inner exception: " + e.InnerException.Message : "";
                        AddToErrors("Unexpected error encountered in LoadHitTargetsList for Target Level " + (int)target_level + ", Entity ID " + entity_id.ToString() + ", performance target ID " + st.performance_target_id.ToString() + " : " + e.Message + innermsg);
                    }
                });

                retval = true;
            }

            return retval;
        }

        #endregion

        #region Participation Index processing

        public decimal GetParticipationAmount(Globals.performance_target_level target_level, int target_metrics_id, int location_count, int quota_count, out int participation_id) {
            // Participation amount is based off data matrix as per September haymaker document for district leaders - total stores for district, total stores for district that have met quota, gives a reward amount
            // e.g. if you have four stores, and two met quota you get the amount 
            decimal retval = 0.00m;
            participation_id = 0;

            if (GOL.commissions_participations != null) {
                // try to find a matching participation index entry for the target metric, the location count and met quota count
                Data.commissions_participation cp = GOL.commissions_participations.Where(p => p.performance_target_level == (int)target_level && p.performance_target_metrics_id == target_metrics_id && p.location_count == location_count && p.met_quota_count == quota_count).FirstOrDefault();

                if (cp != null) {
                    participation_id = cp.commissions_participation_id;

                    // handle auto-calculation of participation value - if value is zero then auto-calculate (quota * location count) but limit to min of 0.30 and max of 1
                    if (cp.value == 0) {
                        decimal val = ((decimal)quota_count / (decimal)location_count);  // have to cast the ints as decimal too so floating-points will be preserved!
                        retval = (val < 0.30m || val > 1) ? 0 : val;
                    }
                    else {
                        retval = cp.value;
                    }
                }
            }

            return retval;
        }

        public decimal ProcessParticipationIndex(Globals.performance_target_level target_level, int pay_period_id, int entity_id, decimal base_GP, int location_count) {
            decimal retval = 0.00M;

            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Participations);

            if (ctype == null) {
                AddToErrors("ProcessParticipationIndex cannot proceed because Commissions Payout Participations type " + Globals.commission_types.Participations + " is not defined in commissions_type table");
            }
            else {
                // get a list of targets for the selected level, pay period and ID
                List<Data.PerformanceTarget> selected_targets = new List<Data.PerformanceTarget>();
                List<Data.HitTargetStore> hit_targets = new List<Data.HitTargetStore>();

                switch (target_level) {
                    case Globals.performance_target_level.Employee:
                        selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == pay_period_id && p.employee_id == entity_id && p.for_employee_targets == true && p.used_for_min_check == false).ToList();
                        break;
                    case Globals.performance_target_level.Store:
                        selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == pay_period_id && p.store_id == entity_id && p.for_employee_targets == false && p.used_for_min_check == false).ToList();
                        break;
                    case Globals.performance_target_level.District:
                        selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == pay_period_id && p.district_id == entity_id && p.for_employee_targets == false && p.used_for_min_check == false).ToList();
                        hit_targets = GOL.hit_target_stores.Where(s => s.DistrictID == entity_id).ToList();
                        break;
                    case Globals.performance_target_level.Region:
                        selected_targets = GOL.performance_targets.Where(p => p.target_level_id == (int)target_level && p.pay_period_id == pay_period_id && p.region_id == entity_id && p.for_employee_targets == false && p.used_for_min_check == false).ToList();
                        hit_targets = GOL.hit_target_districts.Where(s => s.RegionID == entity_id).ToList();
                        break;
                    default:
                        selected_targets.Clear();
                        break;
                }

                // each target will contain a metric that could be linked to a participation index, loop all targets and see if we can find a participation index entry
                selected_targets.ForEach(st => {
                    try {
                        if (GOL.commissions_participations.Any(p => p.performance_target_level == (int)target_level && p.performance_target_metrics_id == st.performance_target_metric_id)) {
                            int hit_target_count = hit_targets.Where(s => s.PerformanceTargetMetricID == st.performance_target_metric_id).Count();
                            int participation_id;
                            decimal pi = GetParticipationAmount(target_level, st.performance_target_metric_id, location_count, hit_target_count, out participation_id);

                            if (pi > 0) {
                                retval += (base_GP * pi);
                            }

                            // log the results
                            Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                            cvd.pay_period_id = pay_period_id;
                            cvd.entity_id = entity_id;
                            cvd.commissions_type_id = ctype.commissions_type_id;
                            cvd.commissions_participation_id = participation_id;
                            cvd.participation_location_count = location_count;
                            cvd.participation_target_count = hit_target_count;
                            cvd.participation_index_value = pi;
                            cvd.performance_target_id = st.performance_target_metric_id;
                            DAL.SaveParticipationCommissionsDetails(target_level, cvd);
                        }
                    }

                    catch (Exception e) {
                        string innermsg = e.InnerException != null ? ", inner exception: " + e.InnerException.Message : "";
                        AddToErrors("Unexpected error encountered in ProcessParticipationIndex for Target Level " + (int)target_level + ", Entity ID " + entity_id.ToString() + " : " + e.Message + innermsg);
                    }
                });
            }

            return retval;
        }

        #endregion

        #region KPI processing

        public decimal ProcessKPI(Globals.performance_target_level target_level, int pay_period_id, int entity_id, List<Data.BSRitems> BSRDataItems) {
            decimal retval = 0.00M;

            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.KPI);

            if (ctype == null) {
                AddToErrors("ProcessKPI cannot proceed because type " + Globals.commission_types.KPI + " is not defined in the commissions_type table");
            }
            else {
                int tot_points = 0;

                // if we've got some BSR data, and KPI data we're good to go
                if (GOL.kpi_data != null && GOL.kpi_points != null && GOL.kpi_values != null && BSRDataItems != null) {
                    // get a list of KPIs for the selected level and ID using the combined data which contains the KPI and the item IDs
                    List<Data.sp_get_commissions_KPI_dataResult> kpi_item_data = GOL.kpi_data.Where(kp => kp.performance_target_level == (int)target_level && kp.item_id == entity_id).ToList();

                    // each KPI will contain a metric we have to evalute against, store up the total points for each metric
                    kpi_item_data.ForEach(kpi => {
                        decimal metric_val = BSRDataItems.Where(b => b.metric_id == kpi.metric_id).Sum(s => s.total);

                        Data.commissions_kpi_goal_points got_kpi_point = GOL.kpi_points.Where(kv => kv.commissions_kpi_groups_id == kpi.commissions_kpi_groups_id && (metric_val >= kv.goal_start && metric_val <= kv.goal_end)).FirstOrDefault();

                        if (got_kpi_point != null) {
                            tot_points += got_kpi_point.points;
                        }

                        // log the results
                        Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                        cvd.pay_period_id = pay_period_id;
                        cvd.entity_id = entity_id;
                        cvd.metric_id = kpi.metric_id;
                        cvd.metric_value = metric_val;
                        cvd.kpi_id = kpi.commissions_kpi_groups_id;
                        cvd.kpi_points = (got_kpi_point != null) ? got_kpi_point.points : 0;
                        cvd.commissions_type_id = ctype.commissions_type_id;
                        DAL.SaveKpiCommissionsDetails(target_level, cvd);
                    });

                    // now go find out what the value of the points are
                    Data.commissions_kpi_points_reward_values got_kpi_points_value = GOL.kpi_values.Where(kp => kp.pay_period_id == pay_period_id && kp.performance_target_level == (int)target_level && (tot_points >= kp.points_earned_start && tot_points <= kp.points_earned_end)).FirstOrDefault();
                    if (got_kpi_points_value != null) {
                        retval = got_kpi_points_value.reward_value;
                    }
                }
            }

            return retval;
        }

        #endregion

        #region MSO file processing

        public void ProcessMSOExtract(DateTime for_date) {
            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Payouts_MSO);

            if (ctype == null) {
                AddToErrors("ProcessMSOExtract cannot proceed because type " + Globals.commission_types.Payouts_MSO + " is not defined in the commissions_type table");
                return;
            }

            DateTime payout_double_triple_start = new DateTime(2014, 10, 01);
            DateTime payout_double_triple_end = new DateTime(2015, 01, 31, 23, 59, 59);
            bool payout_double_triple = (for_date.Date >= payout_double_triple_start && for_date <= payout_double_triple_end);

            // only go back over the past six months worth of invoices for the serial number search
            int months_back = -6;
            DateTime end_date = DateTime.Now;
            DateTime start_date = end_date.AddMonths(months_back);
            List<Data.InvoiceItems> RQ4DataInvoices = null;
            Data.commissions_pay_periods ppd = null;

            // process rep MSO payouts if one if defined for this pay period
            Data.commissions_payouts_category payout_category = GetCategoryByID(Globals.payout_special_categories.MSOPayoutSC);
            if (payout_category != null) {
                // only get invoice lines that contain an MSO install category
                RQ4DataInvoices = DAL.GetRQ4InvoicesForDate(start_date, end_date).Where(inv => mso_RQ4_categories.Contains(inv.CategoryNumber)).ToList();

                // process employees            
                ppd = DAL.GetPayPeriod(for_date, true);
                GOL.LoadGlobalLists(for_date, ppd);
                DAL.GetEmployeeData().ForEach(e => {
                    int emp_id = e.IdNumber;

                    // check for existing header to hang the MSO spiff payout line off - if it doesn't exist don't continue, allow commissions processing to create the header first. This is because this method doesn't have all the data needed to create the header correctly...
                    Data.commissions_sales_consultant sc_dat = DAL.GetSalesConsultantCommissions(ppd.pay_period_id, emp_id);

                    if (sc_dat != null) {
                        List<Data.InvoiceItems> rq4_employee_data = RQ4DataInvoices.Where(i => i.EmployeeID == emp_id).ToList();

                        // get the MSO payout amount and counts
                        if (payout_double_triple) {
                            ProcessPayoutFromMSOExtractDoubleTriple(ppd, rq4_employee_data, emp_id, Globals.spiff_level.Consultant, ctype, payout_category);
                        }
                        else {
                            ProcessPayoutFromMSOExtract(ppd, rq4_employee_data, emp_id, Globals.spiff_level.Consultant, ctype, payout_category);
                        }
                    }
                });
            }

            // process store MSO payouts if one is defined in this pay period
            payout_category = GetCategoryByID(Globals.payout_special_categories.MSOPayoutSL);
            if (payout_category != null) {
                // process store leaders
                ppd = DAL.GetPayPeriod(for_date, false);

                GOL.LoadGlobalLists(for_date, ppd);

                if (RQ4DataInvoices == null)
                    RQ4DataInvoices = DAL.GetRQ4InvoicesForDate(start_date, end_date).Where(inv => mso_RQ4_categories.Contains(inv.CategoryNumber)).ToList();

                GetActiveStores(Globals.performance_target_level.Store).ForEach(s => {
                    int region_id = s.RegionID;
                    int district_id = s.DistrictID;
                    int store_id = s.StoreID;

                    // check for existing header - if it doesn't exist don't continue, allow commissions processing to create the header first
                    Data.commissions_store_leader sl_dat = DAL.GetStoreLeaderCommissions(ppd.pay_period_id, store_id);

                    if (sl_dat != null) {
                        List<Data.InvoiceItems> rq4_store_data = RQ4DataInvoices.Where(i => i.StoreID == store_id).ToList();

                        // get in the MSO payout amount and counts
                        if (payout_double_triple) {
                            ProcessPayoutFromMSOExtractDoubleTriple(ppd, rq4_store_data, store_id, Globals.spiff_level.Store, ctype, payout_category);
                        }
                        else {
                            ProcessPayoutFromMSOExtract(ppd, rq4_store_data, store_id, Globals.spiff_level.Store, ctype, payout_category);
                        }
                    }
                });
            }
        }

        // double/triple payout rules
        public Boolean ProcessPayoutFromMSOExtractDoubleTriple(Data.commissions_pay_periods ppd, List<Data.InvoiceItems> RQ4Data, int entityID, Globals.spiff_level spiff_level, Data.commissions_type ctype, Data.commissions_payouts_category payout_category) {
            Boolean retval = false;

            if (RQ4Data != null) {
                try {
                    // we go back over a maximum of six months worth of MSO data, in case completed installs come in staggered across multiple pay periods
                    if (GOL.CommissionsExtractData == null) {
                        int months_back = -6;
                        GOL.CommissionsExtractData = DAL.GetMSOExtractData(ppd.start_date.AddMonths(months_back), ppd.end_date, true);  // go six months back, as the installs could be completed over multiple pay periods
                    }

                    // build list of all values we'll be matching on in the MSO data - order conf. number and MDN [phone number]
                    var commissions_serial_data = GOL.CommissionsExtractData.Where(ex => RQ4Data.Select(rq => rq.SerialNumber).ToList().Contains(ex.MDN)).GroupBy(g => g.MDN).Select(c => c.Key).ToList().Union(
                                                  GOL.CommissionsExtractData.Where(ex => RQ4Data.Select(rq => rq.SerialNumber).ToList().Contains(ex.MSO_ORDER_CONF_NUMBER)).GroupBy(g => g.MSO_ORDER_CONF_NUMBER).Select(c => c.Key).ToList());

                    if (commissions_serial_data != null) {
                        commissions_serial_data.ToList().ForEach(ced => {
                            // only proceed if no existing payout has been made for this install phone number or order conf. number
                            if (!HasExistingMSOpayout(spiff_level, entityID, ced)) {
                                // get the number of completed installs for this phone number or order conf number
                                int mso_count = GOL.CommissionsExtractData.Where(cd => cd.MDN == ced || cd.MSO_ORDER_CONF_NUMBER == ced).Count();

                                // match the phone number to an invoice and get all items for that number on that invoice
                                List<Data.InvoiceItems> inv_dat = null;

                                if (spiff_level == Globals.spiff_level.Consultant) {
                                    inv_dat = RQ4Data.Where(i => i.EmployeeID == entityID && i.SerialNumber == ced).ToList();  // restrict invoices to just the specified SC and just mso installs, so the payout amount will be for just this person
                                }
                                else
                                    if (spiff_level == Globals.spiff_level.Store || spiff_level == Globals.spiff_level.MultiStore) {
                                        inv_dat = RQ4Data.Where(i => i.StoreID == entityID && i.SerialNumber == ced).ToList();  // restrict invoices to just the specified store and just mso installs, so the payout amount will be for just SL
                                    }

                                if (inv_dat != null && inv_dat.Count > 0) {
                                    // break the list down by invoice number, and process each one. This is because there could be multiple invoices with the same serial number....
                                    inv_dat.GroupBy(gp => gp.InvoiceIDByStore).Select(s => s.First()).ToList().ForEach(inv_list => {
                                        decimal val = 0.00m;

                                        // get total SKU's on the invoice for this MSO install, exluding refunds
                                        var inv_mso_skus = inv_dat.Where(i => i.InvoiceIDByStore == inv_list.InvoiceIDByStore && i.Quantity > 0 && ((double_play_skus.Contains(i.Sku)) || (triple_play_skus.Contains(i.Sku)))).GroupBy(i => new { i.SerialNumber, i.Sku }).ToList();

                                        // determine payout type by number of skus for this serial number, and make sure that all the skus are valid for that type
                                        int match_skus = 0;

                                        inv_mso_skus.ForEach(inv => {
                                            string got_sku = inv.Key.Sku;
                                            match_skus = inv_mso_skus.Count(im => im.Key.SerialNumber == inv.Key.SerialNumber);

                                            // if all skus are valid and we have a matching number of completed installs, then proceed with payout
                                            if (mso_count == match_skus) {
                                                switch (spiff_level) {
                                                    case Globals.spiff_level.Consultant: {
                                                            switch (match_skus) {
                                                                case 2:  // double play
                                                                    {
                                                                        val = 75.00m;
                                                                        break;
                                                                    }
                                                                case 3:  // triple play
                                                                    {
                                                                        val = 100.00m;
                                                                        break;
                                                                    }
                                                            }

                                                            // save the results
                                                            Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                                                            cvd.pay_period_id = ppd.pay_period_id;
                                                            cvd.entity_id = entityID;
                                                            cvd.commissions_type_id = ctype.commissions_type_id;
                                                            cvd.target_value = 0;
                                                            cvd.min_metric_id = 0;
                                                            cvd.min_metric_value = 0;
                                                            cvd.payout_id = payout_category.commissions_payouts_category_id;
                                                            cvd.payout_category = payout_category.category_id;
                                                            cvd.payout_count = match_skus;
                                                            cvd.payout_amount = val;
                                                            cvd.payout_total = val;
                                                            cvd.mso_serial_number = ced;

                                                            // the main commissions loop will pick this value up and apply it to the commissions total
                                                            DAL.SaveMSOPayoutCommissionsDetails(Globals.performance_target_level.Employee, cvd);
                                                            DAL.Commit();

                                                            retval = true;
                                                            break;
                                                        }
                                                    case Globals.spiff_level.Store:
                                                    case Globals.spiff_level.MultiStore: {
                                                            switch (match_skus) {
                                                                case 2:  // double play
                                                                    {
                                                                        val = 50.00m;
                                                                        break;
                                                                    }
                                                                case 3:  // triple play
                                                                    {
                                                                        val = 75.00m;
                                                                        break;
                                                                    }
                                                            }

                                                            // save the results
                                                            Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                                                            cvd.pay_period_id = ppd.pay_period_id;
                                                            cvd.entity_id = entityID;
                                                            cvd.commissions_type_id = ctype.commissions_type_id;
                                                            cvd.target_value = 0;
                                                            cvd.min_metric_id = 0;
                                                            cvd.min_metric_value = 0;
                                                            cvd.payout_id = payout_category.commissions_payouts_category_id;
                                                            cvd.payout_category = payout_category.category_id;
                                                            cvd.payout_count = match_skus;
                                                            cvd.payout_amount = val;
                                                            cvd.payout_total = val;
                                                            cvd.mso_serial_number = ced;

                                                            // the main commissions loop will pick this value up and apply it to the commissions total
                                                            DAL.SaveMSOPayoutCommissionsDetails(Globals.performance_target_level.Store, cvd);
                                                            DAL.Commit();

                                                            retval = true;
                                                            break;
                                                        }
                                                }
                                            }
                                        });
                                    });
                                }
                            }
                        });
                    }
                }
                catch (Exception e) {
                    AddToErrors("Unexpected error in ProcessPayoutFromMSOExtract - error is " + e.Message + e.InnerException != null ? ", inner exception is " + e.InnerException.Message : "");
                }
            }

            return retval;
        }

        // payout flat amount
        public void ProcessPayoutFromMSOExtract(Data.commissions_pay_periods ppd, List<Data.InvoiceItems> RQ4Data, int entityID, Globals.spiff_level spiff_level, Data.commissions_type ctype, Data.commissions_payouts_category payout_category) {
            try {
                // load-on-demand list of serial numbers from the commissions extract file - all that is needed to cross-ref against invoices to see how many items were sold
                // get all completed installs this pay period
                if (GOL.CommissionsExtractData == null) {
                    int months_back = -6;
                    GOL.CommissionsExtractData = DAL.GetMSOExtractData(ppd.start_date.AddMonths(months_back), ppd.end_date, true);  // go six months back, as the installs could be completed over multiple pay periods
                }
            }
            catch (Exception e) {
                AddToErrors("Unexpected error in GetPayoutFromMSOExtract - error is " + e.Message + e.InnerException != null ? ", inner exception is " + e.InnerException.Message : "");
            }

            // build list of all values we'll be matching on in the MSO data - order conf. number and MDN [phone number]
            var commissions_serial_data = GOL.CommissionsExtractData.Where(ex => RQ4Data.Select(rq => rq.SerialNumber).ToList().Contains(ex.MDN)).GroupBy(g => g.MDN).Select(c => c.Key).ToList().Union(
                                            GOL.CommissionsExtractData.Where(ex => RQ4Data.Select(rq => rq.SerialNumber).ToList().Contains(ex.MSO_ORDER_CONF_NUMBER)).GroupBy(g => g.MSO_ORDER_CONF_NUMBER).Select(c => c.Key).ToList());

            // only process MSO data that is in the current invoice list         
            if (commissions_serial_data != null) {
                int tot = 0;

                commissions_serial_data.ToList().ForEach(ced => {
                    decimal base_amount = 0.00m;

                    if (!HasExistingMSOpayout(spiff_level, entityID, ced)) {
                        // don't pay out for refunds, so sum up the total quantity will count all -1's against positive quantity sales
                        if (spiff_level == Globals.spiff_level.Consultant) {
                            tot = RQ4Data.Where(i => i.EmployeeID == entityID && i.SerialNumber == ced).Sum(i => i.Quantity);  // restrict invoices to just the specified SC and just mso installs, so the payout amount will be for just this person
                        }
                        else
                            if (spiff_level == Globals.spiff_level.Store || spiff_level == Globals.spiff_level.MultiStore) {
                                tot = RQ4Data.Where(i => i.StoreID == entityID && i.SerialNumber == ced).Sum(i => i.Quantity);  // restrict invoices to just the specified store and just mso installs, so the payout amount will be for just SL
                            }

                        if (tot > 0) {
                            if (spiff_level == Globals.spiff_level.Consultant) {
                                base_amount = 30.00m;

                                // save the results
                                Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                                cvd.pay_period_id = ppd.pay_period_id;
                                cvd.entity_id = entityID;
                                cvd.commissions_type_id = ctype.commissions_type_id;
                                cvd.target_value = 0;
                                cvd.min_metric_id = 0;
                                cvd.min_metric_value = 0;
                                cvd.payout_id = payout_category.commissions_payouts_category_id;
                                cvd.payout_category = payout_category.category_id;
                                cvd.payout_count = tot;
                                cvd.payout_amount = base_amount;
                                cvd.payout_total = base_amount * tot;
                                cvd.mso_serial_number = ced;

                                // the main commissions loop will pick this value up and apply it to the commissions total
                                DAL.SaveMSOPayoutCommissionsDetails(Globals.performance_target_level.Employee, cvd);
                                DAL.Commit();
                            }
                            else
                                if (spiff_level == Globals.spiff_level.Store || spiff_level == Globals.spiff_level.MultiStore) {
                                    base_amount = 20.00m;

                                    // save the results
                                    Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                                    cvd.pay_period_id = ppd.pay_period_id;
                                    cvd.entity_id = entityID;
                                    cvd.commissions_type_id = ctype.commissions_type_id;
                                    cvd.target_value = 0;
                                    cvd.min_metric_id = 0;
                                    cvd.min_metric_value = 0;
                                    cvd.payout_id = payout_category.commissions_payouts_category_id;
                                    cvd.payout_category = payout_category.category_id;
                                    cvd.payout_count = tot;
                                    cvd.payout_amount = base_amount;
                                    cvd.payout_total = base_amount * tot;
                                    cvd.mso_serial_number = ced;

                                    // the main commissions loop will pick this value up and apply it to the commissions total
                                    DAL.SaveMSOPayoutCommissionsDetails(Globals.performance_target_level.Store, cvd);
                                    DAL.Commit();
                                }
                        }
                    }
                });
            }
        }

        public decimal GetExistingMSOAmount(Globals.spiff_level target_level, int pay_period_id, int entity_id) {
            decimal retval = 0.00m;

            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Payouts_MSO);

            if (ctype == null || DAL.db() == null) {
                return retval;
            }

            switch (target_level) {
                case Globals.spiff_level.Consultant: {
                        Data.commissions_sales_consultant sc = DAL.GetSalesConsultantCommissions(pay_period_id, entity_id);
                        if (sc != null && DAL.db().commissions_sales_consultant_detail.Any(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && s.commissions_type_id == ctype.commissions_type_id)) {
                            retval = DAL.db().commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && s.commissions_type_id == ctype.commissions_type_id).Sum(s => s.payout_total);
                        }
                        break;
                    }
                case Globals.spiff_level.Store: {
                        Data.commissions_store_leader sl = DAL.GetStoreLeaderCommissions(pay_period_id, entity_id);
                        if (sl != null && DAL.db().commissions_store_leader_detail.Any(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && s.commissions_type_id == ctype.commissions_type_id)) {
                            retval = DAL.db().commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && s.commissions_type_id == ctype.commissions_type_id).Sum(s => s.payout_total);
                        }
                        break;
                    }
                case Globals.spiff_level.District: {
                        Data.commissions_district_leader dl = DAL.GetDistrictLeaderCommissions(pay_period_id, entity_id);
                        if (dl != null && DAL.db().commissions_district_leader_detail.Any(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == ctype.commissions_type_id)) {
                            retval = DAL.db().commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == ctype.commissions_type_id).Sum(s => s.payout_total);
                        }
                        break;
                    }
                case Globals.spiff_level.Region: {
                        Data.commissions_region_leader rl = DAL.GetRegionLeaderCommissions(pay_period_id, entity_id);
                        if (rl != null && DAL.db().commissions_region_leader_detail.Any(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == ctype.commissions_type_id)) {
                            retval = DAL.db().commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == ctype.commissions_type_id).Sum(s => s.payout_total);
                        }
                        break;
                    }
            }

            return retval;
        }

        public bool HasExistingMSOpayout(Globals.spiff_level spiff_level, int entity_id, string serial_number) {
            bool retval = false;

            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Payouts_MSO);

            if (ctype == null || DAL.db() == null) {
                return retval;
            }

            switch (spiff_level) {
                case Globals.spiff_level.Consultant: {
                        retval = (from sc in DAL.db().commissions_sales_consultant
                                  join scd in DAL.db().commissions_sales_consultant_detail on sc.commissions_sales_consultant_id equals scd.commissions_sales_consultant_id
                                  where sc.employee_id == entity_id && scd.commissions_type_id == ctype.commissions_type_id && scd.mso_serial_number == serial_number
                                  select sc).FirstOrDefault() != null;
                        break;
                    }
                case Globals.spiff_level.Store:
                case Globals.spiff_level.MultiStore: {
                        retval = (from sl in DAL.db().commissions_store_leader
                                  join sld in DAL.db().commissions_store_leader_detail on sl.commissions_store_leader_id equals sld.commissions_store_leader_id
                                  where sl.store_id == entity_id && sld.commissions_type_id == ctype.commissions_type_id && sld.mso_serial_number == serial_number
                                  select sl).FirstOrDefault() != null;
                        break;
                    }
            }

            return retval;
        }

        #endregion

        #region Target Payouts

        #region Dynamic Data Calculation Engine

        private Data.PerformanceTarget GetTargetData(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, Data.commissions_pay_periods ppd_store, int employee_id, int store_or_higher_id, int source_id, int metric_id, Dictionary<int, decimal> existing_vals, List<Data.BSRitems> BSRDataItems) {
            Data.PerformanceTarget pt_res = null;

            switch (source_id) {
                case (int)Globals.metric_source_type.internal_calculated:  // go to local calcs
                    // if we didn't get a valid left performance target metric, there could be a calculated value for it stored in the list already - go get that instead if it exists
                    if (existing_vals.ContainsKey(metric_id)) {
                        pt_res = new Data.PerformanceTarget();
                        pt_res.performance_target_metric_id = metric_id;
                        pt_res.target_value = existing_vals[metric_id];
                    }
                    break;
                case (int)Globals.metric_source_type.performance_target:
                    // first get all targets linked to this performance target metric
                    List<Data.PerformanceTarget> pt_list = GOL.performance_targets.Where(pt => pt.performance_target_metric_id == metric_id).ToList();

                    // if we're at SC level and there is a valid target at this level for the metric, use it - otherwise go to a different level to get it
                    if (target_level == Globals.performance_target_level.Employee && pt_list.Any(pt => pt.target_level_id == (int)target_level && pt.employee_id == employee_id))
                        pt_res = pt_list.Where(pt => pt.target_level_id == (int)target_level && pt.employee_id == employee_id).FirstOrDefault();
                    else {
                        switch (target_level) {
                            case Globals.performance_target_level.Employee:  // no target at rep level, default to store
                                pt_res = pt_list.Where(pt => pt.target_level_id == (int)Globals.performance_target_level.Store && pt.store_id == store_or_higher_id).FirstOrDefault();
                                break;
                            case Globals.performance_target_level.Store:
                                pt_res = pt_list.Where(pt => pt.target_level_id == (int)target_level && pt.store_id == store_or_higher_id).FirstOrDefault();
                                break;
                            case Globals.performance_target_level.District:
                                pt_res = pt_list.Where(pt => pt.target_level_id == (int)target_level && pt.district_id == store_or_higher_id).FirstOrDefault();
                                break;
                            case Globals.performance_target_level.Region:
                                pt_res = pt_list.Where(pt => pt.target_level_id == (int)target_level && pt.region_id == store_or_higher_id).FirstOrDefault();
                                break;
                            case Globals.performance_target_level.Channel:
                                pt_res = pt_list.Where(pt => pt.target_level_id == (int)target_level && pt.channel_id == store_or_higher_id).FirstOrDefault();
                                break;
                        }
                    }
                    break;
                case (int)Globals.metric_source_type.bsr_metric:
                    pt_res = new Data.PerformanceTarget();
                    pt_res.performance_target_metric_id = metric_id;
                    pt_res.target_value = BSRDataItems.Where(b => b.metric_id == metric_id).Sum(b => b.total);
                    break;
                case (int)Globals.metric_source_type.custom_data:
                    Data.sp_get_commissions_custom_dataResult got_custom_data = null;
                    // if we're at SC level and there is a valid data at this level for the metric, use it - otherwise go to a different level to get it
                    if (target_level == Globals.performance_target_level.Employee && GOL.custom_commissions_data.Any(ccd => ccd.custom_data_type_id == metric_id && ccd.performance_target_level == (int)target_level && ccd.rq_id == employee_id))
                        got_custom_data = GOL.custom_commissions_data.FirstOrDefault(ccd => ccd.custom_data_type_id == metric_id && ccd.performance_target_level == (int)target_level && ccd.rq_id == employee_id);
                    else {
                        int default_level = (target_level == Globals.performance_target_level.Employee) ? (int)Globals.performance_target_level.Store : (int)target_level;
                        got_custom_data = GOL.custom_commissions_data.FirstOrDefault(ccd => ccd.custom_data_type_id == metric_id && ccd.performance_target_level == default_level && ccd.rq_id == store_or_higher_id);
                    }
                    pt_res = new Data.PerformanceTarget();
                    pt_res.performance_target_metric_id = metric_id;
                    pt_res.target_value = (got_custom_data != null) ? got_custom_data.value : 0.00m;
                    break;
            }

            return pt_res;
        }

        private decimal CalculateTargetData(Data.PerformanceTarget pt_left, Data.PerformanceTarget pt_right, char op, Data.commissions_target_calculations for_calc, Data.commissions_pay_periods ppd) {
            decimal gotval = 0.00m;

            // only proceed with valid calc values for both sides
            if (pt_left != null && pt_right != null) {
                switch (op) {
                    case '+':
                        gotval = pt_left.target_value + pt_right.target_value;
                        break;
                    case '-':
                        gotval = pt_left.target_value - pt_right.target_value;
                        break;
                    case '*':
                        gotval = pt_left.target_value * pt_right.target_value;
                        break;
                    case '/':
                        gotval = (pt_right.target_value > 0) ? pt_left.target_value / pt_right.target_value : 0;
                        break;
                }

                if (for_calc.per_pay_period == true) {
                    int pay_period_count = DAL.GetPayPeriodCount(ppd.start_date, ppd.for_consultants);
                    if (pay_period_count > 0)
                        gotval = (gotval / pay_period_count);
                }
            }

            return gotval;
        }

        private Dictionary<int, decimal> BuildCalculatedTargetMetrics(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, Data.commissions_pay_periods ppd_store, int employee_id, int store_or_higher_id, List<Data.BSRitems> BSRDataItems, out List<Data.commissions_target_calculations> calc_breakdown) {
            Dictionary<int, decimal> got_vals = new Dictionary<int, decimal>();
            List<Data.commissions_target_calculations> calc_number_breakdown = new List<Data.commissions_target_calculations>();

            GOL.target_calculations.ForEach(got_calc => {
                if (!calc_number_breakdown.Any(cb => cb.commissions_target_calculations_id == got_calc.commissions_target_calculations_id))
                    calc_number_breakdown.Add(got_calc);

                decimal gotval = 0.00m;
                int left_id = got_calc.left_metric_id;
                int right_id = got_calc.right_metric_id;
                char op = got_calc.@operator;

                // these values could be anywhere - linked to an individual SC or a Store ID, so we have to find 'em...
                // this assumes that for SCs: ppd param will be the SC pay period, ppd_store will be the store pay period

                // the way this works is it builds up a dictionary of target metric ID's, and a value against each one - the value could be calculated from a previous target metric too so this allows calculations to be done on-the-fly
                // example:  metric 1 / metric 2 = metric 3
                //           metric 3 / metric 4 = metric 5
                // in the above, the first calculation would use target values for each metric ID, then store the result of new value in a metric ID 3. 
                // because there are no targets set for metric 3, the next calc would pull the target value from the internal list instead and use it to do the rest of the calculation

                Data.PerformanceTarget pt_left = null;
                Data.PerformanceTarget pt_right = null;

                if (got_calc.left_metric_source == 0)  // calculated field based off dynamic value - make sure the parent value has been calculated first
                {
                    Data.commissions_target_calculations ct = GOL.target_calculations.Where(c => c.result_metric_id == left_id && c.group_id == got_calc.group_id).FirstOrDefault();

                    while (ct != null) {
                        if (!got_vals.ContainsKey(ct.result_metric_id)) {
                            pt_left = GetTargetData(target_level, ppd, ppd_store, employee_id, store_or_higher_id, ct.left_metric_source, ct.left_metric_id, got_vals, BSRDataItems);
                            pt_right = GetTargetData(target_level, ppd, ppd_store, employee_id, store_or_higher_id, ct.right_metric_source, ct.right_metric_id, got_vals, BSRDataItems);

                            gotval = CalculateTargetData(pt_left, pt_right, ct.@operator, ct, ppd);

                            got_vals.Add(ct.result_metric_id, gotval);
                        }

                        ct = GOL.target_calculations.Where(c => c.result_metric_id == ct.left_metric_id && c.group_id == ct.group_id).FirstOrDefault();

                        if (ct == null || ct.left_metric_source == 0)
                            break;
                    }
                }

                if (got_calc.right_metric_source == 0)  // calculated field based off dynamic value - make sure the parent value has been calculated first
                {
                    Data.commissions_target_calculations ct = GOL.target_calculations.Where(c => c.result_metric_id == right_id && c.group_id == got_calc.group_id).FirstOrDefault();

                    while (ct != null) {
                        if (!got_vals.ContainsKey(ct.result_metric_id)) {
                            pt_left = GetTargetData(target_level, ppd, ppd_store, employee_id, store_or_higher_id, ct.left_metric_source, ct.left_metric_id, got_vals, BSRDataItems);
                            pt_right = GetTargetData(target_level, ppd, ppd_store, employee_id, store_or_higher_id, ct.right_metric_source, ct.right_metric_id, got_vals, BSRDataItems);

                            gotval = CalculateTargetData(pt_left, pt_right, ct.@operator, ct, ppd);

                            got_vals.Add(ct.result_metric_id, gotval);
                        }

                        ct = GOL.target_calculations.Where(c => c.result_metric_id == ct.right_metric_id && c.group_id == ct.group_id).FirstOrDefault();

                        if (ct == null || ct.right_metric_source == 0)
                            break;
                    }
                }

                if (!got_vals.ContainsKey(got_calc.result_metric_id)) {
                    pt_left = GetTargetData(target_level, ppd, ppd_store, employee_id, store_or_higher_id, got_calc.left_metric_source, left_id, got_vals, BSRDataItems);
                    pt_right = GetTargetData(target_level, ppd, ppd_store, employee_id, store_or_higher_id, got_calc.right_metric_source, right_id, got_vals, BSRDataItems);

                    gotval = CalculateTargetData(pt_left, pt_right, op, got_calc, ppd);

                    switch (got_calc.round_direction) {
                        case -1: gotval = Math.Floor(gotval);
                            break;
                        case 1: gotval = Math.Ceiling(gotval);
                            break;
                    }

                    got_vals.Add(got_calc.result_metric_id, gotval);
                }
            });

            calc_breakdown = calc_number_breakdown;
            return got_vals;
        }

        #endregion

        #region Payout Calculator
        private decimal GetPayoutValue(Data.commissions_target_payout payout_target, decimal pct_to_target) {
            decimal retval = 0.00m;

            if (payout_target != null) {
                // GJK 6/22/2015 - target ranges are four decimals - so make sure the value we're trying to match on is the same four decimal format using the modulus operator [we don't want it rounding, just truncating to four decimals]
                decimal pct_to_target_four = pct_to_target - (pct_to_target % 0.0001M);
                Data.commissions_target_payout_values got_payout_val = GOL.target_payout_values.Where(m => m.commissions_target_payout_id == payout_target.commissions_target_payout_id
                                                                                            && (pct_to_target_four >= m.start_value && pct_to_target_four <= m.end_value)).FirstOrDefault();
                if (got_payout_val != null)
                    retval = got_payout_val.payout_value;
            }

            return retval;
        }
        #endregion

        #region Target Payout main methods
        public Data.TargetPayoutResult ProcessTargetPayouts(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, List<Data.BSRitems> BSRDataItems, decimal comm_gp, decimal comm_gp_monthly, int commission_group_id, Data.commissions_pay_periods ppd_store = null, int store_or_higher_id = -1) {
            Data.TargetPayoutResult res = new Data.TargetPayoutResult();
            res.total = 0.00m;
            res.box_attain_percent = 0.00m;

            decimal retval = 0.00M;

            // this type has to be defined otherwise we can't create a detail record
            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.TargetPayouts);

            if (ctype == null || BSRDataItems == null || GOL.target_payouts == null || GOL.performance_targets == null) {
                if (ctype == null)
                    AddToErrors("ProcessTargetPayouts cannot proceed because Commissions Target Payout type " + Globals.commission_types.TargetPayouts + " is not defined in commissions_type table");
                return res;
            }

            if (!ppd.for_consultants) {
                ppd_store = ppd;
                store_or_higher_id = entity_id;
            }

            // construct a list of metric values, using a dynamic calculation engine - grabs metric ID's and math operators from a table and calculates data on-the-fly storing the results against metric IDs
            List<Data.commissions_target_calculations> calc_number_breakdown = new List<Data.commissions_target_calculations>();
            Dictionary<int, decimal> calculated_target_values = BuildCalculatedTargetMetrics(target_level, ppd, ppd_store, entity_id, store_or_higher_id, BSRDataItems, out calc_number_breakdown);

            // GJK 9/28/2015 - October 2015 comp plan revisions, we now support both a calculated target and static target
            // also the target payouts can now broken down by both RQ4 commission group and an optional target tiers range, which basically means there could be multiple payouts in here now
            // so first we have to filter the list down to just the target payouts we need to process - 
            List<Data.commissions_target_payout> target_payouts_to_process = GOL.target_payouts.Where(tp => tp.pay_period_id == ppd.pay_period_id && tp.performance_target_level == (int)target_level).ToList();

            // check if there are any which match the current user commission group and filter to process only those payouts - the commission group is mutally exclusive so if we have a group ID and it matches
            // at least one payout then we only process everything assigned to that group. Otherwise we only process that target payouts with no commission group ID
            if (commission_group_id > 0 && target_payouts_to_process.Any(tp => tp.rq4_commission_group_id.HasValue && tp.rq4_commission_group_id.Value == commission_group_id))
                // get all targets linked to this commission group only
                target_payouts_to_process = target_payouts_to_process.Where(tp => tp.rq4_commission_group_id.HasValue && tp.rq4_commission_group_id.Value == commission_group_id).ToList();
            else
                // get all target payouts not linked to any commission group - the group ID is null
                target_payouts_to_process = target_payouts_to_process.Where(tp => !tp.rq4_commission_group_id.HasValue).ToList();

            // GJK 11/12/2015: added this to delete any payouts that already exist but not for the target payouts from above - this is so if someone is moved to a different commissions group we don't leave any old data which belonged to the other group
            DAL.DeleteExistingPayoutsNotInThisList(target_level, ppd, new List<int> { ctype.commissions_type_id }, entity_id, target_payouts_to_process.Select(tp => tp.commissions_target_payout_id).ToList(), null, null);

            foreach (Data.commissions_target_payout got_tp in target_payouts_to_process) {
                try {
                    // if this target is linked to a tier that means there could be multiple targets linked to different tiers, so we only process the one that matches the correct tier
                    if (got_tp.commissions_target_tiers_id.HasValue) {
                        Data.commissions_target_tiers got_target_tier = GOL.target_payout_tiers.Where(tpt => tpt.commissions_target_tiers_id == got_tp.commissions_target_tiers_id.Value).FirstOrDefault();
                        if (got_target_tier == null)  // not linked to a valid target tier, skip this target payout
                            continue;

                        // check if the current metric value for this payout tier is with its defined range - if not, this tier doesn't apply so we skip this target payout
                        decimal metric_val = BSRDataItems.Where(b => b.metric_id == got_target_tier.metric_id).Sum(b => b.total);

                        // Now we need to check against metric 169 when it's a performance target metric so that we can base the tiers off that target value 
                        // rather than against the contribution value.
                        if (got_tp.metric_source == (int)Globals.metric_source_type.performance_target) {
                            if (got_target_tier.metric_id == 169) {

                                int performance_target_metric_id = got_tp.metric_id;
                                Data.PerformanceTarget selected_target = null;

                                switch (target_level) {
                                    case Globals.performance_target_level.Employee:
                                    case Globals.performance_target_level.Store:
                                        selected_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)target_level && pt.store_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                        break;
                                }

                                if (selected_target != null) {
                                    // grab the set target
                                    metric_val = selected_target.target_value;

                                }
                            }

                            // This is the now new thing we are using tier codes for NSSMs target payouts.  If this is the case we need to pull the corresponding performance target value for that store
                            // From the monthly pay period.
                            if (got_target_tier.metric_id == 172) {

                                // Get the metric ID
                                using (Data.AWirelessDataContext db = new Data.AWirelessDataContext()) {
                                    // Need to make sure we are always in the month pay period.  As this metric deals strictly with the store target tiers that was uploaded.
                                    // This should return a list with the main month pay period in it.
                                    Data.commissions_pay_periods all_month_pay_period = db.commissions_pay_periods.Where(pp => pp.start_date.Month == ppd.start_date.Month && pp.start_date.Day == 1 && pp.end_date.Day > 16 && pp.start_date.Year == ppd.start_date.Year).FirstOrDefault();
                                    if (all_month_pay_period == null) {
                                        continue;
                                    }

                                    Data.performance_target_metrics target_metric = db.performance_target_metrics.Where(ptm => ptm.metric_ID == 172 && ptm.pay_period_id == all_month_pay_period.pay_period_id).FirstOrDefault();

                                    // If we find the metric then we can proceed.
                                    if (target_metric != null) {
                                        int performance_target_metric_id = target_metric.performance_target_metric_id;

                                        Data.PerformanceTarget selected_target = null;

                                        switch (target_level) {
                                            case Globals.performance_target_level.Employee:
                                            case Globals.performance_target_level.Store:
                                                selected_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)Globals.performance_target_level.Store && pt.store_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                                break;
                                        }

                                        if (selected_target != null) {
                                            // grab the set target
                                            metric_val = selected_target.target_value;
                                        }
                                    }
                                    else {
                                        continue;
                                    }
                                }
                            }
                        }

                        // End of Modification for metric 169 and 172


                        if (!(metric_val >= got_target_tier.start_value && metric_val <= got_target_tier.end_value)) {
                            // not this tier - make sure no previous payout exists for payout tier ID
                            DAL.DeleteExistingPayout(target_level, ppd, entity_id, got_tp.commissions_target_payout_id, -1, -1);
                            continue;
                        }
                    }

                    decimal work_val = 0.00m;
                    decimal got_target_val = 0.00m;
                    decimal pct_target = 0.00m;
                    int min_metric_id = 0;
                    decimal min_metric_value = 0.00m;
                    int cur_metric_id = 0;
                    decimal cur_metric_value = 0.00m;
                    decimal target_val = 0.00m;

                    switch (got_tp.metric_source) {
                        case (int)Globals.metric_source_type.internal_calculated:
                            int res_metric_id = got_tp.metric_id;
                            pct_target = calculated_target_values.ContainsKey(res_metric_id) ? calculated_target_values[res_metric_id] : 0.00m;

                            // get the target and current value for the result calc if we have one
                            Data.commissions_target_calculations got_result_calc = calc_number_breakdown.FirstOrDefault(cb => cb.result_metric_id == res_metric_id);
                            if (got_result_calc != null) {
                                cur_metric_id = res_metric_id;
                                min_metric_id = (got_result_calc.left_metric_bsr_id.HasValue) ? got_result_calc.left_metric_bsr_id.Value : got_result_calc.left_metric_id;
                                Data.PerformanceTarget got_right = GetTargetData(target_level, ppd, ppd_store, entity_id, store_or_higher_id, got_result_calc.right_metric_source, got_result_calc.right_metric_id, calculated_target_values, BSRDataItems);
                                target_val = (got_right != null) ? got_right.target_value : 0.00m;
                                Data.PerformanceTarget got_left = GetTargetData(target_level, ppd, ppd_store, entity_id, store_or_higher_id, got_result_calc.left_metric_source, got_result_calc.left_metric_id, calculated_target_values, BSRDataItems);
                                min_metric_value = (got_left != null) ? got_left.target_value : 0.00m;
                            }

                            // GJK 6/1/2016: if this is box attainment % save it to apply to final payout for East trial
                            if (res_metric_id == 77 || res_metric_id == 78)
                                res.box_attain_percent = pct_target;
                            break;

                        case (int)Globals.metric_source_type.performance_target:
                            // static target - get values from performance targets and compare with BSR to get % to target
                            int performance_target_metric_id = got_tp.metric_id;

                            Data.PerformanceTarget got_target = null;

                            switch (target_level) {
                                case Globals.performance_target_level.Employee:
                                    got_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)target_level && pt.employee_id == entity_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                    break;
                                case Globals.performance_target_level.Store:
                                    got_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)target_level && pt.store_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                    break;
                                case Globals.performance_target_level.District:
                                    got_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)target_level && pt.district_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                    break;
                                case Globals.performance_target_level.Region:
                                    got_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)target_level && pt.region_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                    break;
                                case Globals.performance_target_level.Channel:
                                    got_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)target_level && pt.channel_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                    break;
                            }

                            if (got_target != null) {
                                // grab the value for the metric - if this is a min check target we need to get the metric is checking against
                                int target_BSR_metric_id = -1;
                                if (got_target.used_for_min_check == true && got_target.min_check_metric_ID.HasValue)
                                    target_BSR_metric_id = got_target.min_check_metric_ID.Value;
                                else
                                    target_BSR_metric_id = got_target.metric_ID;

                                decimal metric_val = BSRDataItems.Where(b => b.metric_id == target_BSR_metric_id).Sum(b => b.total);

                                // grab the set target
                                target_val = got_target.target_value;

                                // only proceed if the target is greater than zero and is not the auto-created default
                                if (target_val > 0 && target_val != Globals.defaults.default_target_val) {
                                    // calculate % to target
                                    decimal performance_pct_target = (metric_val / target_val);
                                    // round the % to target to four decimal places for range match (DB schema uses four decimals)
                                    pct_target = performance_pct_target - (performance_pct_target % 0.0001M);
                                }

                                min_metric_id = target_BSR_metric_id;
                                min_metric_value = metric_val;
                                cur_metric_id = target_BSR_metric_id;
                                cur_metric_value = metric_val;
                            }
                            break;

                        case (int)Globals.metric_source_type.bsr_metric:
                            int bsr_metric_id = got_tp.metric_id;
                            decimal bsr_metric_val = 0.00m;

                            switch (target_level) {
                                case Globals.performance_target_level.Employee:
                                    bsr_metric_val = BSRDataItems.Where(b => b.id_field == entity_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                    break;
                                case Globals.performance_target_level.Store:
                                    bsr_metric_val = BSRDataItems.Where(b => b.store_id == store_or_higher_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                    break;
                                case Globals.performance_target_level.District:
                                    bsr_metric_val = BSRDataItems.Where(b => b.district_id == store_or_higher_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                    break;
                                case Globals.performance_target_level.Region:
                                    bsr_metric_val = BSRDataItems.Where(b => b.region_id == store_or_higher_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                    break;
                                case Globals.performance_target_level.Channel:
                                    bsr_metric_val = BSRDataItems.Where(b => b.channel_id == store_or_higher_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                    break;
                            }

                            min_metric_id = bsr_metric_id;
                            min_metric_value = bsr_metric_val;
                            cur_metric_id = bsr_metric_id;
                            cur_metric_value = bsr_metric_val;
                            pct_target = cur_metric_value;
                            target_val = cur_metric_value;  // GJK 8/24/2017 - there is no real target here its just a straight value from BSR, but for WRAP display purposes we need to show them as "hitting target"
                            break;

                        default:
                            continue;
                    }

                    // get the payout for the % to target from the payout range
                    decimal payout_val = GetPayoutValue(got_tp, pct_target);
                    decimal original_payout_val = payout_val;
                    decimal percent_of_metric_value = 0.00m;

                    // if we're paying a % of a BSR metric, pull that value here from the range above and apply - otherwise just pay out the result of the range % to target from above
                    if (got_tp.percent_of_metric_id.HasValue) {
                        int pct_metric_id = got_tp.percent_of_metric_id.Value;
                        // if the metric value we're using to calc the % is GP, use the value passed to the method as that will have any commissionable amounts added in - otherwise go get the value from BSR
                        percent_of_metric_value = (pct_metric_id == (int)Globals.BSR_metrics.GP_metric_id) ? comm_gp : BSRDataItems.Where(b => b.metric_id == pct_metric_id).Sum(b => b.total);

                        switch (got_tp.performance_target_level) {
                            case (int)Globals.performance_target_level.Employee:  // SC processing - these get a % of their metric value from the calculations done above
                                work_val = (percent_of_metric_value * payout_val);
                                break;
                            case (int)Globals.performance_target_level.Store:  // SL
                                // if we're processing GP then these get a % slightly differently, first the % target lookup above is multiplied by the base GP % for the store tier, then the result of that is the % to payout from GP
                                if (got_tp.percent_of_metric_id == (int)Globals.BSR_metrics.GP_metric_id) {
                                    Data.PerformanceTierDataItem got_tier = GetPerformanceTier(target_level, store_or_higher_id);
                                    if (got_tier != null) {
                                        got_target_val = got_tier.gross_profit_margin;
                                        payout_val = (got_target_val * payout_val);
                                        work_val = (percent_of_metric_value * payout_val);
                                    }
                                    else {
                                        payout_val = 0.00m;
                                        work_val = 0.00m;
                                    }
                                }
                                else
                                    work_val = (percent_of_metric_value * payout_val);
                                break;
                            case (int)Globals.performance_target_level.District:  // DL
                            case (int)Globals.performance_target_level.Region:  // RL
                            case (int)Globals.performance_target_level.Channel:  // Channel / Area
                                work_val = (percent_of_metric_value * payout_val);
                                break;
                        }
                    }
                    else
                        work_val = payout_val;

                    // GJK 9/1/2017 - added this extra step to support the new BJs comp structure, which is a tier on metic x -> payout on metric y -> single dollar amount in range 0-9999 -> (payout metric value * $ from range)
                    // so this new flag was added the payouts table to tell comp it should take the value from its payout metric and multiply it by the value from the payout range
                    if (got_tp.multiply_metric_val_by_payout_amt)
                        work_val = (cur_metric_value * payout_val);

                    // log the results
                    Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                    cvd.pay_period_id = ppd.pay_period_id;
                    cvd.entity_id = entity_id;
                    cvd.commissions_type_id = ctype.commissions_type_id;
                    cvd.performance_target_id = got_tp.commissions_target_payout_id;
                    cvd.target_value = target_val;
                    cvd.min_metric_id = min_metric_id;
                    cvd.min_metric_value = min_metric_value;
                    cvd.metric_id = cur_metric_id;
                    cvd.metric_value = pct_target;
                    cvd.percent_to_target = original_payout_val;
                    cvd.accelerator_value = payout_val;
                    cvd.performance_total = work_val;
                    cvd.payout_amount = percent_of_metric_value;

                    DAL.SavePerformanceCommissionsDetails(target_level, cvd);
                    DAL.Commit();

                    res.has_data = true;
                    retval += work_val;
                }

                catch (Exception e) {
                    string innermsg = e.InnerException != null ? ", inner exception: " + e.InnerException.Message : "";
                    AddToErrors("Unexpected error encountered in ProcessTargetPayouts for Target Level " + (int)target_level + ", Entity ID " + entity_id.ToString() + " : " + e.Message + innermsg);
                }
            };

            res.total = retval;
            return res;
        }

        public Data.TargetPayoutResult ProcessTeamBonuses(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, Data.commissions_pay_periods ppd_store, int entity_id, List<Data.BSRitems> BSRDataItemsAll, decimal comm_gp, decimal comm_gp_monthly, int store_or_higher_id, int commission_group_id) {
            Data.TargetPayoutResult res = new Data.TargetPayoutResult();
            res.has_data = false;
            res.box_attain_percent = -1;
            res.total = 0.00m;

            decimal work_val = 0.00m;
            decimal payout_value_range_match = 0.00m;
            int min_metric_id = -1;
            decimal min_metric_value = 0.00m;
            int cur_metric_id = -1;
            decimal cur_metric_value = 0.00m;
            decimal target_val = 0.00m;
            int ppd_id = 0;

            if (GOL.target_payout_team_bonus == null || GOL.target_payout_team_bonus.Count == 0)
                return res;

            if (target_level == Globals.performance_target_level.Employee)
                ppd_id = ppd.pay_period_id;
            else
                ppd_id = ppd_store.pay_period_id;

            // load all team bonuses for this pay period, level and the current user RQ4 commission group if any exists - if not get any default bonuses where the comm group is null
            List<Data.commissions_target_payout_team_bonus> team_bonuses_to_process = GOL.target_payout_team_bonus.Where(tpt => tpt.pay_period_id == ppd.pay_period_id && tpt.performance_target_level == (int)target_level).ToList();

            // GJK 10/23/2017 - moved this to here for the clear-down code below, first we load ALL payout types that could be created in this pay period and for the current level
            List<int> payout_types = new List<int>();
            foreach (Data.commissions_target_payout_team_bonus got_team_bonus in team_bonuses_to_process) {
                Data.commissions_type ctype_team_bonus = GetCommissionsType(got_team_bonus.commissions_type_code);

                // not pointing to a valid [commissions_type] or the data it references isn't loaded = skip
                if (ctype_team_bonus != null)
                    payout_types.Add(ctype_team_bonus.commissions_type_id);
            }

            // check if there are any which match the current user commission group and filter to process only those team bonus payouts - the commission group is mutally exclusive so if we have a group ID and it matches
            // at least one payout then we only process everything assigned to that group. Otherwise we only process that target payouts with no commission group ID
            if (commission_group_id > 0 && team_bonuses_to_process.Any(tb => tb.rq4_commission_group_id.HasValue && tb.rq4_commission_group_id.Value == commission_group_id))
                // get all team bonuses linked to this commission group only
                team_bonuses_to_process = team_bonuses_to_process.Where(tb => tb.rq4_commission_group_id.HasValue && tb.rq4_commission_group_id.Value == commission_group_id).ToList();
            else
                // get all team bonuses not linked to any commission group - the group ID is null
                team_bonuses_to_process = team_bonuses_to_process.Where(tb => !tb.rq4_commission_group_id.HasValue).ToList();

            if (team_bonuses_to_process.Count == 0)
                return res;

            bool any_payouts_per_location = (team_bonuses_to_process.Any(tb => tb.payout_per_assigned_location == true));
            List<int> location_ids = new List<int>();

            // GJK 10/28/2015 - added another layer to this, to allow the team bonus to be paid to each store this person is leader of: if this flag is not set, or the user is not assigned as the leader of a location, it defaults to the primary location instead
            // GJK 12/10/2015 - refined this, it really only applies to SC's who sell at multiple stores - SL's and upwards get paid per each location as part of the main processing loop so we don't want to pay them for all those locations here too
            List<Data.StoreItems> locations_to_process = new List<Data.StoreItems>();
            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    if (any_payouts_per_location)
                        locations_to_process = GOL.rq4_stores.Where(st => st.StoreManagerID == entity_id).ToList();
                    if (locations_to_process.Count == 0)
                        locations_to_process = GOL.rq4_stores.Where(st => st.StoreID == store_or_higher_id).ToList();
                    location_ids = locations_to_process.Select(loc => loc.StoreID).ToList();
                    break;
                case Globals.performance_target_level.Store:
                    locations_to_process = GOL.rq4_stores.Where(st => st.StoreID == store_or_higher_id).ToList();
                    location_ids = locations_to_process.Select(loc => loc.StoreID).ToList();
                    break;
                case Globals.performance_target_level.District:
                    locations_to_process = GOL.rq4_stores.Where(st => st.DistrictID == store_or_higher_id).ToList();
                    location_ids = locations_to_process.Select(loc => loc.DistrictID).ToList();
                    break;
                case Globals.performance_target_level.Region:
                    locations_to_process = GOL.rq4_stores.Where(st => st.RegionID == store_or_higher_id).ToList();
                    location_ids = locations_to_process.Select(loc => loc.RegionID).ToList();
                    break;
                case Globals.performance_target_level.Channel:
                    locations_to_process = GOL.rq4_stores.Where(st => st.ChannelID == store_or_higher_id).ToList();
                    location_ids = locations_to_process.Select(loc => loc.ChannelID).ToList();
                    break;
            }

            // load payout range values if not already loaded in this session
            if (GOL.target_payout_team_bonus_values == null || GOL.target_payout_team_bonus_values.Count == 0)
                GOL.target_payout_team_bonus_values = DAL.GetTargetPayoutTeamBonusValues();

            // GJK 11/12/2015: added this to delete any payouts that already exist but not for the target payouts from above - this is so if someone is moved to a different commissions group we don't leave any old data which belonged to the other group
            // remove anything isn't in this payout list for these locations - hate to do multiple loops but need to get the payout type IDs and remove things first, that way if it goes wrong at least the loop below this will put the correct values back!
            // GJK 10/23/2017: axosoft task 1320 - moved the list of payout_type IDs up to the top so this call will now find ALL payout types potentially created and then delete any that don't belong to the payout_IDs we're about to process
            List<int> payout_ids = team_bonuses_to_process.Select(tb => tb.commissions_target_payout_team_bonus_id).ToList();
            DAL.DeleteExistingPayoutsNotInThisList(target_level, ppd, payout_types, entity_id, null, payout_ids, location_ids);
            
            // run through the list of team bonuses and remove each one as we go when either processed or rejected, always working on the first item in the list for speed 
            while (team_bonuses_to_process.Count > 0) {
                Data.commissions_target_payout_team_bonus got_team_bonus = team_bonuses_to_process[0];

                try {
                    // get the actual bonus payout type (team bonus, monthly bonus)
                    Data.commissions_type ctype_team_bonus = GetCommissionsType(got_team_bonus.commissions_type_code);

                    // not pointing to a valid [commissions_type] or the data it references isn't loaded = skip
                    if (ctype_team_bonus == null)
                        continue;

                    // GJK 4/3/2017 - added this in, since the BSR monthly data passed in could be for multiple locations we need to filter it down per-location
                    List<Data.BSRitems> BSRDataItems = null;

                    foreach (Data.StoreItems location in locations_to_process) {
                        switch (target_level) {
                            // Updated the team bonus to pay out for all levels at store or higher, so this means everything in the team bonus list only gets paid once-per-month - but reps get team bonuses too so we switch up to using store level data if we're running this at rep level
                            case Globals.performance_target_level.Employee:
                            case Globals.performance_target_level.Store:
                                store_or_higher_id = location.StoreID;
                                BSRDataItems = BSRDataItemsAll.Where(bsr => bsr.store_id == store_or_higher_id).ToList();
                                break;
                            case Globals.performance_target_level.District:
                                store_or_higher_id = location.DistrictID;
                                BSRDataItems = BSRDataItemsAll.Where(bsr => bsr.store_id == store_or_higher_id).ToList();
                                break;
                            case Globals.performance_target_level.Region:
                                store_or_higher_id = location.RegionID;
                                BSRDataItems = BSRDataItemsAll.Where(bsr => bsr.store_id == store_or_higher_id).ToList();
                                break;
                            case Globals.performance_target_level.Channel:
                                store_or_higher_id = location.ChannelID;
                                BSRDataItems = BSRDataItemsAll.Where(bsr => bsr.store_id == store_or_higher_id).ToList();
                                break;
                        }

                        if (got_team_bonus.metric_source == (int)Globals.metric_source_type.bsr_metric && BSRDataItems == null)
                            continue;

                        // construct a list of metric values using a dynamic calculation engine - grabs metric ID's and math operators from a table and calculates data on-the-fly storing the results against metric IDs
                        // GJK 4/3/2017 - since BSR data could have multiple locations, moved this down to here to it only creates results bases on BSR for each location in the loop
                        List<Data.commissions_target_calculations> calc_number_breakdown = new List<Data.commissions_target_calculations>();
                        Dictionary<int, decimal> calculated_target_values = BuildCalculatedTargetMetrics(target_level, ppd, ppd_store, entity_id, store_or_higher_id, BSRDataItems, out calc_number_breakdown);

                        // if this target is linked to a tier that means there could be multiple targets linked to different tiers, so we only process the one that matches the correct tier
                        if (got_team_bonus.commissions_target_tiers_id.HasValue) {
                            int comm_target_tiers_id = got_team_bonus.commissions_target_tiers_id.Value;
                            Data.commissions_target_tiers got_target_tier = GOL.target_payout_tiers.Where(tpt => tpt.commissions_target_tiers_id == comm_target_tiers_id).FirstOrDefault();
                            if (got_target_tier == null)  // not linked to a valid target tier, skip this target payout
                                continue;

                            // check if the current metric value for this payout tier is with its defined range - if not, this tier doesn't apply so we skip this target payout
                            decimal metric_val = 0.00m;

                            // Do a quick check.  We now need to be able to base off of performance target values and not the metric value for tiers with store bonus Sept. 22, 2018

                            if (got_team_bonus.metric_source == (int)Globals.metric_source_type.performance_target) {
                                if (got_target_tier.metric_id == 169) {

                                    int performance_target_metric_id = got_team_bonus.metric_id;
                                    Data.PerformanceTarget selected_target = null;

                                    switch (target_level) {
                                        case Globals.performance_target_level.Employee:
                                        case Globals.performance_target_level.Store:
                                            selected_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)Globals.performance_target_level.Store && pt.store_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                            break;
                                    }

                                    if (selected_target != null) {
                                        // grab the set target
                                        target_val = selected_target.target_value;
                                    }
                                }

                                // This is the now new thing we are using tier codes for NSSMs target payouts.  If this is the case we need to pull the corresponding performance target value for that store
                                // From the monthly pay period.
                                if (got_target_tier.metric_id == 172) {

                                    // Get the metric ID
                                    using (Data.AWirelessDataContext db = new Data.AWirelessDataContext()) {
                                        // Need to make sure we are always in the month pay period.  As this metric deals strictly with the store target tiers that was uploaded.
                                        // This should return a list with the main month pay period in it.
                                        Data.commissions_pay_periods all_month_pay_period = db.commissions_pay_periods.Where(pp => pp.start_date.Month == ppd.start_date.Month && pp.start_date.Day == 1 && pp.end_date.Day > 16 && pp.start_date.Year == ppd.start_date.Year).FirstOrDefault();
                                        if (all_month_pay_period == null) {
                                            all_month_pay_period = db.commissions_pay_periods.Where(pp => pp.pay_period_id == 175) as Data.commissions_pay_periods;
                                        }

                                        Data.performance_target_metrics target_metric = db.performance_target_metrics.Where(ptm => ptm.metric_ID == 172 && ptm.pay_period_id == all_month_pay_period.pay_period_id).FirstOrDefault();

                                        // If we find the metric then we can proceed.
                                        if (target_metric != null) {
                                            int performance_target_metric_id = target_metric.performance_target_metric_id;

                                            Data.PerformanceTarget selected_target = null;

                                            switch (target_level) {
                                                case Globals.performance_target_level.Employee:
                                                case Globals.performance_target_level.Store:
                                                    selected_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)Globals.performance_target_level.Store && pt.store_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                                    break;
                                            }

                                            if (selected_target != null) {
                                                // grab the set target
                                                target_val = selected_target.target_value;
                                            }
                                        }
                                        else {
                                            continue;
                                        }
                                    }
                                }
                            }
                            // End of the hackery for now.

                            switch (target_level) {
                                case Globals.performance_target_level.Employee:
                                case Globals.performance_target_level.Store:
                                    // Possible solution.  If we supply the store target metric the then metric value needs to be the target value.
                                    if (got_target_tier.metric_id == 169 || got_target_tier.metric_id == 172)
                                        metric_val = target_val;
                                    else
                                        metric_val = BSRDataItems.Where(b => b.store_id == store_or_higher_id && b.metric_id == got_target_tier.metric_id).Sum(b => b.total);
                                    break;
                                case Globals.performance_target_level.District:
                                    metric_val = BSRDataItems.Where(b => b.district_id == store_or_higher_id && b.metric_id == got_target_tier.metric_id).Sum(b => b.total);
                                    break;
                                case Globals.performance_target_level.Region:
                                    metric_val = BSRDataItems.Where(b => b.region_id == store_or_higher_id && b.metric_id == got_target_tier.metric_id).Sum(b => b.total);
                                    break;
                                case Globals.performance_target_level.Channel:
                                    metric_val = BSRDataItems.Where(b => b.channel_id == store_or_higher_id && b.metric_id == got_target_tier.metric_id).Sum(b => b.total);
                                    break;
                            }

                            if (!(metric_val >= got_target_tier.start_value && metric_val <= got_target_tier.end_value)) {
                                // not this tier - make sure no previous payout exists for payout tier ID
                                DAL.DeleteExistingPayout(target_level, ppd, entity_id, -1, got_team_bonus.commissions_target_payout_team_bonus_id, store_or_higher_id);
                                continue;
                            }
                        }

                        switch (got_team_bonus.metric_source) {
                            case (int)Globals.metric_source_type.internal_calculated:
                                int calc_result_metric_id = got_team_bonus.metric_id;
                                payout_value_range_match = calculated_target_values.ContainsKey(calc_result_metric_id) ? calculated_target_values[calc_result_metric_id] : 0.00m;

                                // get the target and current value for the result calc if we have one
                                Data.commissions_target_calculations got_result_calc = calc_number_breakdown.FirstOrDefault(cb => cb.result_metric_id == calc_result_metric_id);
                                if (got_result_calc != null) {
                                    cur_metric_id = calc_result_metric_id;
                                    min_metric_id = (got_result_calc.left_metric_bsr_id.HasValue) ? got_result_calc.left_metric_bsr_id.Value : got_result_calc.left_metric_id;
                                    Data.PerformanceTarget got_right = GetTargetData(target_level, ppd, ppd_store, entity_id, store_or_higher_id, got_result_calc.right_metric_source, got_result_calc.right_metric_id, calculated_target_values, BSRDataItems);
                                    target_val = (got_right != null) ? got_right.target_value : 0.00m;
                                    Data.PerformanceTarget got_left = GetTargetData(target_level, ppd, ppd_store, entity_id, store_or_higher_id, got_result_calc.left_metric_source, got_result_calc.left_metric_id, calculated_target_values, BSRDataItems);
                                    min_metric_value = (got_left != null) ? got_left.target_value : 0.00m;
                                    cur_metric_value = min_metric_value;
                                }

                                // GJK 6/1/2016: if this is box attainment % save it to apply to final payout for East trial
                                if (calc_result_metric_id == 77 || calc_result_metric_id == 78)
                                    res.box_attain_percent = payout_value_range_match;
                                break;

                            case (int)Globals.metric_source_type.performance_target:
                                int performance_target_metric_id = got_team_bonus.metric_id;
                                Data.PerformanceTarget selected_target = null;

                                switch (target_level) {
                                    case Globals.performance_target_level.Employee:
                                    case Globals.performance_target_level.Store:
                                        selected_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)Globals.performance_target_level.Store && pt.store_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                        break;
                                    case Globals.performance_target_level.District:
                                        selected_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)target_level && pt.district_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                        break;
                                    case Globals.performance_target_level.Region:
                                        selected_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)target_level && pt.region_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                        break;
                                    case Globals.performance_target_level.Channel:
                                        selected_target = GOL.performance_targets.Where(pt => pt.target_level_id == (int)target_level && pt.channel_id == store_or_higher_id && pt.performance_target_metric_id == performance_target_metric_id).FirstOrDefault();
                                        break;
                                }

                                if (selected_target != null) {
                                    // grab the set target
                                    target_val = selected_target.target_value;
                                    int target_BSR_metric_id = -1;
                                    // grab the value for the metric - if this is a min check target we need to get the metric its checking against
                                    if (selected_target.used_for_min_check == true && selected_target.min_check_metric_ID.HasValue)
                                        target_BSR_metric_id = selected_target.min_check_metric_ID.Value;
                                    else
                                        target_BSR_metric_id = selected_target.metric_ID;

                                    decimal metric_val = 0.00m;

                                    switch (target_level) {
                                        case Globals.performance_target_level.Employee:
                                        case Globals.performance_target_level.Store:
                                            metric_val = BSRDataItems.Where(b => b.store_id == store_or_higher_id && b.metric_id == target_BSR_metric_id).Sum(b => b.total);
                                            break;
                                        case Globals.performance_target_level.District:
                                            metric_val = BSRDataItems.Where(b => b.district_id == store_or_higher_id && b.metric_id == target_BSR_metric_id).Sum(b => b.total);
                                            break;
                                        case Globals.performance_target_level.Region:
                                            metric_val = BSRDataItems.Where(b => b.region_id == store_or_higher_id && b.metric_id == target_BSR_metric_id).Sum(b => b.total);
                                            break;
                                        case Globals.performance_target_level.Channel:
                                            metric_val = BSRDataItems.Where(b => b.channel_id == store_or_higher_id && b.metric_id == target_BSR_metric_id).Sum(b => b.total);
                                            break;
                                    }

                                    // grab the target
                                    min_metric_id = target_BSR_metric_id;
                                    min_metric_value = target_val;
                                    cur_metric_id = target_BSR_metric_id;
                                    cur_metric_value = metric_val;
                                    decimal pct_target = 0.00m;

                                    // only proceed if the target is greater than zero and is not the auto-created default
                                    if (target_val > 0 && target_val != Globals.defaults.default_target_val)
                                        // calculate % to target
                                        pct_target = (metric_val / target_val);

                                    // this value is a % - format down to just four decimals so the range check will work correct due to the range data values being stored as four decimals
                                    payout_value_range_match = pct_target - (pct_target % 0.0001M);

                                    // GJK 6/1/2016: if this is box attainment % save it to apply to final payout for East trial
                                    if (target_BSR_metric_id == 119 || target_BSR_metric_id == 120)
                                        res.box_attain_percent = payout_value_range_match;
                                }
                                break;

                            case (int)Globals.metric_source_type.bsr_metric:
                                int bsr_metric_id = got_team_bonus.metric_id;
                                decimal bsr_metric_val = 0.00m;

                                switch (target_level) {
                                    case Globals.performance_target_level.Employee:
                                        bsr_metric_val = BSRDataItems.Where(b => b.id_field == entity_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                        break;
                                    case Globals.performance_target_level.Store:
                                        bsr_metric_val = BSRDataItems.Where(b => b.store_id == store_or_higher_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                        break;
                                    case Globals.performance_target_level.District:
                                        bsr_metric_val = BSRDataItems.Where(b => b.district_id == store_or_higher_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                        break;
                                    case Globals.performance_target_level.Region:
                                        bsr_metric_val = BSRDataItems.Where(b => b.region_id == store_or_higher_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                        break;
                                    case Globals.performance_target_level.Channel:
                                        bsr_metric_val = BSRDataItems.Where(b => b.channel_id == store_or_higher_id && b.metric_id == bsr_metric_id).Sum(b => b.total);
                                        break;
                                }

                                // grab the target
                                min_metric_id = bsr_metric_id;
                                min_metric_value = bsr_metric_val;
                                cur_metric_id = bsr_metric_id;
                                cur_metric_value = bsr_metric_val;
                                payout_value_range_match = bsr_metric_val;

                                // GJK 6/1/2016: if this is box attainment % save it to apply to final payout for East trial
                                if (bsr_metric_id == 119)
                                    res.box_attain_percent = payout_value_range_match;

                                break;
                        }

                        decimal payout_amount_tb = 0.00m;
                        decimal bonus_calc_val = 0.00m;

                        // GJK 10/2/2017 - axosoft task 1282:  the payout ranges are held in four decimal places, so we round the attainment value to match otherwise we could miss
                        // a real-world example was a rep who got 1.0999466666666666666666666667 in payout_value_range_match [attainment] which of course is slighly higher than 1.0999 but below the next range start of 1.1000
                        payout_value_range_match = Math.Round(payout_value_range_match, 4);
                        Data.commissions_target_payout_team_bonus_values got_payout_val = GOL.target_payout_team_bonus_values.Where(bv => bv.commissions_target_payout_team_bonus_id == got_team_bonus.commissions_target_payout_team_bonus_id
                                                                                                           && (payout_value_range_match >= bv.start_value && payout_value_range_match <= bv.end_value)).FirstOrDefault();

                        // does the % to target meet or exceed the required target set for the team bonus?
                        if (got_payout_val != null) {
                            if (got_team_bonus.percent_of_metric_id.HasValue) {
                                int pct_metric_id = got_team_bonus.percent_of_metric_id.Value;
                                // if the metric value we're using to calc the % is GP, use the value passed to the method as that will have any commissionable amounts added in - otherwise go get the value from BSR
                                bonus_calc_val = (pct_metric_id == (int)Globals.BSR_metrics.GP_metric_id) ? comm_gp_monthly : BSRDataItems.Where(b => b.id_field == entity_id && b.metric_id == pct_metric_id).Sum(b => b.total);
                                payout_amount_tb = (bonus_calc_val * got_payout_val.payout_value);

                                // if this team bonus gets paid regardless of min boxes, save the number to be passed back out instead of adding in to the commission total here. Then the callee can handle adding it to commission total outside the min check.
                                /* GJK 6/28/2016: this logic is long obsolete now
                                if (got_team_bonus.ignore_min_check)
                                    team_bonus_payout_val += payout_amount_tb;
                                else
                                */
                                work_val += payout_amount_tb;  // just add to the running grand total
                            }
                            else {
                                payout_amount_tb = got_payout_val.payout_value;
                                /* GJK 6/28/2016: this logic is long obsolete now
                                if (got_team_bonus.ignore_min_check)
                                    team_bonus_payout_val += payout_amount_tb;
                                else
                                */
                                work_val += payout_amount_tb;
                            }
                        }

                        // always save result to show team bonus is active and what current attainment % is
                        Data.CommissionValueDetails cvd_tb = new Data.CommissionValueDetails();
                        cvd_tb.pay_period_id = ppd.pay_period_id;
                        cvd_tb.entity_id = entity_id;
                        cvd_tb.commissions_type_id = ctype_team_bonus.commissions_type_id;
                        cvd_tb.payout_id = got_team_bonus.commissions_target_payout_team_bonus_id;
                        cvd_tb.metric_id = cur_metric_id;
                        cvd_tb.metric_value = cur_metric_value;
                        cvd_tb.min_metric_id = min_metric_id;
                        cvd_tb.min_metric_value = min_metric_value;
                        cvd_tb.target_value = target_val;
                        cvd_tb.percent_to_target = payout_value_range_match;
                        cvd_tb.payout_category = ctype_team_bonus.code;
                        cvd_tb.accelerator_value = (got_payout_val != null) ? got_payout_val.payout_value : 0;
                        cvd_tb.performance_total = payout_amount_tb;
                        cvd_tb.payout_count = (got_payout_val != null) ? 1 : 0;
                        cvd_tb.payout_amount = (got_payout_val != null) ? got_payout_val.payout_value : 0;
                        cvd_tb.payout_total = (got_payout_val != null) ? payout_amount_tb : 0;
                        cvd_tb.payout_location_id = store_or_higher_id;

                        DAL.SaveCategoryPayoutCommissionsDetails(target_level, cvd_tb);
                        DAL.Commit();
                        res.has_data = true;
                    }
                }
                finally {
                    team_bonuses_to_process.Remove(got_team_bonus);  // always remove this team bonus item from the list
                }
            }

            res.total = work_val;
            
            return res;
        }
        #endregion

        #endregion

        #region Special Payouts
        public decimal ApplyMultiplier(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, List<Data.BSRitems> BSRDataItems, decimal multiplier_value, decimal amount_value, bool team_bonus = false, int leader_id = -1) {
            decimal retval = 0.00m;
            decimal adj_val = 0.00m;

            if (BSRDataItems == null || BSRDataItems.Count == 0)
                return retval;

            Data.commissions_payouts_category got_special_cat = null;
            Data.commissions_type ctype = GetCommissionsType(Globals.commission_types.Payouts_cat);
            int channel_id = BSRDataItems.First().channel_id;
            if (channel_id != 6)
                return retval;

            // apply box attainment % to final payout for East trial if the payout exists for this level and pay period
            string cat_id = (team_bonus) ? "BOXATTAINACCTB" : "BOXATTAINACC";
            got_special_cat = GOL.payouts_category_list.Where(pcl => pcl.performance_target_level == (int)target_level && pcl.pay_period_id == ppd.pay_period_id && pcl.category_id == cat_id).FirstOrDefault();
            if (got_special_cat != null && multiplier_value > 0) {
                // set floor and ceiling values we can go no lower or higher than
                if (multiplier_value < 0.75m)
                    multiplier_value = 0.75m;
                else
                    if (multiplier_value > 1.25m)
                        multiplier_value = 1.25m;

                adj_val = (amount_value * multiplier_value);
                retval = (adj_val - amount_value);

                Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                cvd.pay_period_id = ppd.pay_period_id;
                cvd.entity_id = entity_id;
                cvd.commissions_type_id = ctype.commissions_type_id;
                cvd.min_metric_value = retval;  // analytics reporting logic requires this field to be set
                cvd.target_value = multiplier_value;
                cvd.payout_id = got_special_cat.commissions_payouts_category_id;
                cvd.payout_category = got_special_cat.category_id;
                cvd.payout_count = multiplier_value;
                cvd.payout_amount = amount_value;
                cvd.payout_total = retval;
                DAL.SaveCategoryPayoutCommissionsDetails(target_level, cvd);
                DAL.Commit();
            }

            // apply special east area DL booster for specific people if the payout exists for this level and pay period
            got_special_cat = GOL.payouts_category_list.Where(pcl => pcl.performance_target_level == (int)target_level && pcl.pay_period_id == ppd.pay_period_id && pcl.category_id == "COMMBOOST").FirstOrDefault();
            if (got_special_cat != null) {
                // GJK: yes this is a real thing. A true hack. A request to pay these specific people an extra amount.
                decimal multiply_by = 1.5m;
                int user_id = -1;

                switch (target_level) {
                    case Globals.performance_target_level.Employee:
                        user_id = entity_id;
                        break;
                    case Globals.performance_target_level.Store:
                    case Globals.performance_target_level.District:
                    case Globals.performance_target_level.Region:
                        user_id = leader_id;
                        break;
                }

                // so far we have Gene [Jack] Arrowood 9807 and Steven Haupt 9541...
                if (user_id == 9541 || user_id == 9807) {
                    adj_val = (amount_value * multiply_by);
                    retval = (adj_val - amount_value);

                    Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                    cvd.pay_period_id = ppd.pay_period_id;
                    cvd.entity_id = entity_id;
                    cvd.commissions_type_id = ctype.commissions_type_id;
                    cvd.target_value = multiplier_value;
                    cvd.payout_id = got_special_cat.commissions_payouts_category_id;
                    cvd.payout_category = got_special_cat.category_id;
                    cvd.payout_count = multiply_by;
                    cvd.payout_amount = amount_value;
                    cvd.payout_total = retval;

                    DAL.SaveCategoryPayoutCommissionsDetails(target_level, cvd);
                    DAL.Commit();
                }
            }

            return retval;
        }

        public bool CreatePendingPayout(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, string commission_type, string payout_code, int target_id, decimal payout_count) {
            bool res = false;

            int payout_id = -1;
            decimal payout_amount = 0.00m;

            switch (commission_type.ToLower()) {
                case "pay_sku":
                    Data.commissions_payouts_sku ps = GOL.payouts_sku_list.FirstOrDefault(psl => psl.performance_target_level == (int)target_level && psl.sku == payout_code);
                    if (ps != null) {
                        payout_id = ps.commissions_payouts_sku_id;
                        payout_amount = ps.amount;
                    }
                    break;

                case "pay_cat":
                    Data.commissions_payouts_category pc = GOL.payouts_category_list.FirstOrDefault(pcl => pcl.performance_target_level == (int)target_level && pcl.category_id == payout_code);
                    if (pc != null) {
                        payout_id = pc.commissions_payouts_category_id;
                        payout_amount = pc.amount;
                    }
                    break;
            }

            if (payout_id <= 0)
                return res;

            Data.commissions_pending_payouts cpp = DAL.db().commissions_pending_payouts.FirstOrDefault(pp => pp.pay_period_id == ppd.pay_period_id && pp.target_level == (int)target_level && pp.commission_type == commission_type && pp.target_id == target_id);
            if (cpp == null) {
                cpp = new Data.commissions_pending_payouts();
                cpp.pay_period_id = ppd.pay_period_id;
                cpp.commission_type = commission_type;
                cpp.target_level = (int)target_level;
                cpp.target_id = target_id;
                cpp.payout_code = payout_code;
                DAL.db().commissions_pending_payouts.InsertOnSubmit(cpp);
            }
            cpp.payout_count = payout_count;
            cpp.payout_value = payout_amount;
            cpp.payout_total = (payout_amount * payout_count);
            DAL.Commit();

            return res;
        }

        public decimal GetPendingPayouts(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int target_id) {
            decimal res = 0.00m;

            // get all pending payouts for this level and ID, create/update each payout record, and return the grand total
            List<Data.commissions_pending_payouts> cpps = DAL.db().commissions_pending_payouts.Where(pp => pp.pay_period_id == ppd.pay_period_id && pp.target_level == (int)target_level && pp.target_id == target_id).ToList();

            foreach (Data.commissions_pending_payouts cpp in cpps) {
                Data.commissions_type ctype = GetCommissionsType(cpp.commission_type);

                if (ctype != null) {
                    Data.CommissionValueDetails cvd = new Data.CommissionValueDetails();
                    cvd.pay_period_id = ppd.pay_period_id;
                    cvd.entity_id = target_id;
                    cvd.commissions_type_id = ctype.commissions_type_id;
                    cvd.payout_id = cpp.payout_id;
                    cvd.payout_count = cpp.payout_count;
                    cvd.payout_amount = cpp.payout_value;
                    cvd.payout_total = cpp.payout_total;

                    switch (cpp.commission_type) {
                        case "pay_sku":
                            cvd.payout_sku = cpp.payout_code;
                            DAL.SaveSkuPayoutCommissionsDetails(target_level, cvd);
                            DAL.Commit();
                            break;
                        case "pay_cat":
                            cvd.payout_category = cpp.payout_code;
                            DAL.SaveCategoryPayoutCommissionsDetails(target_level, cvd);
                            DAL.Commit();
                            break;
                    }

                    res += cpp.payout_value;
                }

                // processed this pending payout, remove it now
                DAL.db().commissions_pending_payouts.DeleteOnSubmit(cpp);
                DAL.Commit();
            }

            return res;
        }

        #endregion

        #region Info and Error list routines

        public void InitMessageLists() {
            if (info == null) {
                info = new List<string>();
            }
            else {
                info.Clear();
            }

            if (errors == null) {
                errors = new List<string>();
            }
            else {
                errors.Clear();
            }

            if (notify == null) {
                notify = new List<string>();
            }
            else {
                notify.Clear();
            }

            if (debug == null) {
                debug = new List<string>();
            }
            else {
                debug.Clear();
            }

            // load existing messages so we can restrict number of times some are sent, and clear out any messages older than 12 hours so existing messages can be re-sent
            log_list = DAL.GetCompPlanLog(true);
        }

        public void AddToInfo(string msg) {
            if (info != null) {
                info.Add(msg);
            }
        }

        public void AddToErrors(string msg) {
            if (errors != null) {
                errors.Add(msg);
            }
        }

        public void AddToNotify(string msg) {
            // only send these notification if we haven't already sent it - this log is cleared down at a set interval during load to allow message to be re-sent
            if (notify != null && !log_list.Any(l => l.message_type == (int)Globals.comp_plan_log_message_type.notification && l.message_text == msg)) {
                notify.Add(msg);
            }
        }

        public void AddToDebug(string msg) {
            // only send these notification if we haven't already sent it - this log is cleared down at a set interval during load to allow message to be re-sent
            if (debug != null) {
                debug.Add(msg);
            }
        }

        public void SendInfoLists() {
            if ((info != null && info.Count > 0) || (errors != null && errors.Count > 0) || (notify != null && notify.Count > 0) || (debug != null && debug.Count > 0)) {
                Tools.Mailer mailer = new Tools.Mailer();

                string sSoftwareDev = "";
                string sNotifyPeople = "";

#if DEBUG
                sSoftwareDev = ConfigurationManager.AppSettings["debugemailTo"].ToString();
                sNotifyPeople = ConfigurationManager.AppSettings["debugemailTo"].ToString();
#else
                sNotifyPeople = ConfigurationManager.AppSettings["emailNotify"].ToString();
                sSoftwareDev = ConfigurationManager.AppSettings["emailTo"].ToString();
#endif

                if (info.Count > 0) {
                    mailer.SendEmail(sNotifyPeople, "CompPlan info", "CompPlan information message(s):" + Environment.NewLine + String.Join(Environment.NewLine, info), sSoftwareDev, false);
                    DAL.SaveCompPlanLog(Globals.comp_plan_log_message_type.info, info);
                }

                if (errors.Count > 0) {
                    mailer.SendEmail(sSoftwareDev, "CompPlan errors", "CompPlan encountered the following errors while processing:" + Environment.NewLine + String.Join(Environment.NewLine, errors), "", false);
                    DAL.SaveCompPlanLog(Globals.comp_plan_log_message_type.error, errors);
                }

                if (notify.Count > 0) {
                    mailer.SendEmail(sNotifyPeople, "CompPlan issues", "CompPlan encountered the following issues while processing:" + Environment.NewLine + String.Join(Environment.NewLine, notify), sSoftwareDev, false);
                    DAL.SaveCompPlanLog(Globals.comp_plan_log_message_type.notification, notify);
                }

                if (debug.Count > 0) {
                    mailer.SendEmail(ConfigurationManager.AppSettings["debugemailTo"].ToString(), "CompPlan debug info", "Debugging Info:" + Environment.NewLine + String.Join(Environment.NewLine, debug), "", false);
                }
            }
        }

        #endregion

    }
}

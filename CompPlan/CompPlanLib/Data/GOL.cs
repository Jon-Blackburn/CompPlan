using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

// Global Objects Layer
namespace CompPlanLib.Data {
    public class GOL {
        // data access layer class
        Data.DAL DAL;

        // load-on-demand lists
        public List<Data.commissions_type> commissions_types = null;
        public List<Data.vw_iQmetrix_Employees> iqmetrix_employees = null;
        public List<Data.EmployeeItems> rq4_sales_consultants = null;
        public List<Data.StoreItems> rq4_stores = null;
        public List<Data.HitTargetStore> hit_target_stores = null;
        public List<Data.HitTargetStore> hit_target_districts = null;
        public List<Data.commissions_participation> commissions_participations = null;
        public List<Data.commissions_accelerator> commissions_accelerators = null;
        public List<Data.commissions_accelerator_values> commissions_accelerator_values = null;
        public List<commissions_accelerator_source_types> commissions_accelerator_source_types = null;
        public List<Data.PerformanceTarget> performance_targets = null;
        public List<Data.commissions_payouts_sku> payouts_sku_list = null;
        public List<Data.commissions_payouts_category> payouts_category_list = null;
        public List<Data.commissions_payouts_category_values> payouts_category_values_list = null;
        public List<Data.commissions_payouts_minimum> payouts_minimum_list = null;
        public List<Data.performance_tiers> performance_tiers = null;
        public List<Data.PerformanceTierDataItem> performance_tiers_data = null;
        public List<Data.commissions_tier_assignment> tier_assigments = null;
        public List<Data.commissions_kpi> kpi_list = null;
        public List<Data.commissions_kpi_points_reward_values> kpi_values = null;
        public List<Data.commissions_kpi_goal_points> kpi_points = null;
        public List<Data.sp_get_commissions_KPI_dataResult> kpi_data = null;
        public List<Data.commissions_target_calculations> target_calculations = null;
        public List<Data.commissions_target_payout> target_payouts = null;
        public List<Data.commissions_target_payout_values> target_payout_values = null;
        public List<Data.commissions_target_payout_team_bonus> target_payout_team_bonus = null;
        public List<Data.commissions_target_tiers> target_payout_tiers = null;
        public List<Data.InvoiceItems> RQ4Data_payout_sku_invoices_for_returns = null;
        public List<Data.sp_get_commissions_custom_dataResult> custom_commissions_data = null;
        public List<Data.commissions_target_payout_team_bonus_values> target_payout_team_bonus_values = null;  // load-on-demand, loaded on first pass of ProcessTeamBonuses()
        public List<Data.sp_get_commissions_RQ4_employee_storesResult> rq4_employee_store_assignments = null;

        // manual adjustments and coupons
        public List<Data.sp_get_commissions_manual_adjustmentsResult> manual_adjustments = null;
        public List<Data.sp_get_commissions_couponsResult> coupons = null;

        // MSO payout spiff lists
        public List<Data.sp_get_commissions_MSO_dataResult> CommissionsExtractData = null;

        public Data.commissions_pay_periods ppd_previous;

        public int returns_cutoffday = 14;

        public GOL(Data.DAL DataAccessLayer) {
            DAL = DataAccessLayer;

            if (ConfigurationManager.AppSettings["ReturnsWindowDays"] != null)
                int.TryParse(ConfigurationManager.AppSettings["ReturnsWindowDays"].ToString(), out returns_cutoffday);
        }

        public void LoadGlobalLists(DateTime rundate, Data.commissions_pay_periods ppd) {
            if (iqmetrix_employees == null || iqmetrix_employees.Count == 0)
                iqmetrix_employees = DAL.GetIQmetrixEmployees();

            if (rq4_sales_consultants == null || rq4_sales_consultants.Count == 0)
                rq4_sales_consultants = DAL.GetEmployeeData();

            if (rq4_stores == null || rq4_stores.Count == 0)
                rq4_stores = DAL.GetStoreData(ppd.start_date);

            // load performance target data for this pay period if we don't already have it
            if (performance_targets == null || performance_targets.Count == 0 || !performance_targets.Select(c => c.pay_period_id).Contains(ppd.pay_period_id))
                performance_targets = DAL.GetPerformanceTargets(ppd.pay_period_id);

            // load performance tiers data for this pay period if we don't already have it
            // header only
            if (performance_tiers == null || performance_tiers.Count == 0 || !performance_tiers.Select(c => c.pay_period_id).Contains(ppd.pay_period_id))
                performance_tiers = DAL.GetPerformanceTiers(ppd.pay_period_id);

            // header and detail tier data combined
            if (performance_tiers_data == null || performance_tiers_data.Count == 0 || !performance_tiers_data.Select(c => c.pay_period_id).Contains(ppd.pay_period_id))
                performance_tiers_data = DAL.GetPerformanceTiersData(ppd.pay_period_id);

            // load any performance tier assignments data for this pay period if we don't already have it, this is used to auto-populate the tiers
            if (tier_assigments == null || tier_assigments.Count == 0 || !tier_assigments.Select(c => c.pay_period_id).Contains(ppd.pay_period_id))
                tier_assigments = DAL.GetTierAssignments(ppd.pay_period_id);

            // load accelerators
            if (commissions_accelerator_source_types == null || commissions_accelerator_source_types.Count == 0) {
                if (commissions_accelerator_source_types == null)
                    commissions_accelerator_source_types = new List<Data.commissions_accelerator_source_types>();

                commissions_accelerator_source_types = DAL.GetCommissionsAcceleratorSourceTypes();
            }

            if (commissions_accelerators == null || commissions_accelerators.Count == 0 || !commissions_accelerators.Select(c => c.pay_period_id).Contains(ppd.pay_period_id)) {
                commissions_accelerators = DAL.GetCommissionsAcceleratorForPayPeriod(ppd.pay_period_id);

                commissions_accelerators.ForEach(ca => {
                    if (commissions_accelerator_values == null || commissions_accelerator_values.Count == 0 || !commissions_accelerator_values.Select(c => c.commissions_accelerator_id).Contains(ca.commissions_accelerator_id)) {
                        if (commissions_accelerator_values == null)
                            commissions_accelerator_values = new List<Data.commissions_accelerator_values>();
                        commissions_accelerator_values.AddRange(DAL.GetCommissionsAcceleratorValues(ca.commissions_accelerator_id));
                    }                   
                }
            );
            }

            // load payout data for each performance target if we don't already have it
            DAL.GetPerformanceTargetMetrics(ppd.pay_period_id).ForEach(pt => {
                // load participation index data if we don't already have it
                if (commissions_participations == null || commissions_participations.Count == 0 || !commissions_participations.Select(c => c.performance_target_metrics_id).Contains(pt.performance_target_metric_id)) {
                    if (commissions_participations == null)
                        commissions_participations = new List<Data.commissions_participation>();

                    commissions_participations.AddRange(DAL.GetCommissionsParticipation(pt.performance_target_metric_id));
                }

            });

            // load payout list if not already loaded for this date range
            if (payouts_sku_list == null || payouts_sku_list.Count == 0 || !payouts_sku_list.Any(p => p.end_date > ppd.start_date && p.start_date < ppd.end_date))
                payouts_sku_list = DAL.GetPayoutSkus(ppd.start_date, ppd.end_date);

            // load category payout list if not already loaded for this date range
            if (payouts_category_list == null || payouts_category_list.Count == 0 || !payouts_category_list.Select(c => c.pay_period_id).Contains(ppd.pay_period_id)) {
                payouts_category_list = DAL.GetPayoutCategories(rundate, ppd.pay_period_id);

                // for each payout category, load any defined range values (to accomodate a spiff that pays different amounts for a different BSR value)
                payouts_category_list.ForEach(pc => {
                    if (payouts_category_values_list == null)
                        payouts_category_values_list = new List<Data.commissions_payouts_category_values>();

                    if (!payouts_category_values_list.Any(pcv => pcv.commissions_payouts_category_id == pc.commissions_payouts_category_id))
                        payouts_category_values_list.AddRange(DAL.GetPayoutCategoryValues(pc.commissions_payouts_category_id));
                });
            }

            // load min payouts if not already loaded for this date range
            if (payouts_minimum_list == null || payouts_minimum_list.Count == 0 || !payouts_minimum_list.Any(p => p.end_date > ppd.start_date && p.start_date < ppd.end_date))
                payouts_minimum_list = DAL.GetPayoutMinimums(ppd.start_date, ppd.end_date);

            // load list of commissions types
            if (commissions_types == null || commissions_types.Count == 0)
                commissions_types = DAL.GetCommissionsTypes();

            // list of stores and districts that hit target - populated during accelerator run in ProcessPerformanceTargets
            if (hit_target_stores == null)
                hit_target_stores = new List<Data.HitTargetStore>();
            else
                hit_target_stores.Clear();

            if (hit_target_districts == null)
                hit_target_districts = new List<Data.HitTargetStore>();
            else
                hit_target_districts.Clear();

            // load manual adjustments
            if (manual_adjustments == null || manual_adjustments.Count == 0 || !manual_adjustments.Any(p => p.DateCreated >= ppd.start_date))
                manual_adjustments = DAL.GetManualAdjustments(ppd.start_date);

            // load all coupons
            if (coupons == null || coupons.Count == 0)
                coupons = DAL.GetCoupons();

            // load KPI data
            if (kpi_list == null || kpi_list.Count == 0 || !kpi_list.Select(c => c.pay_period_id).Contains(ppd.pay_period_id)) {
                kpi_list = DAL.GetKpi(ppd.pay_period_id);

                kpi_list.ForEach(k => {
                    DAL.GetKpiGroups(k.commissions_kpi_id).ForEach(kg => {
                        if (kpi_points == null || kpi_points.Count == 0 || !kpi_points.Any(a => a.commissions_kpi_groups_id == kg.commissions_kpi_groups_id)) {
                            if (kpi_points == null) {
                                kpi_points = new List<Data.commissions_kpi_goal_points>();
                            }

                            kpi_points.AddRange(DAL.GetKpiPoints(kg.commissions_kpi_groups_id));
                        }
                    });
                });
            }

            if (kpi_values == null || kpi_values.Count == 0 || !kpi_values.Select(c => c.pay_period_id).Contains(ppd.pay_period_id))
                kpi_values = DAL.GetKpiValues(ppd.pay_period_id);

            // special list of KPI and items (employee, store, district, region IDs) joined together
            if (kpi_data == null || kpi_data.Count == 0 || !kpi_data.Select(c => c.pay_period_id).Contains(ppd.pay_period_id))
                kpi_data = DAL.GetKpiData(ppd.pay_period_id);

            // target calculations
            if (target_calculations == null || target_calculations.Count == 0 || !target_calculations.Select(c => c.pay_period_id).Contains(ppd.pay_period_id))
                target_calculations = DAL.GetCalculatedTargets(ppd.pay_period_id);

            // Target payouts
            if (target_payouts == null || target_payouts.Count == 0 || !target_payouts.Select(c => c.pay_period_id).Contains(ppd.pay_period_id)) {
                target_payouts = DAL.GetTargetPayouts(ppd.pay_period_id);

                target_payouts.ForEach(tp => {
                    if (target_payout_values == null || target_payout_values.Count == 0 || !target_payout_values.Select(c => c.commissions_target_payout_id).Contains(tp.commissions_target_payout_id)) {
                        if (target_payout_values == null) {
                            target_payout_values = new List<Data.commissions_target_payout_values>();
                        }

                        target_payout_values.AddRange(DAL.GetTargetPayoutValues(tp.commissions_target_payout_id));
                    }

                    if (target_payout_team_bonus == null || target_payout_team_bonus.Count == 0 || !target_payout_team_bonus.Select(c => c.pay_period_id).Contains(ppd.pay_period_id)) {
                        if (target_payout_team_bonus == null) {
                            target_payout_team_bonus = new List<Data.commissions_target_payout_team_bonus>();
                        }

                        target_payout_team_bonus.AddRange(DAL.GetTargetPayoutTeamBonus(ppd.pay_period_id));
                    }
                }
                );
            }

            // target payout tiers
            if (target_payout_tiers == null || target_payout_tiers.Count == 0 || !target_payout_tiers.Select(c => c.pay_period_id).Contains(ppd.pay_period_id))
                target_payout_tiers = DAL.GetPayoutTiers(ppd.pay_period_id);

            // custom commissions data
            if (custom_commissions_data == null || custom_commissions_data.Count == 0 | !custom_commissions_data.Select(c => c.pay_period_id).Contains(ppd.pay_period_id))
                custom_commissions_data = DAL.GetCustomCommissionsData(ppd.pay_period_id);

            if (rq4_employee_store_assignments == null || rq4_employee_store_assignments.Count == 0)
                rq4_employee_store_assignments = DAL.GetRQ4EmployeeStores();

            // load all invoices going back to the start of the return period window - this is so we can check for returns on or after a promotion start and only include those returns on sales that happened inside the returns window
            if (RQ4Data_payout_sku_invoices_for_returns == null || RQ4Data_payout_sku_invoices_for_returns.Count == 0) {
                DateTime start_of_return_period = ppd.start_date.Date.AddDays(-returns_cutoffday);
                RQ4Data_payout_sku_invoices_for_returns = DAL.GetRQ4InvoicesForDate(start_of_return_period, ppd.end_date);
            }

            // get previous pay period data for reference by various payout helper and other methods
            ppd_previous = DAL.GetPreviousPayPeriod(ppd.pay_period_id, ppd.for_consultants);

            // SC target values references store targets, so full store level upward data needs to be loaded too
            if (ppd.for_consultants == true) {
                Data.commissions_pay_periods ppd_store = DAL.GetPayPeriod(ppd.start_date, false);
                if (!performance_targets.Select(c => c.pay_period_id).Contains(ppd_store.pay_period_id))
                    performance_targets.AddRange(DAL.GetPerformanceTargets(ppd_store.pay_period_id));

                if (!target_calculations.Select(c => c.pay_period_id).Contains(ppd_store.pay_period_id))
                    target_calculations.AddRange(DAL.GetCalculatedTargets(ppd_store.pay_period_id));

                // load store level custom data
                if (custom_commissions_data == null)  // should have been loaded already, but just in case
                    custom_commissions_data = DAL.GetCustomCommissionsData(ppd.pay_period_id);

                if (!custom_commissions_data.Select(c => c.pay_period_id).Contains(ppd_store.pay_period_id))
                    custom_commissions_data.AddRange(DAL.GetCustomCommissionsData(ppd_store.pay_period_id));
            }
        }

        public void ClearGlobalLists() {
            if (iqmetrix_employees != null || iqmetrix_employees.Count > 0)
                iqmetrix_employees.Clear();

            if (rq4_sales_consultants != null || rq4_sales_consultants.Count > 0)
                rq4_sales_consultants.Clear();

            if (rq4_stores != null || rq4_stores.Count > 0)
                rq4_stores.Clear();

            if (commissions_participations != null && commissions_participations.Count > 0)
                commissions_participations.Clear();

            if (commissions_accelerators != null && commissions_accelerators.Count > 0)
                commissions_accelerators.Clear();

            if (performance_targets != null && performance_targets.Count > 0)
                performance_targets.Clear();

            if (payouts_sku_list != null && payouts_sku_list.Count > 0)
                payouts_sku_list.Clear();

            if (payouts_category_list != null && payouts_category_list.Count > 0)
                payouts_category_list.Clear();

            if (hit_target_stores != null && hit_target_stores.Count > 0)
                hit_target_stores.Clear();

            if (hit_target_districts != null && hit_target_districts.Count > 0)
                hit_target_districts.Clear();

            if (manual_adjustments != null && manual_adjustments.Count > 0)
                manual_adjustments.Clear();

            if (coupons != null && coupons.Count > 0)
                coupons.Clear();

            if (kpi_list != null && kpi_list.Count > 0)
                kpi_list.Clear();

            if (kpi_values != null && kpi_values.Count > 0)
                kpi_values.Clear();

            if (kpi_data != null && kpi_data.Count > 0)
                kpi_data.Clear();

            if (kpi_points != null && kpi_points.Count > 0)
                kpi_points.Clear();

            if (target_calculations != null && target_calculations.Count > 0)
                target_calculations.Clear();

            if (target_payouts != null && target_payouts.Count > 0)
                target_payouts.Clear();

            if (target_payout_values != null && target_payout_values.Count > 0)
                target_payout_values.Clear();

            if (target_payout_tiers != null && target_payout_tiers.Count > 0)
                target_payout_tiers.Clear();

            if (custom_commissions_data != null && custom_commissions_data.Count > 0)
                custom_commissions_data.Clear();

            if (target_payout_team_bonus_values != null && target_payout_team_bonus_values.Count > 0)
                target_payout_team_bonus_values.Clear();

            if (rq4_employee_store_assignments != null && rq4_employee_store_assignments.Count > 0)
                rq4_employee_store_assignments.Clear();
        }
    }
}

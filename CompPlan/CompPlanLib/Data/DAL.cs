using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Configuration;

namespace CompPlanLib.Data {
    public class DAL {
        // DB connections
        Data.AWirelessDataContext SQLserverDB_live;
        Data.AWirelessDataContext SQLserverDB_readonly;

        // global private variables
        private Dictionary<string, int> pay_period_count = new Dictionary<string, int>();

        #region Constructor/Destructor

        public DAL(string SQLServerConnectionString_live, string SQLServerConnectionString_readonly) {
            // set up DB connections - if no connection string is passed in, default to using the connection strings the DBML was mapped to when it was created that are in the local .config
            if (SQLServerConnectionString_live != "")
                SQLserverDB_live = new Data.AWirelessDataContext(SQLServerConnectionString_live);
            else
                SQLserverDB_live = new Data.AWirelessDataContext();

            if (SQLServerConnectionString_readonly != "")
                SQLserverDB_readonly = new Data.AWirelessDataContext(SQLServerConnectionString_readonly);
            else
                SQLserverDB_readonly = new Data.AWirelessDataContext();

            SQLserverDB_live.CommandTimeout = 0;
            SQLserverDB_readonly.CommandTimeout = 0;
        }

        #endregion

        #region DB commit

        public void Commit() {
            SQLserverDB_live.SubmitChanges();
        }

        #endregion

        #region DB object access

        public Data.AWirelessDataContext db() {
            return SQLserverDB_live;
        }

        public string SQLServerConnectionString_live() {
            return SQLserverDB_live.Connection.ConnectionString;
        }

        public string SQLServerConnectionString_readonly() {
            return SQLserverDB_readonly.Connection.ConnectionString;
        }

        #endregion

        #region Data Loading Routines

        public Data.commissions_configuration GetCommissionsConfig(Globals.performance_target_level target_level, int pay_period_id) {
            Data.commissions_configuration got_config = SQLserverDB_readonly.commissions_configuration.FirstOrDefault(p => p.performance_target_level == (int)target_level && p.pay_period_id == pay_period_id);

            // auto-roll the config into a new pay period using values from the previous pay period
            if (got_config == null) {
                got_config = new Data.commissions_configuration();
                Data.commissions_configuration prev_config = SQLserverDB_live.commissions_configuration.OrderByDescending(o => o.pay_period_id).FirstOrDefault(p => p.performance_target_level == (int)target_level && p.pay_period_id < pay_period_id);

                if (prev_config != null) {
                    got_config.performance_target_level = (int)target_level;
                    got_config.pay_period_id = pay_period_id;
                    got_config.default_tier_code = prev_config.default_tier_code;
                }
                else {
                    got_config.performance_target_level = (int)target_level;
                    got_config.pay_period_id = pay_period_id;
                    got_config.default_tier_code = "?";
                }

                SQLserverDB_live.commissions_configuration.InsertOnSubmit(got_config);
                SQLserverDB_live.SubmitChanges();
            }

            return got_config;
        }

        public List<Data.commissions_type> GetCommissionsTypes() {
            return SQLserverDB_readonly.commissions_type.ToList();
        }

        public List<Data.vw_iQmetrix_Employees> GetIQmetrixEmployees() {
            return SQLserverDB_readonly.vw_iQmetrix_Employees.ToList();
        }

        public List<Data.EmployeeItems> GetEmployeeData() {
            // uses stored proc sp_get_commissions_employees to get the data from RQ4, saves lots of LINQ joins!
            return SQLserverDB_readonly.sp_get_commissions_employees().Select
                (e => new Data.EmployeeItems {
                    FirstName = e.First_Name,
                    LastName = e.Last_Name,
                    EmployeeName = e.Employee_Name,
                    IdNumber = e.Id_Number,
                    SpecialIdentifier = e.SpecialIdentifier,
                    DefaultLocation = e.DefaultLocation,
                    AccountDisabled = e.Account_Disabled,
                    startDate = e.StartDate,
                    ChannelID = e.channel_id,
                    ChannelName = e.channel_name,
                    RegionID = e.region_id,
                    RegionName = e.region_name,
                    DistrictID = e.district_id,
                    DistrictName = e.district_name,
                    StoreID = e.store_id,
                    StoreName = e.store_name,
                    RQ4CommissionGroupID = e.CommissionGroupID.Value,
                    LastCompUpdate = (e.last_comp_update.HasValue) ? e.last_comp_update.Value : DateTime.Now
                }).ToList();
        }

        public List<Data.StoreItems> GetStoreData(DateTime rundate) {
            // uses stored proc sp_get_commissions_employees to get the data from RQ4, saves lots of LINQ joins!
            return SQLserverDB_readonly.sp_get_commissions_stores().Select
                (s => new Data.StoreItems {
                    StoreID = s.store_id.Value,
                    StoreAbbrev = s.store_pos_id,
                    StoreManagerID = s.store_manager_id.Value,
                    StoreName = s.store_name,
                    DistrictID = s.district_id.Value,
                    DistrictManagerID = s.district_manager_id,
                    DistrictName = s.district_name,
                    RegionID = s.region_id.Value,
                    RegionManagerID = s.region_manager_id,
                    RegionName = s.region_name,
                    ChannelID = s.channel_id.Value,
                    ChannelName = s.channel_name,
                    ChannelLeaderID = s.channel_leader_id.Value,
                    OpenDate = s.store_open_date,
                    CloseDate = s.store_close_date,
                    totalMonths = (s.store_open_date.HasValue) ? Globals.MonthDifference(rundate, s.store_open_date.Value) : 0,
                    StoreManagerCommissionGroupID = s.store_leader_commission_group_id,
                    DistrictManagerCommissionGroupID = s.district_leader_commission_group_id,
                    RegionManagerCommissionGroupID = s.region_leader_commission_group_id,
                    StoreTypeID = (s.store_type_id.HasValue) ? s.store_type_id.Value : -1
                }).ToList();
        }

        public Data.commissions_pay_periods GetPayPeriod(DateTime fordate, bool ForSC) {
            return SQLserverDB_readonly.commissions_pay_periods.Where(p => p.start_date <= fordate && p.end_date >= fordate && p.for_consultants == ForSC).FirstOrDefault();
        }

        public Data.commissions_pay_periods GetPreviousPayPeriod(int currentPayperiodID, bool ForSC) {
            return SQLserverDB_readonly.commissions_pay_periods.OrderByDescending(o => o.pay_period_id).FirstOrDefault(p => p.pay_period_id < currentPayperiodID && p.for_consultants == ForSC);
        }

        public int GetPayPeriodCount(DateTime fordate, bool forSC) {
            int retval = 0;

            string datekey = fordate.ToString() + "_" + forSC.ToString();

            // check for count in cache, and use this instead if exists to save a DB hit
            if (pay_period_count.ContainsKey(datekey)) {
                retval = pay_period_count[datekey];
            }
            else {
                retval = SQLserverDB_readonly.commissions_pay_periods.Where(p => ((p.start_date.Month == fordate.Month && p.start_date.Year == fordate.Year) && (p.end_date.Month == fordate.Month && p.end_date.Year == fordate.Year)) && p.for_consultants == forSC).Count();

                pay_period_count.Add(datekey, retval);
            }

            return retval;
        }

        public List<Data.commissions_participation> GetCommissionsParticipationForPayPeriod(int pay_period_id) {
            return SQLserverDB_readonly.commissions_participation.Where(cp => cp.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.commissions_participation> GetCommissionsParticipation(int performance_target_metrics_id) {
            return SQLserverDB_readonly.commissions_participation.Where(cp => cp.performance_target_metrics_id == performance_target_metrics_id).ToList();
        }

        public List<commissions_accelerator_source_types> GetCommissionsAcceleratorSourceTypes() {
            return SQLserverDB_readonly.commissions_accelerator_source_types.ToList();
        }

        public List<Data.commissions_accelerator> GetCommissionsAcceleratorForPayPeriod(int pay_period_id) {
            return SQLserverDB_readonly.commissions_accelerator.Where(cm => cm.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.commissions_accelerator> GetCommissionsAccelerator(int performance_target_metrics_id) {
            return SQLserverDB_readonly.commissions_accelerator.Where(cm => cm.metric_source == 1 && cm.metric_id == performance_target_metrics_id).ToList();
        }

        public List<Data.commissions_accelerator_values> GetCommissionsAcceleratorValues(int commissions_accelerator_id) {
            return SQLserverDB_readonly.commissions_accelerator_values.Where(cm => cm.commissions_accelerator_id == commissions_accelerator_id).ToList();
        }

        public List<Data.performance_target_metrics> GetPerformanceTargetMetrics(int pay_period_id) {
            return SQLserverDB_readonly.performance_target_metrics.Where(pm => pm.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.PerformanceTarget> GetPerformanceTargets(int pay_period_id) {
            return SQLserverDB_readonly.sp_get_commissions_performance_targets(pay_period_id).Select
                (t => new Data.PerformanceTarget {
                    pay_period_id = t.pay_period_id,
                    performance_target_metric_id = t.performance_target_metric_id,
                    performance_target_id = t.performance_target_id,
                    metric_ID = t.metric_ID,
                    target_value = t.target_value,
                    target_level_id = t.target_level_id,
                    store_id = t.store_id.HasValue ? t.store_id.Value : 0,
                    store_name = t.store_name,
                    district_id = t.district_id.HasValue ? t.district_id.Value : 0,
                    district_name = t.district_name,
                    region_id = t.region_id.HasValue ? t.region_id.Value : 0,
                    region_name = t.region_name,
                    channel_id = t.channel_id.HasValue ? t.channel_id.Value : 0,
                    channel_name = t.channel_name,
                    employee_id = t.employee_id.HasValue ? t.employee_id.Value : 0,
                    employee_name = t.employee_name,
                    roll_up = t.roll_up,
                    used_for_min_check = t.used_for_min_check,
                    min_check_metric_ID = t.min_check_metric_ID.HasValue ? t.min_check_metric_ID.Value : 0,
                    for_general_targets = t.for_general_targets,
                    for_employee_targets = t.for_employee_targets
                }).ToList();
        }

        public List<Data.PerformanceTierDataItem> GetPerformanceTiersData(int pay_period_id) {
            return SQLserverDB_readonly.sp_get_commissions_performance_tiers(pay_period_id).Select
                (t => new Data.PerformanceTierDataItem {
                    performance_tiers_id = t.performance_tiers_id,
                    pay_period_id = t.pay_period_id,
                    performance_target_level = t.performance_target_level,
                    tier_code = t.tier_code,
                    tier_description = t.tier_description,
                    performance_tier_items_id = t.performance_tiers_items_id,
                    item_id = t.item_id,
                    gross_profit_margin = t.gross_profit_margin,
                    commission_cap = t.commission_cap
                }).ToList();
        }

        public List<Data.PerformanceTierDataItem> GetPerformanceTiersDataForID(Globals.performance_target_level target_level, int pay_period_id, int entity_id) {
            return SQLserverDB_readonly.sp_get_commissions_performance_tiers(pay_period_id).Where(cpt => cpt.performance_target_level == (int)target_level && cpt.item_id == entity_id).Select
                (t => new Data.PerformanceTierDataItem {
                    performance_tiers_id = t.performance_tiers_id,
                    pay_period_id = t.pay_period_id,
                    performance_target_level = t.performance_target_level,
                    tier_code = t.tier_code,
                    tier_description = t.tier_description,
                    performance_tier_items_id = t.performance_tiers_items_id,
                    item_id = t.item_id,
                    gross_profit_margin = t.gross_profit_margin,
                    commission_cap = t.commission_cap
                }).ToList();
        }

        public List<Data.performance_tiers> GetPerformanceTiers(int pay_period_id) {
            return SQLserverDB_readonly.performance_tiers.Where(pt => pt.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.commissions_tier_assignment> GetTierAssignments(int pay_period_id) {
            return SQLserverDB_readonly.commissions_tier_assignment.Where(ct => ct.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.commissions_target_tiers> GetPayoutTiers(int pay_period_id) {
            return SQLserverDB_readonly.commissions_target_tiers.Where(ctt => ctt.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.sp_get_commissions_custom_dataResult> GetCustomCommissionsData(int pay_period_id) {
            return SQLserverDB_readonly.sp_get_commissions_custom_data(pay_period_id).ToList();
        }

        public List<Data.sp_get_commissions_RQ4_employee_storesResult> GetRQ4EmployeeStores() {
            return SQLserverDB_readonly.sp_get_commissions_RQ4_employee_stores().ToList();
        }

        public List<Data.BSRitems> GetBSRtestdata() {
            List<Data.BSRitems> gotdata = new List<Data.BSRitems>();
            Data.BSRitems test1 = new Data.BSRitems();
            test1.id_field = 1;
            test1.metric_id = (int)Globals.BSR_metrics.apb_metric_id;
            test1.total = 106;
            Data.BSRitems test2 = new Data.BSRitems();
            test2.id_field = 1;
            test2.metric_id = 2;
            test2.total = 85;
            Data.BSRitems test3 = new Data.BSRitems();
            test3.id_field = 1;
            test3.metric_id = (int)Globals.BSR_metrics.GP_metric_id;
            test3.total = 1000;

            gotdata.Add(test1);
            gotdata.Add(test2);
            gotdata.Add(test3);
            return gotdata;
        }

        public List<Data.BSRitems> GetBSRdata(Data.commissions_pay_periods ppd, Globals.BSR_report_level report_level) {
            return SQLserverDB_readonly.sp_get_metrics_data_cached(ppd.start_date, ppd.end_date, (int)report_level, -1, -1, -1, -1).Select
                (t => new Data.BSRitems {
                    id_field = (t.ID_field.HasValue) ? t.ID_field.Value : 0,
                    description_field = t.description_field,
                    channel_id = (t.channel_id.HasValue) ? t.channel_id.Value : 0,
                    channel_name = t.channel_name,
                    region_id = (t.region_id.HasValue) ? t.region_id.Value : 0,
                    region_name = t.region_name,
                    district_id = (t.district_id.HasValue) ? t.district_id.Value : 0,
                    district_name = t.district_name,
                    store_id = (t.store_id.HasValue) ? t.store_id.Value : 0,
                    store_name = t.store_name,
                    metric_id = (t.metric_ID.HasValue) ? t.metric_ID.Value : 0,
                    total = (t.totals.HasValue) ? t.totals.Value : 0,
                    column_header = t.column_header,
                    info_id = (t.info_id.HasValue) ? t.info_id.Value : 0,
                    info_desc = t.info_desc
                }).ToList();
        }

        public List<Data.BSRitems> GetBSRdataFromProc(Data.commissions_pay_periods ppd, Globals.BSR_report_level report_level) {
            List<Data.BSRitems> gotdata = new List<Data.BSRitems>();

            SqlConnection con;

#if DEBUG
            con = new SqlConnection(ConfigurationManager.ConnectionStrings["DB_development"].ConnectionString);
#else
            con = new SqlConnection(ConfigurationManager.ConnectionStrings["DB_readonly"].ConnectionString);
#endif

            try {
                DateTime start = ppd.start_date;
                DateTime end = ppd.end_date;
                int rep_lev = (int)report_level;

                // we use this proc which just pulls data from the BSR cache - not only is this faster, but the cache preserves stores and district locations at the time it was cached so if something moves and we re-calc that month we'll still include it at the correct location
                SqlCommand cmd = new SqlCommand("sp_get_metrics_data_cached", con);

                if (con.State != System.Data.ConnectionState.Open) {
                    con.Open();
                }

                cmd.CommandTimeout = 0;
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@start_date", start);
                cmd.Parameters.AddWithValue("@end_date", end);
                cmd.Parameters.AddWithValue("@report_level", rep_lev.ToString());
                cmd.Parameters.AddWithValue("@region_id", "-1");
                cmd.Parameters.AddWithValue("@district_id", "-1");
                cmd.Parameters.AddWithValue("@store_id", "-1");
                cmd.Parameters.AddWithValue("@employee_id", "-1");

                SqlDataReader dat = cmd.ExecuteReader();

                while (dat.Read()) {
                    Data.BSRitems BSRdataitem = new Data.BSRitems();

                    BSRdataitem.id_field = Convert.ToInt32(dat["id_field"]);
                    BSRdataitem.description_field = Convert.ToString(dat["description_field"]);
                    BSRdataitem.region_id = Convert.ToInt32(dat["region_id"]);
                    BSRdataitem.region_name = Convert.ToString(dat["region_name"]);
                    BSRdataitem.district_id = !dat.IsDBNull(dat.GetOrdinal("district_id")) ? Convert.ToInt32(dat["district_id"]) : -1;
                    BSRdataitem.district_name = !dat.IsDBNull(dat.GetOrdinal("district_name")) ? Convert.ToString(dat["district_name"]) : "";
                    BSRdataitem.store_id = !dat.IsDBNull(dat.GetOrdinal("store_id")) ? Convert.ToInt32(dat["store_id"]) : -1;
                    BSRdataitem.store_name = !dat.IsDBNull(dat.GetOrdinal("store_name")) ? Convert.ToString(dat["store_name"]) : "";
                    BSRdataitem.metric_id = Convert.ToInt32(dat["metric_id"]);
                    BSRdataitem.total = Convert.ToDecimal(dat["totals"]);
                    BSRdataitem.column_header = Convert.ToString(dat["column_header"]);

                    gotdata.Add(BSRdataitem);
                }
            }

            finally {
                if (con.State != System.Data.ConnectionState.Closed) {
                    con.Close();
                }
            }

            return gotdata;
        }

        public List<Data.InvoiceItems> GetRQ4Invoices(Data.commissions_pay_periods ppd) {
            // get all invoices for pay period
            return SQLserverDB_readonly.sp_get_commissions_invoices(ppd.start_date, ppd.end_date, "").Select
                        (i => new Data.InvoiceItems {
                            ChannelID = i.ChannelID,
                            RegionID = i.RegionID,
                            DistrictID = i.DistrictID,
                            StoreID = i.StoreID2,
                            EmployeeID = i.EmployeeID1,
                            SaleInvoiceID = i.SaleInvoiceID,
                            CategoryNumber = i.CategoryNumber,
                            Comments = i.Comments,
                            OriginalSaleInvoiceID = i.OriginalSaleInvoiceID,
                            Sku = i.ProductIdentifier,
                            UnitPrice = i.UnitPrice,
                            UnitCost = i.UnitCost,
                            Quantity = i.Quantity,
                            DateCreated = i.DateCreated,
                            GlobalProductID = i.GlobalProductID,
                            InvoiceIDByStore = i.InvoiceIDByStore,
                            SerialNumber = i.SerialNumber,
                        })
                    .ToList();
        }

        public List<Data.InvoiceItems> GetRQ4InvoicesForDate(DateTime start_date, DateTime end_date) {
            return SQLserverDB_readonly.sp_get_commissions_invoices(start_date, end_date, "").Select
                        (i => new Data.InvoiceItems {
                            ChannelID = i.ChannelID,
                            RegionID = i.RegionID,
                            DistrictID = i.DistrictID,
                            StoreID = i.StoreID2,
                            EmployeeID = i.EmployeeID1,
                            SaleInvoiceID = i.SaleInvoiceID,
                            CategoryNumber = i.CategoryNumber,
                            Comments = i.Comments,
                            OriginalSaleInvoiceID = i.OriginalSaleInvoiceID,
                            Sku = i.ProductIdentifier,
                            UnitPrice = i.UnitPrice,
                            UnitCost = i.UnitCost,
                            Quantity = i.Quantity,
                            DateCreated = i.DateCreated,
                            GlobalProductID = i.GlobalProductID,
                            InvoiceIDByStore = i.InvoiceIDByStore,
                            SerialNumber = i.SerialNumber,
                        })
                    .ToList();
        }

        public List<Data.sp_get_commissions_manual_adjustmentsResult> GetManualAdjustments(DateTime start_date) {
            return SQLserverDB_readonly.sp_get_commissions_manual_adjustments(start_date).ToList();
        }

        public List<Data.sp_get_commissions_couponsResult> GetCoupons() {
            return SQLserverDB_readonly.sp_get_commissions_coupons().ToList();
        }

        public List<Data.commissions_payouts_sku> GetPayoutSkus(DateTime start_date, DateTime end_date) {
            // load all active payouts by SKU
            return SQLserverDB_readonly.commissions_payouts_sku.Where(p => p.end_date.Date >= start_date && p.start_date <= end_date).ToList();
        }

        public List<Data.commissions_payouts_category> GetPayoutCategories(DateTime rundate, int pay_period_id) {
            // load all active payouts by Category
            return SQLserverDB_readonly.commissions_payouts_category.Where(p => p.pay_period_id == pay_period_id && ((!p.start_date.HasValue && !p.end_date.HasValue) || (rundate <= p.end_date && rundate >= p.start_date))).ToList();
        }

        public List<Data.commissions_payouts_category_values> GetPayoutCategoryValues(int payout_category_id) {
            return SQLserverDB_readonly.commissions_payouts_category_values.Where(p => p.commissions_payouts_category_id == payout_category_id).ToList();
        }

        public List<Data.commissions_payouts_minimum> GetPayoutMinimums(DateTime start_date, DateTime end_date) {
            // load all active payouts by SKU
            return SQLserverDB_readonly.commissions_payouts_minimum.Where(p => p.end_date.Date >= start_date && p.start_date <= end_date).ToList();
        }

        public List<Data.sp_get_commissions_MSO_dataResult> GetMSOExtractData(DateTime start_date, DateTime end_date, bool completed_installs_only) {
            return SQLserverDB_readonly.sp_get_commissions_MSO_data(start_date, end_date, completed_installs_only).ToList();
        }

        public Data.commissions_sales_consultant GetSalesConsultantCommissions(int pay_period_id, int for_id, bool autocreate = false) {
            commissions_sales_consultant sc = SQLserverDB_live.commissions_sales_consultant.Where(s => s.pay_period_id == pay_period_id && s.employee_id == for_id).FirstOrDefault();

            if (sc == null && autocreate == true) {
                sc = new Data.commissions_sales_consultant();
                sc.pay_period_id = pay_period_id;
                sc.employee_id = for_id;
                sc.region_id = 0;
                sc.region_name = "";
                sc.district_id = 0;
                sc.district_name = "";
                sc.store_id = 0;
                sc.store_name = "";
                sc.base_gross_profit = 0;
                sc.gross_profit_margin_percent = 0;
                sc.gross_profit_margin = 0;
                sc.performance_total = 0;
                sc.payout_total = 0;
                sc.commission_total = 0;
                sc.manual_adjustment = 0;
                sc.last_updated = DateTime.Now;

                SQLserverDB_live.commissions_sales_consultant.InsertOnSubmit(sc);
                SQLserverDB_live.SubmitChanges();
            }

            return sc;
        }

        public Data.commissions_store_leader GetStoreLeaderCommissions(int pay_period_id, int for_id, bool autocreate = false) {
            commissions_store_leader sl = SQLserverDB_live.commissions_store_leader.Where(s => s.pay_period_id == pay_period_id && s.store_id == for_id).FirstOrDefault();

            if (sl == null && autocreate == true) {
                sl = new Data.commissions_store_leader();
                sl.pay_period_id = pay_period_id;
                sl.region_id = 0;
                sl.region_name = "";
                sl.district_id = 0;
                sl.district_name = "";
                sl.store_id = for_id;
                sl.store_name = "";
                sl.employee_id = 0;
                sl.base_gross_profit = 0;
                sl.gross_profit_margin_percent = 0;
                sl.gross_profit_margin = 0;
                sl.performance_total = 0;
                sl.payout_total = 0;
                sl.commission_total = 0;
                sl.manual_adjustment = 0;
                sl.last_updated = DateTime.Now;

                SQLserverDB_live.commissions_store_leader.InsertOnSubmit(sl);
                SQLserverDB_live.SubmitChanges();
            }

            return sl;
        }

        public Data.commissions_district_leader GetDistrictLeaderCommissions(int pay_period_id, int for_id, bool autocreate = false) {
            commissions_district_leader dl = SQLserverDB_live.commissions_district_leader.Where(s => s.pay_period_id == pay_period_id && s.district_id == for_id).FirstOrDefault();

            if (dl == null && autocreate == true) {
                dl = new Data.commissions_district_leader();
                dl.pay_period_id = pay_period_id;
                dl.region_id = 0;
                dl.region_name = "";
                dl.district_id = for_id;
                dl.district_name = "";
                dl.employee_id = 0;
                dl.base_gross_profit = 0;
                dl.gross_profit_margin_percent = 0;
                dl.gross_profit_margin = 0;
                dl.participation_total = 0;
                dl.commission_total = 0;
                dl.manual_adjustment = 0;
                dl.last_updated = DateTime.Now;

                SQLserverDB_live.commissions_district_leader.InsertOnSubmit(dl);
                SQLserverDB_live.SubmitChanges();
            }

            return dl;
        }

        public Data.commissions_region_leader GetRegionLeaderCommissions(int pay_period_id, int for_id, bool autocreate = false) {
            commissions_region_leader rl = SQLserverDB_live.commissions_region_leader.Where(s => s.pay_period_id == pay_period_id && s.region_id == for_id).FirstOrDefault();

            if (rl == null && autocreate == true) {
                rl = new Data.commissions_region_leader();
                rl.pay_period_id = pay_period_id;
                rl.region_id = for_id;
                rl.region_name = "";
                rl.employee_id = 0;
                rl.base_gross_profit = 0;
                rl.gross_profit_margin_percent = 0;
                rl.gross_profit_margin = 0;
                rl.participation_total = 0;
                rl.commission_total = 0;
                rl.manual_adjustment = 0;
                rl.last_updated = DateTime.Now;

                SQLserverDB_live.commissions_region_leader.InsertOnSubmit(rl);
                SQLserverDB_live.SubmitChanges();
            }


            return rl;
        }

        public Data.commissions_channel_leader GetChannelLeaderCommissions(int pay_period_id, int for_id, bool autocreate = false) {
            commissions_channel_leader cl = SQLserverDB_live.commissions_channel_leader.Where(s => s.pay_period_id == pay_period_id && s.channel_id == for_id).FirstOrDefault();

            if (cl == null && autocreate == true) {
                cl = new Data.commissions_channel_leader();
                cl.pay_period_id = pay_period_id;
                cl.channel_id = for_id;
                cl.channel_name = "";
                cl.employee_id = 0;
                cl.base_gross_profit = 0;
                cl.gross_profit_margin_percent = 0;
                cl.gross_profit_margin = 0;
                cl.participation_total = 0;
                cl.commission_total = 0;
                cl.manual_adjustment = 0;
                cl.last_updated = DateTime.Now;

                SQLserverDB_live.commissions_channel_leader.InsertOnSubmit(cl);
                SQLserverDB_live.SubmitChanges();
            }


            return cl;
        }
        public List<Data.commissions_compplan_log> GetCompPlanLog(bool clear_old_messages) {
            if (clear_old_messages) {
                SQLserverDB_live.commissions_compplan_log.DeleteAllOnSubmit(SQLserverDB_live.commissions_compplan_log.Where(l => l.created_date <= DateTime.Now.AddHours(-12)).ToList());
                SQLserverDB_live.SubmitChanges();
            }

            return SQLserverDB_live.commissions_compplan_log.ToList();
        }

        public List<Data.commissions_kpi> GetKpi(int pay_period_id) {
            return SQLserverDB_readonly.commissions_kpi.Where(k => k.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.commissions_kpi_groups> GetKpiGroups(int commissions_kpi_id) {
            return SQLserverDB_readonly.commissions_kpi_groups.Where(k => k.commissions_kpi_id == commissions_kpi_id).ToList();
        }

        public List<Data.commissions_kpi_goal_points> GetKpiPoints(int commissions_kpi_id_group_id) {
            return SQLserverDB_readonly.commissions_kpi_goal_points.Where(k => k.commissions_kpi_groups_id == commissions_kpi_id_group_id).ToList();
        }

        public List<Data.commissions_kpi_points_reward_values> GetKpiValues(int pay_period_id) {
            return SQLserverDB_readonly.commissions_kpi_points_reward_values.Where(k => k.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.sp_get_commissions_KPI_dataResult> GetKpiData(int pay_period_id) {
            return SQLserverDB_readonly.sp_get_commissions_KPI_data(pay_period_id).ToList();
        }

        public List<Data.commissions_target_calculations> GetCalculatedTargets(int pay_period_id) {
            return SQLserverDB_readonly.commissions_target_calculations.Where(ct => ct.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.commissions_target_payout> GetTargetPayouts(int pay_period_id) {
            return SQLserverDB_readonly.commissions_target_payouts.Where(tp => tp.pay_period_id == pay_period_id).ToList();
        }

        public List<Data.commissions_target_payout_values> GetTargetPayoutValues(int commissions_target_payout_id) {
            return SQLserverDB_readonly.commissions_target_payout_values.Where(tpv => tpv.commissions_target_payout_id == commissions_target_payout_id).ToList();
        }

        public List<Data.commissions_target_payout_team_bonus> GetTargetPayoutTeamBonus(int pay_period_id) {
            return SQLserverDB_readonly.commissions_target_payout_team_bonus.Where(tpv => tpv.pay_period_id == pay_period_id).ToList();
        }

        public List<commissions_target_payout_team_bonus_values> GetTargetPayoutTeamBonusValues() {
            return SQLserverDB_readonly.commissions_target_payout_team_bonus_values.ToList();
        }

        public List<Data.rq4_iQmetrix_EmployeeGroup> GetRqCommissionsGroups() {
            return SQLserverDB_readonly.rq4_iQmetrix_EmployeeGroups.ToList();
        }

        #endregion

        #region Data Saving Routines

        public bool SaveLastRunDate(Globals.performance_target_level target_level, int pay_period_id, DateTime rundate) {
            Data.commissions_configuration got_config = SQLserverDB_live.commissions_configuration.FirstOrDefault(p => p.performance_target_level == (int)target_level && p.pay_period_id == pay_period_id);

            // auto-roll the config into a new pay period using values from the previous pay period
            if (got_config != null) {
                got_config.last_run_date = rundate;
                SQLserverDB_live.SubmitChanges();
            }

            return true;
        }

        public bool SaveCompPlanLog(Globals.comp_plan_log_message_type msg_type, List<string> msgs) {
            bool res = false;

            if (msgs != null && msgs.Count > 0) {
                msgs.ForEach(m => {
                    Data.commissions_compplan_log lg = new commissions_compplan_log();
                    lg.created_date = DateTime.Now;
                    lg.message_type = (int)msg_type;
                    lg.message_text = m;

                    SQLserverDB_live.commissions_compplan_log.InsertOnSubmit(lg);
                });

                SQLserverDB_live.SubmitChanges();
                res = true;
            }

            return res;
        }

        // saves header row containing totals only
        public bool SaveCommissionsValuesHeader(Globals.performance_target_level target_level, Data.CommissionValues cv) {
            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(cv.pay_period_id, cv.entity_id);

                    if (sc == null) {
                        sc = new Data.commissions_sales_consultant();
                        sc.pay_period_id = cv.pay_period_id;
                        sc.employee_id = cv.entity_id;

                        SQLserverDB_live.commissions_sales_consultant.InsertOnSubmit(sc);
                    }

                    sc.region_id = cv.region_id;
                    sc.region_name = cv.region_name;
                    sc.district_id = cv.district_id;
                    sc.district_name = cv.district_name;
                    sc.store_id = cv.store_id;
                    sc.store_name = cv.store_name;
                    sc.base_gross_profit = cv.base_gross_profit;
                    sc.commission_gross_profit = cv.commission_gross_profit;
                    sc.gross_profit_margin_percent = cv.gross_profit_margin_percent;
                    sc.gross_profit_margin = cv.gross_profit_margin;
                    sc.performance_total = cv.performance_total;
                    sc.payout_total = cv.payout_total;
                    sc.commission_total = cv.commission_total;
                    sc.manual_adjustment = cv.manual_adjustment;
                    sc.min_boxes_total = cv.min_boxes_total;
                    sc.current_boxes_total = cv.current_boxes_total;
                    sc.coupons = cv.coupons;
                    sc.kpi_total = cv.kpi_total;
                    sc.last_updated = DateTime.Now;

                    return true;

                case Globals.performance_target_level.Store:
                    Data.commissions_store_leader sl = GetStoreLeaderCommissions(cv.pay_period_id, cv.entity_id);

                    if (sl == null) {
                        sl = new Data.commissions_store_leader();
                        sl.pay_period_id = cv.pay_period_id;
                        sl.store_id = cv.entity_id;

                        SQLserverDB_live.commissions_store_leader.InsertOnSubmit(sl);
                    }

                    sl.region_id = cv.region_id;
                    sl.region_name = cv.region_name;
                    sl.district_id = cv.district_id;
                    sl.district_name = cv.district_name;
                    sl.employee_id = cv.employee_id;
                    sl.store_name = cv.store_name;
                    sl.base_gross_profit = cv.base_gross_profit;
                    sl.commission_gross_profit = cv.commission_gross_profit;
                    sl.gross_profit_margin_percent = cv.gross_profit_margin_percent;
                    sl.gross_profit_margin = cv.gross_profit_margin;
                    sl.performance_total = cv.performance_total;
                    sl.payout_total = cv.payout_total;
                    sl.commission_total = cv.commission_total;
                    sl.manual_adjustment = cv.manual_adjustment;
                    sl.coupons = cv.coupons;
                    sl.kpi_total = cv.kpi_total;
                    sl.last_updated = DateTime.Now;

                    return true;

                case Globals.performance_target_level.District:
                    Data.commissions_district_leader dl = GetDistrictLeaderCommissions(cv.pay_period_id, cv.entity_id);

                    if (dl == null) {
                        dl = new Data.commissions_district_leader();
                        dl.pay_period_id = cv.pay_period_id;
                        dl.district_id = cv.entity_id;

                        SQLserverDB_live.commissions_district_leader.InsertOnSubmit(dl);
                    }

                    dl.region_id = cv.region_id;
                    dl.region_name = cv.region_name;
                    dl.district_name = cv.district_name;
                    dl.employee_id = cv.employee_id;
                    dl.base_gross_profit = cv.base_gross_profit;
                    dl.commission_gross_profit = cv.commission_gross_profit;
                    dl.gross_profit_margin_percent = cv.gross_profit_margin_percent;
                    dl.gross_profit_margin = cv.gross_profit_margin;
                    dl.participation_total = cv.participation_total;
                    dl.commission_total = cv.commission_total;
                    dl.manual_adjustment = cv.manual_adjustment;
                    dl.coupons = cv.coupons;
                    dl.kpi_total = cv.kpi_total;
                    dl.last_updated = DateTime.Now;

                    return true;

                case Globals.performance_target_level.Region:
                    Data.commissions_region_leader rl = GetRegionLeaderCommissions(cv.pay_period_id, cv.entity_id);

                    if (rl == null) {
                        rl = new Data.commissions_region_leader();
                        rl.pay_period_id = cv.pay_period_id;
                        rl.region_id = cv.entity_id;

                        SQLserverDB_live.commissions_region_leader.InsertOnSubmit(rl);
                    }

                    rl.region_name = cv.region_name;
                    rl.employee_id = cv.employee_id;
                    rl.base_gross_profit = cv.base_gross_profit;
                    rl.commission_gross_profit = cv.commission_gross_profit;
                    rl.gross_profit_margin_percent = cv.gross_profit_margin_percent;
                    rl.gross_profit_margin = cv.gross_profit_margin;
                    rl.participation_total = cv.participation_total;
                    rl.commission_total = cv.commission_total;
                    rl.manual_adjustment = cv.manual_adjustment;
                    rl.coupons = cv.coupons;
                    rl.kpi_total = cv.kpi_total;
                    rl.last_updated = DateTime.Now;

                    return true;

                case Globals.performance_target_level.Channel:
                    Data.commissions_channel_leader ch = GetChannelLeaderCommissions(cv.pay_period_id, cv.entity_id);

                    if (ch == null) {
                        ch = new Data.commissions_channel_leader();
                        ch.pay_period_id = cv.pay_period_id;
                        ch.channel_id = cv.entity_id;

                        SQLserverDB_live.commissions_channel_leader.InsertOnSubmit(ch);
                    }

                    ch.channel_name = cv.channel_name;
                    ch.employee_id = cv.employee_id;
                    ch.base_gross_profit = cv.base_gross_profit;
                    ch.commission_gross_profit = cv.commission_gross_profit;
                    ch.gross_profit_margin_percent = cv.gross_profit_margin_percent;
                    ch.gross_profit_margin = cv.gross_profit_margin;
                    ch.participation_total = cv.participation_total;
                    ch.commission_total = cv.commission_total;
                    ch.manual_adjustment = cv.manual_adjustment;
                    ch.coupons = cv.coupons;
                    ch.kpi_total = cv.kpi_total;
                    ch.last_updated = DateTime.Now;

                    return true;
                default:
                    return false;
            }
        }

        // saves details row containing breakdown of values only - each method handles data specific to each module
        public bool SavePerformanceCommissionsDetails(Globals.performance_target_level target_level, CommissionValueDetails cvd) {
            // This handles saving all performance values
            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_sales_consultant_detail sd = SQLserverDB_live.commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && s.commissions_type_id == cvd.commissions_type_id && s.performance_target_id == cvd.performance_target_id).FirstOrDefault();

                    if (sd == null) {
                        sd = new Data.commissions_sales_consultant_detail();
                        sd.commissions_sales_consultant_id = sc.commissions_sales_consultant_id;
                        sd.commissions_type_id = cvd.commissions_type_id;
                        sd.performance_target_id = cvd.performance_target_id;

                        // these aren't used here but set some defaults
                        sd.payout_id = cvd.payout_id;
                        sd.payout_category_id = cvd.payout_category;
                        sd.payout_sku = cvd.payout_sku;
                        sd.payout_amount = cvd.payout_amount;
                        sd.payout_count = cvd.payout_count;
                        sd.payout_total = cvd.payout_total;
                        sd.kpi_id = cvd.kpi_id;
                        sd.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_sales_consultant_detail.InsertOnSubmit(sd);
                    }

                    sd.min_metric_id = cvd.min_metric_id;
                    sd.min_metric_value = cvd.min_metric_value;
                    sd.metric_id = cvd.metric_id;
                    sd.metric_value = cvd.metric_value;
                    sd.target_value = cvd.target_value;
                    sd.percent_to_target = cvd.percent_to_target;
                    sd.accelerator_value = cvd.accelerator_value;
                    sd.performance_total = cvd.performance_total;
                    sd.payout_amount = cvd.payout_amount;

                    return true;

                case Globals.performance_target_level.Store:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_store_leader sl = GetStoreLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_store_leader_detail sld = SQLserverDB_live.commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.performance_target_id == cvd.performance_target_id).FirstOrDefault();

                    if (sld == null) {
                        sld = new Data.commissions_store_leader_detail();
                        sld.commissions_store_leader_id = sl.commissions_store_leader_id;
                        sld.commissions_type_id = cvd.commissions_type_id;
                        sld.performance_target_id = cvd.performance_target_id;

                        // these aren't used here but set some defaults
                        sld.payout_id = cvd.payout_id;
                        sld.payout_category_id = cvd.payout_category;
                        sld.payout_sku = cvd.payout_sku;
                        sld.payout_amount = cvd.payout_amount;
                        sld.payout_count = cvd.payout_count;
                        sld.payout_total = cvd.payout_total;
                        sld.kpi_id = cvd.kpi_id;
                        sld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_store_leader_detail.InsertOnSubmit(sld);
                    }

                    sld.min_metric_id = cvd.min_metric_id;
                    sld.min_metric_value = cvd.min_metric_value;
                    sld.metric_id = cvd.metric_id;
                    sld.metric_value = cvd.metric_value;
                    sld.target_value = cvd.target_value;
                    sld.percent_to_target = cvd.percent_to_target;
                    sld.accelerator_value = cvd.accelerator_value;
                    sld.performance_total = cvd.performance_total;
                    sld.payout_amount = cvd.payout_amount;

                    return true;

                // so far employee and store don't have participation indexes
                case Globals.performance_target_level.District:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_district_leader dl = GetDistrictLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_district_leader_detail dld = SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.performance_target_id == cvd.performance_target_id).FirstOrDefault();

                    if (dld == null) {
                        dld = new Data.commissions_district_leader_detail();
                        dld.commissions_district_leader_id = dl.commissions_district_leader_id;
                        dld.commissions_type_id = cvd.commissions_type_id;
                        dld.performance_target_id = cvd.performance_target_id;

                        // these aren't used here but set some defaults
                        dld.participation_index_value = cvd.participation_index_value;
                        dld.participation_location_count = cvd.participation_location_count;
                        dld.participation_target_count = cvd.participation_target_count;
                        dld.payout_id = cvd.payout_id;
                        dld.payout_category_id = cvd.payout_category;
                        dld.payout_sku = cvd.payout_sku;
                        dld.payout_count = cvd.payout_count;
                        dld.payout_amount = cvd.payout_amount;
                        dld.payout_total = cvd.payout_total;
                        dld.kpi_id = cvd.kpi_id;
                        dld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_district_leader_detail.InsertOnSubmit(dld);
                    }

                    dld.min_metric_id = cvd.min_metric_id;
                    dld.min_metric_value = cvd.min_metric_value;
                    dld.metric_id = cvd.metric_id;
                    dld.metric_value = cvd.metric_value;
                    dld.target_value = cvd.target_value;
                    dld.percent_to_target = cvd.percent_to_target;
                    dld.accelerator_value = cvd.accelerator_value;
                    dld.performance_total = cvd.performance_total;
                    dld.payout_amount = cvd.payout_amount;

                    return true;

                case Globals.performance_target_level.Region:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_region_leader rl = GetRegionLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_region_leader_detail rld = SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.performance_target_id == cvd.performance_target_id).FirstOrDefault();

                    if (rld == null) {
                        rld = new Data.commissions_region_leader_detail();
                        rld.commissions_region_leader_id = rl.commissions_region_leader_id;
                        rld.commissions_type_id = cvd.commissions_type_id;
                        rld.performance_target_id = cvd.performance_target_id;

                        // these aren't used here but set some defaults
                        rld.participation_index_value = cvd.participation_index_value;
                        rld.participation_location_count = cvd.participation_location_count;
                        rld.participation_target_count = cvd.participation_target_count;
                        rld.payout_id = cvd.payout_id;
                        rld.payout_category_id = cvd.payout_category;
                        rld.payout_sku = cvd.payout_sku;
                        rld.payout_count = cvd.payout_count;
                        rld.payout_amount = cvd.payout_amount;
                        rld.payout_total = cvd.payout_total;
                        rld.kpi_id = cvd.kpi_id;
                        rld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_region_leader_detail.InsertOnSubmit(rld);
                    }

                    rld.min_metric_id = cvd.min_metric_id;
                    rld.min_metric_value = cvd.min_metric_value;
                    rld.metric_id = cvd.metric_id;
                    rld.metric_value = cvd.metric_value;
                    rld.target_value = cvd.target_value;
                    rld.percent_to_target = cvd.percent_to_target;
                    rld.accelerator_value = cvd.accelerator_value;
                    rld.performance_total = cvd.performance_total;
                    rld.payout_amount = cvd.payout_amount;

                    return true;

                case Globals.performance_target_level.Channel:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_channel_leader cl = GetChannelLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_channel_leader_detail cld = SQLserverDB_live.commissions_channel_leader_detail.Where(s => s.commissions_channel_leader_id == cl.commissions_channel_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.performance_target_id == cvd.performance_target_id).FirstOrDefault();

                    if (cld == null) {
                        cld = new Data.commissions_channel_leader_detail();
                        cld.commissions_channel_leader_id = cl.commissions_channel_leader_id;
                        cld.commissions_type_id = cvd.commissions_type_id;
                        cld.performance_target_id = cvd.performance_target_id;

                        // these aren't used here but set some defaults
                        cld.participation_index_value = cvd.participation_index_value;
                        cld.participation_location_count = cvd.participation_location_count;
                        cld.participation_target_count = cvd.participation_target_count;
                        cld.payout_id = cvd.payout_id;
                        cld.payout_category_id = cvd.payout_category;
                        cld.payout_sku = cvd.payout_sku;
                        cld.payout_count = cvd.payout_count;
                        cld.payout_amount = cvd.payout_amount;
                        cld.payout_total = cvd.payout_total;
                        cld.kpi_id = cvd.kpi_id;
                        cld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_channel_leader_detail.InsertOnSubmit(cld);
                    }

                    cld.min_metric_id = cvd.min_metric_id;
                    cld.min_metric_value = cvd.min_metric_value;
                    cld.metric_id = cvd.metric_id;
                    cld.metric_value = cvd.metric_value;
                    cld.target_value = cvd.target_value;
                    cld.percent_to_target = cvd.percent_to_target;
                    cld.accelerator_value = cvd.accelerator_value;
                    cld.performance_total = cvd.performance_total;
                    cld.payout_amount = cvd.payout_amount;

                    return true;

                default:
                    return false;
            }
        }

        // saves details row containing breakdown of values only - each method handles data specific to each module
        public bool SaveParticipationCommissionsDetails(Globals.performance_target_level target_level, CommissionValueDetails cvd) {
            // This handles saving all performance values
            switch (target_level) {
                // so far employee and store don't have participation indexes
                case Globals.performance_target_level.Employee:
                    return false;

                case Globals.performance_target_level.Store:
                    return false;

                case Globals.performance_target_level.District:

                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_district_leader dl = GetDistrictLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_district_leader_detail dld = SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == cvd.commissions_type_id).FirstOrDefault();

                    if (dld == null) {
                        dld = new Data.commissions_district_leader_detail();
                        dld.commissions_district_leader_id = dl.commissions_district_leader_id;
                        dld.commissions_type_id = cvd.commissions_type_id;

                        // these aren't used here but set some defaults
                        dld.payout_id = cvd.payout_id;
                        dld.payout_category_id = cvd.payout_category;
                        dld.payout_sku = cvd.payout_sku;
                        dld.payout_count = cvd.payout_count;
                        dld.payout_amount = cvd.payout_amount;
                        dld.payout_total = cvd.payout_total;
                        dld.min_metric_id = cvd.min_metric_id;
                        dld.min_metric_value = cvd.min_metric_value;
                        dld.metric_id = cvd.metric_id;
                        dld.metric_value = cvd.metric_value;
                        dld.target_value = cvd.target_value;
                        dld.percent_to_target = cvd.percent_to_target;
                        dld.accelerator_value = cvd.accelerator_value;
                        dld.performance_total = cvd.performance_total;
                        dld.kpi_id = cvd.kpi_id;
                        dld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_district_leader_detail.InsertOnSubmit(dld);
                    }

                    dld.commissions_participation_id = cvd.commissions_participation_id;
                    dld.participation_index_value = cvd.participation_index_value;
                    dld.participation_location_count = cvd.participation_location_count;
                    dld.participation_target_count = cvd.participation_target_count;
                    dld.performance_target_id = cvd.performance_target_id;

                    return true;

                case Globals.performance_target_level.Region:

                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_region_leader rl = GetRegionLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_region_leader_detail rld = SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == cvd.commissions_type_id).FirstOrDefault();

                    if (rld == null) {
                        rld = new Data.commissions_region_leader_detail();
                        rld.commissions_region_leader_id = rl.commissions_region_leader_id;
                        rld.commissions_type_id = cvd.commissions_type_id;

                        // these aren't used here but set some defaults
                        rld.payout_id = cvd.payout_id;
                        rld.payout_category_id = cvd.payout_category;
                        rld.payout_sku = cvd.payout_sku;
                        rld.payout_count = cvd.payout_count;
                        rld.payout_amount = cvd.payout_amount;
                        rld.payout_total = cvd.payout_total;
                        rld.min_metric_id = cvd.min_metric_id;
                        rld.min_metric_value = cvd.min_metric_value;
                        rld.metric_id = cvd.metric_id;
                        rld.metric_value = cvd.metric_value;
                        rld.target_value = cvd.target_value;
                        rld.percent_to_target = cvd.percent_to_target;
                        rld.accelerator_value = cvd.accelerator_value;
                        rld.performance_total = cvd.performance_total;
                        rld.kpi_id = cvd.kpi_id;
                        rld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_region_leader_detail.InsertOnSubmit(rld);
                    }

                    rld.commissions_participation_id = cvd.commissions_participation_id;
                    rld.participation_index_value = cvd.participation_index_value;
                    rld.participation_location_count = cvd.participation_location_count;
                    rld.participation_target_count = cvd.participation_target_count;
                    rld.performance_target_id = cvd.performance_target_id;

                    return true;

                default:
                    return false;
            }
        }

        public bool SaveSkuPayoutCommissionsDetails(Globals.performance_target_level target_level, CommissionValueDetails cvd) {
            // this handles saving values for all SKU payouts, category is not used here and defaults to empty value
            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_sales_consultant_detail sd = SQLserverDB_live.commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_sku == cvd.payout_sku).FirstOrDefault();

                    if (sd == null) {
                        sd = new Data.commissions_sales_consultant_detail();
                        sd.commissions_sales_consultant_id = sc.commissions_sales_consultant_id;
                        sd.commissions_type_id = cvd.commissions_type_id;
                        sd.payout_id = cvd.payout_id;
                        sd.payout_category_id = cvd.payout_category;
                        sd.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        sd.min_metric_id = cvd.min_metric_id;
                        sd.min_metric_value = cvd.min_metric_value;
                        sd.metric_id = cvd.metric_id;
                        sd.metric_value = cvd.metric_value;
                        sd.target_value = cvd.target_value;
                        sd.percent_to_target = cvd.percent_to_target;
                        sd.accelerator_value = cvd.accelerator_value;
                        sd.performance_total = cvd.performance_total;
                        sd.kpi_id = cvd.kpi_id;
                        sd.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_sales_consultant_detail.InsertOnSubmit(sd);
                    }

                    sd.payout_count = cvd.payout_count;
                    sd.payout_amount = cvd.payout_amount;
                    sd.payout_total = cvd.payout_total;

                    return true;

                case Globals.performance_target_level.Store:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_store_leader sl = GetStoreLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_store_leader_detail sld = SQLserverDB_live.commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_sku == cvd.payout_sku).FirstOrDefault();

                    if (sld == null) {
                        sld = new Data.commissions_store_leader_detail();
                        sld.commissions_store_leader_id = sl.commissions_store_leader_id;
                        sld.commissions_type_id = cvd.commissions_type_id;
                        sld.payout_id = cvd.payout_id;
                        sld.payout_category_id = cvd.payout_category;
                        sld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        sld.min_metric_id = cvd.min_metric_id;
                        sld.min_metric_value = cvd.min_metric_value;
                        sld.metric_id = cvd.metric_id;
                        sld.metric_value = cvd.metric_value;
                        sld.target_value = cvd.target_value;
                        sld.percent_to_target = cvd.percent_to_target;
                        sld.accelerator_value = cvd.accelerator_value;
                        sld.performance_total = cvd.performance_total;
                        sld.kpi_id = cvd.kpi_id;
                        sld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_store_leader_detail.InsertOnSubmit(sld);
                    }

                    sld.payout_count = cvd.payout_count;
                    sld.payout_amount = cvd.payout_amount;
                    sld.payout_total = cvd.payout_total;

                    return true;

                case Globals.performance_target_level.District:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_district_leader dl = GetDistrictLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_district_leader_detail dld = SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_sku == cvd.payout_sku).FirstOrDefault();

                    if (dld == null) {
                        dld = new Data.commissions_district_leader_detail();
                        dld.commissions_district_leader_id = dl.commissions_district_leader_id;
                        dld.commissions_type_id = cvd.commissions_type_id;
                        dld.payout_id = cvd.payout_id;
                        dld.payout_category_id = cvd.payout_category;
                        dld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        dld.min_metric_id = cvd.min_metric_id;
                        dld.min_metric_value = cvd.min_metric_value;
                        dld.metric_id = cvd.metric_id;
                        dld.metric_value = cvd.metric_value;
                        dld.target_value = cvd.target_value;
                        dld.percent_to_target = cvd.percent_to_target;
                        dld.accelerator_value = cvd.accelerator_value;
                        dld.performance_total = cvd.performance_total;
                        dld.commissions_participation_id = cvd.commissions_participation_id;
                        dld.participation_index_value = cvd.participation_index_value;
                        dld.participation_location_count = cvd.participation_location_count;
                        dld.participation_target_count = cvd.participation_target_count;
                        dld.performance_target_id = cvd.performance_target_id;
                        dld.kpi_id = cvd.kpi_id;
                        dld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_district_leader_detail.InsertOnSubmit(dld);
                    }

                    dld.payout_count = cvd.payout_count;
                    dld.payout_amount = cvd.payout_amount;
                    dld.payout_total = cvd.payout_total;

                    return true;

                case Globals.performance_target_level.Region:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_region_leader rl = GetRegionLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_region_leader_detail rld = SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_sku == cvd.payout_sku).FirstOrDefault();

                    if (rld == null) {
                        rld = new Data.commissions_region_leader_detail();
                        rld.commissions_region_leader_id = rl.commissions_region_leader_id;
                        rld.commissions_type_id = cvd.commissions_type_id;
                        rld.payout_id = cvd.payout_id;
                        rld.payout_category_id = cvd.payout_category;
                        rld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        rld.min_metric_id = cvd.min_metric_id;
                        rld.min_metric_value = cvd.min_metric_value;
                        rld.metric_id = cvd.metric_id;
                        rld.metric_value = cvd.metric_value;
                        rld.target_value = cvd.target_value;
                        rld.percent_to_target = cvd.percent_to_target;
                        rld.accelerator_value = cvd.accelerator_value;
                        rld.performance_total = cvd.performance_total;
                        rld.commissions_participation_id = cvd.commissions_participation_id;
                        rld.participation_index_value = cvd.participation_index_value;
                        rld.participation_location_count = cvd.participation_location_count;
                        rld.participation_target_count = cvd.participation_target_count;
                        rld.performance_target_id = cvd.performance_target_id;
                        rld.kpi_id = cvd.kpi_id;
                        rld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_region_leader_detail.InsertOnSubmit(rld);
                    }

                    rld.payout_count = cvd.payout_count;
                    rld.payout_amount = cvd.payout_amount;
                    rld.payout_total = cvd.payout_total;

                    return true;

                case Globals.performance_target_level.Channel:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_channel_leader cl = GetChannelLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_channel_leader_detail cld = SQLserverDB_live.commissions_channel_leader_detail.Where(s => s.commissions_channel_leader_id == cl.commissions_channel_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_sku == cvd.payout_sku).FirstOrDefault();

                    if (cld == null) {
                        cld = new Data.commissions_channel_leader_detail();
                        cld.commissions_channel_leader_id = cl.commissions_channel_leader_id;
                        cld.commissions_type_id = cvd.commissions_type_id;
                        cld.payout_id = cvd.payout_id;
                        cld.payout_category_id = cvd.payout_category;
                        cld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        cld.min_metric_id = cvd.min_metric_id;
                        cld.min_metric_value = cvd.min_metric_value;
                        cld.metric_id = cvd.metric_id;
                        cld.metric_value = cvd.metric_value;
                        cld.target_value = cvd.target_value;
                        cld.percent_to_target = cvd.percent_to_target;
                        cld.accelerator_value = cvd.accelerator_value;
                        cld.performance_total = cvd.performance_total;
                        cld.commissions_participation_id = cvd.commissions_participation_id;
                        cld.participation_index_value = cvd.participation_index_value;
                        cld.participation_location_count = cvd.participation_location_count;
                        cld.participation_target_count = cvd.participation_target_count;
                        cld.performance_target_id = cvd.performance_target_id;
                        cld.kpi_id = cvd.kpi_id;
                        cld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_channel_leader_detail.InsertOnSubmit(cld);
                    }

                    cld.payout_count = cvd.payout_count;
                    cld.payout_amount = cvd.payout_amount;
                    cld.payout_total = cvd.payout_total;

                    return true;

                default:
                    return false;
            }
        }

        public bool SaveCategoryPayoutCommissionsDetails(Globals.performance_target_level target_level, CommissionValueDetails cvd) {
            // this handles saving values for all category-based payouts, SKU is not used here and defaults to empty value
            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_sales_consultant_detail sd = SQLserverDB_live.commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_id == cvd.payout_id && s.payout_category_id == cvd.payout_category && (cvd.payout_location_id <= 0 || s.payout_location_id == cvd.payout_location_id)).FirstOrDefault();

                    if (sd == null) {
                        sd = new Data.commissions_sales_consultant_detail();
                        sd.commissions_sales_consultant_id = sc.commissions_sales_consultant_id;
                        sd.commissions_type_id = cvd.commissions_type_id;
                        sd.payout_id = cvd.payout_id;
                        sd.payout_category_id = cvd.payout_category;

                        // these aren't used here but set some defaults
                        sd.payout_sku = cvd.payout_sku;
                        sd.kpi_id = cvd.kpi_id;
                        sd.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_sales_consultant_detail.InsertOnSubmit(sd);
                    }

                    sd.percent_to_target = cvd.percent_to_target;
                    sd.accelerator_value = cvd.accelerator_value;
                    sd.performance_total = cvd.performance_total;
                    sd.min_metric_id = cvd.min_metric_id;
                    sd.min_metric_value = cvd.min_metric_value;
                    sd.metric_id = cvd.metric_id;
                    sd.metric_value = cvd.metric_value;
                    sd.target_value = cvd.target_value;
                    sd.payout_count = cvd.payout_count;
                    sd.payout_amount = cvd.payout_amount;
                    sd.payout_total = cvd.payout_total;
                    sd.mso_serial_number = cvd.mso_serial_number;
                    if (cvd.payout_location_id > 0)
                        sd.payout_location_id = cvd.payout_location_id;
                    return true;

                case Globals.performance_target_level.Store:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_store_leader sl = GetStoreLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_store_leader_detail sld = SQLserverDB_live.commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_id == cvd.payout_id && s.payout_category_id == cvd.payout_category).FirstOrDefault();

                    if (sld == null) {
                        sld = new Data.commissions_store_leader_detail();
                        sld.commissions_store_leader_id = sl.commissions_store_leader_id;
                        sld.commissions_type_id = cvd.commissions_type_id;
                        sld.payout_id = cvd.payout_id;
                        sld.payout_category_id = cvd.payout_category;

                        // these aren't used here but set some defaults
                        sld.payout_sku = cvd.payout_sku;
                        sld.accelerator_value = cvd.accelerator_value;
                        sld.performance_total = cvd.performance_total;
                        sld.kpi_id = cvd.kpi_id;
                        sld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_store_leader_detail.InsertOnSubmit(sld);
                    }

                    sld.min_metric_id = cvd.min_metric_id;
                    sld.min_metric_value = cvd.min_metric_value;
                    sld.metric_id = cvd.metric_id;
                    sld.metric_value = cvd.metric_value;
                    sld.target_value = cvd.target_value;
                    sld.percent_to_target = cvd.percent_to_target;
                    sld.payout_count = cvd.payout_count;
                    sld.payout_amount = cvd.payout_amount;
                    sld.payout_total = cvd.payout_total;
                    sld.mso_serial_number = cvd.mso_serial_number;

                    return true;

                case Globals.performance_target_level.District:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_district_leader dl = GetDistrictLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_district_leader_detail dld = SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_id == cvd.payout_id && s.payout_category_id == cvd.payout_category).FirstOrDefault();

                    if (dld == null) {
                        dld = new Data.commissions_district_leader_detail();
                        dld.commissions_district_leader_id = dl.commissions_district_leader_id;
                        dld.commissions_type_id = cvd.commissions_type_id;
                        dld.payout_id = cvd.payout_id;
                        dld.payout_category_id = cvd.payout_category;
                        dld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        dld.percent_to_target = cvd.percent_to_target;
                        dld.accelerator_value = cvd.accelerator_value;
                        dld.performance_total = cvd.performance_total;
                        dld.commissions_participation_id = cvd.commissions_participation_id;
                        dld.participation_index_value = cvd.participation_index_value;
                        dld.participation_location_count = cvd.participation_location_count;
                        dld.participation_target_count = cvd.participation_target_count;
                        dld.performance_target_id = cvd.performance_target_id;
                        dld.kpi_id = cvd.kpi_id;
                        dld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_district_leader_detail.InsertOnSubmit(dld);
                    }

                    dld.min_metric_id = cvd.min_metric_id;
                    dld.min_metric_value = cvd.min_metric_value;
                    dld.metric_id = cvd.metric_id;
                    dld.metric_value = cvd.metric_value;
                    dld.target_value = cvd.target_value;
                    dld.payout_count = cvd.payout_count;
                    dld.payout_amount = cvd.payout_amount;
                    dld.payout_total = cvd.payout_total;

                    return true;

                case Globals.performance_target_level.Region:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_region_leader rl = GetRegionLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_region_leader_detail rld = SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_id == cvd.payout_id && s.payout_category_id == cvd.payout_category).FirstOrDefault();

                    if (rld == null) {
                        rld = new Data.commissions_region_leader_detail();
                        rld.commissions_region_leader_id = rl.commissions_region_leader_id;
                        rld.commissions_type_id = cvd.commissions_type_id;
                        rld.payout_id = cvd.payout_id;
                        rld.payout_category_id = cvd.payout_category;
                        rld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        rld.percent_to_target = cvd.percent_to_target;
                        rld.accelerator_value = cvd.accelerator_value;
                        rld.performance_total = cvd.performance_total;
                        rld.commissions_participation_id = cvd.commissions_participation_id;
                        rld.participation_index_value = cvd.participation_index_value;
                        rld.participation_location_count = cvd.participation_location_count;
                        rld.participation_target_count = cvd.participation_target_count;
                        rld.performance_target_id = cvd.performance_target_id;
                        rld.kpi_id = cvd.kpi_id;
                        rld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_region_leader_detail.InsertOnSubmit(rld);
                    }

                    rld.min_metric_id = cvd.min_metric_id;
                    rld.min_metric_value = cvd.min_metric_value;
                    rld.metric_id = cvd.metric_id;
                    rld.metric_value = cvd.metric_value;
                    rld.target_value = cvd.target_value;
                    rld.payout_count = cvd.payout_count;
                    rld.payout_amount = cvd.payout_amount;
                    rld.payout_total = cvd.payout_total;

                    return true;

                case Globals.performance_target_level.Channel:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_channel_leader cl = GetChannelLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_channel_leader_detail cld = SQLserverDB_live.commissions_channel_leader_detail.Where(s => s.commissions_channel_leader_id == cl.commissions_channel_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_id == cvd.payout_id && s.payout_category_id == cvd.payout_category).FirstOrDefault();

                    if (cld == null) {
                        cld = new Data.commissions_channel_leader_detail();
                        cld.commissions_channel_leader_id = cl.commissions_channel_leader_id;
                        cld.commissions_type_id = cvd.commissions_type_id;
                        cld.payout_id = cvd.payout_id;
                        cld.payout_category_id = cvd.payout_category;
                        cld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        cld.percent_to_target = cvd.percent_to_target;
                        cld.accelerator_value = cvd.accelerator_value;
                        cld.performance_total = cvd.performance_total;
                        cld.commissions_participation_id = cvd.commissions_participation_id;
                        cld.participation_index_value = cvd.participation_index_value;
                        cld.participation_location_count = cvd.participation_location_count;
                        cld.participation_target_count = cvd.participation_target_count;
                        cld.performance_target_id = cvd.performance_target_id;
                        cld.kpi_id = cvd.kpi_id;
                        cld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_channel_leader_detail.InsertOnSubmit(cld);
                    }

                    cld.min_metric_id = cvd.min_metric_id;
                    cld.min_metric_value = cvd.min_metric_value;
                    cld.metric_id = cvd.metric_id;
                    cld.metric_value = cvd.metric_value;
                    cld.target_value = cvd.target_value;
                    cld.payout_count = cvd.payout_count;
                    cld.payout_amount = cvd.payout_amount;
                    cld.payout_total = cvd.payout_total;

                    return true;

                default:
                    return false;
            }
        }

        public bool SaveMSOPayoutCommissionsDetails(Globals.performance_target_level target_level, CommissionValueDetails cvd) {
            // this handles saving values for all category-based payouts, SKU is not used here and defaults to empty value
            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_sales_consultant_detail sd = SQLserverDB_live.commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_category_id == cvd.payout_category && s.mso_serial_number == cvd.mso_serial_number).FirstOrDefault();

                    if (sd == null) {
                        sd = new Data.commissions_sales_consultant_detail();
                        sd.commissions_sales_consultant_id = sc.commissions_sales_consultant_id;
                        sd.commissions_type_id = cvd.commissions_type_id;
                        sd.payout_id = cvd.payout_id;
                        sd.payout_category_id = cvd.payout_category;

                        // these aren't used here but set some defaults
                        sd.payout_sku = cvd.payout_sku;
                        sd.kpi_id = cvd.kpi_id;
                        sd.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_sales_consultant_detail.InsertOnSubmit(sd);
                    }

                    sd.percent_to_target = cvd.percent_to_target;
                    sd.accelerator_value = cvd.accelerator_value;
                    sd.performance_total = cvd.performance_total;
                    sd.min_metric_id = cvd.min_metric_id;
                    sd.min_metric_value = cvd.min_metric_value;
                    sd.metric_id = cvd.metric_id;
                    sd.metric_value = cvd.metric_value;
                    sd.target_value = cvd.target_value;
                    sd.payout_count = cvd.payout_count;
                    sd.payout_amount = cvd.payout_amount;
                    sd.payout_total = cvd.payout_total;
                    sd.mso_serial_number = cvd.mso_serial_number;

                    return true;

                case Globals.performance_target_level.Store:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_store_leader sl = GetStoreLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_store_leader_detail sld = SQLserverDB_live.commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_category_id == cvd.payout_category && s.mso_serial_number == cvd.mso_serial_number).FirstOrDefault();

                    if (sld == null) {
                        sld = new Data.commissions_store_leader_detail();
                        sld.commissions_store_leader_id = sl.commissions_store_leader_id;
                        sld.commissions_type_id = cvd.commissions_type_id;
                        sld.payout_id = cvd.payout_id;
                        sld.payout_category_id = cvd.payout_category;

                        // these aren't used here but set some defaults
                        sld.payout_sku = cvd.payout_sku;
                        sld.metric_id = cvd.metric_id;
                        sld.metric_value = cvd.metric_value;
                        sld.percent_to_target = cvd.percent_to_target;
                        sld.accelerator_value = cvd.accelerator_value;
                        sld.performance_total = cvd.performance_total;
                        sld.kpi_id = cvd.kpi_id;
                        sld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_store_leader_detail.InsertOnSubmit(sld);
                    }

                    sld.min_metric_id = cvd.min_metric_id;
                    sld.min_metric_value = cvd.min_metric_value;
                    sld.metric_id = cvd.metric_id;
                    sld.metric_value = cvd.metric_value;
                    sld.target_value = cvd.target_value;
                    sld.payout_count = cvd.payout_count;
                    sld.payout_amount = cvd.payout_amount;
                    sld.payout_total = cvd.payout_total;
                    sld.mso_serial_number = cvd.mso_serial_number;

                    return true;

                case Globals.performance_target_level.District:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_district_leader dl = GetDistrictLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_district_leader_detail dld = SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_category_id == cvd.payout_category).FirstOrDefault();

                    if (dld == null) {
                        dld = new Data.commissions_district_leader_detail();
                        dld.commissions_district_leader_id = dl.commissions_district_leader_id;
                        dld.commissions_type_id = cvd.commissions_type_id;
                        dld.payout_id = cvd.payout_id;
                        dld.payout_category_id = cvd.payout_category;
                        dld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        dld.percent_to_target = cvd.percent_to_target;
                        dld.accelerator_value = cvd.accelerator_value;
                        dld.performance_total = cvd.performance_total;
                        dld.commissions_participation_id = cvd.commissions_participation_id;
                        dld.participation_index_value = cvd.participation_index_value;
                        dld.participation_location_count = cvd.participation_location_count;
                        dld.participation_target_count = cvd.participation_target_count;
                        dld.performance_target_id = cvd.performance_target_id;
                        dld.kpi_id = cvd.kpi_id;
                        dld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_district_leader_detail.InsertOnSubmit(dld);
                    }

                    dld.min_metric_id = cvd.min_metric_id;
                    dld.min_metric_value = cvd.min_metric_value;
                    dld.metric_id = cvd.metric_id;
                    dld.metric_value = cvd.metric_value;
                    dld.target_value = cvd.target_value;
                    dld.payout_count = cvd.payout_count;
                    dld.payout_amount = cvd.payout_amount;
                    dld.payout_total = cvd.payout_total;

                    return true;

                case Globals.performance_target_level.Region:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_region_leader rl = GetRegionLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_region_leader_detail rld = SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.payout_category_id == cvd.payout_category).FirstOrDefault();

                    if (rld == null) {
                        rld = new Data.commissions_region_leader_detail();
                        rld.commissions_region_leader_id = rl.commissions_region_leader_id;
                        rld.commissions_type_id = cvd.commissions_type_id;
                        rld.payout_id = cvd.payout_id;
                        rld.payout_category_id = cvd.payout_category;
                        rld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        rld.percent_to_target = cvd.percent_to_target;
                        rld.accelerator_value = cvd.accelerator_value;
                        rld.performance_total = cvd.performance_total;
                        rld.commissions_participation_id = cvd.commissions_participation_id;
                        rld.participation_index_value = cvd.participation_index_value;
                        rld.participation_location_count = cvd.participation_location_count;
                        rld.participation_target_count = cvd.participation_target_count;
                        rld.performance_target_id = cvd.performance_target_id;
                        rld.kpi_id = cvd.kpi_id;
                        rld.kpi_points = cvd.kpi_points;

                        SQLserverDB_live.commissions_region_leader_detail.InsertOnSubmit(rld);
                    }

                    rld.min_metric_id = cvd.min_metric_id;
                    rld.min_metric_value = cvd.min_metric_value;
                    rld.metric_id = cvd.metric_id;
                    rld.metric_value = cvd.metric_value;
                    rld.target_value = cvd.target_value;
                    rld.payout_count = cvd.payout_count;
                    rld.payout_amount = cvd.payout_amount;
                    rld.payout_total = cvd.payout_total;

                    return true;

                default:
                    return false;
            }
        }

        // saves details of KPI
        public bool SaveKpiCommissionsDetails(Globals.performance_target_level target_level, CommissionValueDetails cvd) {
            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_sales_consultant_detail sd = SQLserverDB_live.commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && s.commissions_type_id == cvd.commissions_type_id && s.metric_id == cvd.metric_id).FirstOrDefault();

                    if (sd == null) {
                        sd = new Data.commissions_sales_consultant_detail();
                        sd.commissions_sales_consultant_id = sc.commissions_sales_consultant_id;
                        sd.commissions_type_id = cvd.commissions_type_id;
                        sd.payout_id = cvd.payout_id;
                        sd.payout_category_id = cvd.payout_category;
                        sd.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        sd.min_metric_id = cvd.min_metric_id;
                        sd.min_metric_value = cvd.min_metric_value;
                        sd.metric_id = cvd.metric_id;
                        sd.metric_value = cvd.metric_value;
                        sd.target_value = cvd.target_value;
                        sd.percent_to_target = cvd.percent_to_target;
                        sd.accelerator_value = cvd.accelerator_value;
                        sd.performance_total = cvd.performance_total;
                        sd.payout_count = cvd.payout_count;
                        sd.payout_amount = cvd.payout_amount;
                        sd.payout_total = cvd.payout_total;

                        SQLserverDB_live.commissions_sales_consultant_detail.InsertOnSubmit(sd);
                    }

                    sd.metric_id = cvd.metric_id;
                    sd.metric_value = cvd.metric_value;
                    sd.kpi_id = cvd.kpi_id;
                    sd.kpi_points = cvd.kpi_points;

                    return true;

                case Globals.performance_target_level.Store:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_store_leader sl = GetStoreLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_store_leader_detail sld = SQLserverDB_live.commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.metric_id == cvd.metric_id).FirstOrDefault();

                    if (sld == null) {
                        sld = new Data.commissions_store_leader_detail();
                        sld.commissions_store_leader_id = sl.commissions_store_leader_id;
                        sld.commissions_type_id = cvd.commissions_type_id;
                        sld.payout_id = cvd.payout_id;
                        sld.payout_category_id = cvd.payout_category;
                        sld.payout_sku = cvd.payout_sku;

                        // these aren't used here but set some defaults
                        sld.min_metric_id = cvd.min_metric_id;
                        sld.min_metric_value = cvd.min_metric_value;
                        sld.metric_id = cvd.metric_id;
                        sld.metric_value = cvd.metric_value;
                        sld.target_value = cvd.target_value;
                        sld.percent_to_target = cvd.percent_to_target;
                        sld.accelerator_value = cvd.accelerator_value;
                        sld.performance_total = cvd.performance_total;
                        sld.payout_count = cvd.payout_count;
                        sld.payout_amount = cvd.payout_amount;
                        sld.payout_total = cvd.payout_total;

                        SQLserverDB_live.commissions_store_leader_detail.InsertOnSubmit(sld);
                    }

                    sld.metric_id = cvd.metric_id;
                    sld.metric_value = cvd.metric_value;
                    sld.kpi_id = cvd.kpi_id;
                    sld.kpi_points = cvd.kpi_points;

                    return true;

                case Globals.performance_target_level.District:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_district_leader dl = GetDistrictLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_district_leader_detail dld = SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.metric_id == cvd.metric_id).FirstOrDefault();

                    if (dld == null) {
                        dld = new Data.commissions_district_leader_detail();
                        dld.commissions_district_leader_id = dl.commissions_district_leader_id;
                        dld.commissions_type_id = cvd.commissions_type_id;

                        // these aren't used here but set some defaults
                        dld.payout_id = cvd.payout_id;
                        dld.payout_category_id = cvd.payout_category;
                        dld.payout_sku = cvd.payout_sku;
                        dld.payout_count = cvd.payout_count;
                        dld.payout_amount = cvd.payout_amount;
                        dld.payout_total = cvd.payout_total;
                        dld.min_metric_id = cvd.min_metric_id;
                        dld.min_metric_value = cvd.min_metric_value;
                        dld.metric_id = cvd.metric_id;
                        dld.metric_value = cvd.metric_value;
                        dld.target_value = cvd.target_value;
                        dld.percent_to_target = cvd.percent_to_target;
                        dld.accelerator_value = cvd.accelerator_value;
                        dld.performance_total = cvd.performance_total;
                        dld.commissions_participation_id = cvd.commissions_participation_id;
                        dld.participation_index_value = cvd.participation_index_value;
                        dld.participation_location_count = cvd.participation_location_count;
                        dld.participation_target_count = cvd.participation_target_count;
                        dld.performance_target_id = cvd.performance_target_id;

                        SQLserverDB_live.commissions_district_leader_detail.InsertOnSubmit(dld);
                    }

                    dld.metric_id = cvd.metric_id;
                    dld.metric_value = cvd.metric_value;
                    dld.kpi_id = cvd.kpi_id;
                    dld.kpi_points = cvd.kpi_points;

                    return true;

                case Globals.performance_target_level.Region:
                    // get header for the detail row, if one doesn't exist yet create with defaults
                    Data.commissions_region_leader rl = GetRegionLeaderCommissions(cvd.pay_period_id, cvd.entity_id, true);

                    Data.commissions_region_leader_detail rld = SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == cvd.commissions_type_id && s.metric_id == cvd.metric_id).FirstOrDefault();

                    if (rld == null) {
                        rld = new Data.commissions_region_leader_detail();
                        rld.commissions_region_leader_id = rl.commissions_region_leader_id;
                        rld.commissions_type_id = cvd.commissions_type_id;

                        // these aren't used here but set some defaults
                        rld.payout_id = cvd.payout_id;
                        rld.payout_category_id = cvd.payout_category;
                        rld.payout_sku = cvd.payout_sku;
                        rld.payout_count = cvd.payout_count;
                        rld.payout_amount = cvd.payout_amount;
                        rld.payout_total = cvd.payout_total;
                        rld.min_metric_id = cvd.min_metric_id;
                        rld.min_metric_value = cvd.min_metric_value;
                        rld.metric_id = cvd.metric_id;
                        rld.metric_value = cvd.metric_value;
                        rld.target_value = cvd.target_value;
                        rld.percent_to_target = cvd.percent_to_target;
                        rld.accelerator_value = cvd.accelerator_value;
                        rld.performance_total = cvd.performance_total;
                        rld.commissions_participation_id = cvd.commissions_participation_id;
                        rld.participation_index_value = cvd.participation_index_value;
                        rld.participation_location_count = cvd.participation_location_count;
                        rld.participation_target_count = cvd.participation_target_count;
                        rld.performance_target_id = cvd.performance_target_id;

                        SQLserverDB_live.commissions_region_leader_detail.InsertOnSubmit(rld);
                    }

                    rld.metric_id = cvd.metric_id;
                    rld.metric_value = cvd.metric_value;
                    rld.kpi_id = cvd.kpi_id;
                    rld.kpi_points = cvd.kpi_points;

                    return true;

                default:
                    return false;
            }
        }

        public bool SavePerformanceTierItem(Data.performance_tiers_items performance_tier_item) {
            if (performance_tier_item != null) {
                Data.performance_tiers_items pti = SQLserverDB_live.performance_tiers_items.Where(pt => pt.performance_tiers_items_id == performance_tier_item.performance_tiers_items_id).FirstOrDefault();

                if (pti == null) {
                    pti = new Data.performance_tiers_items();
                    pti.item_id = performance_tier_item.item_id;
                    SQLserverDB_live.performance_tiers_items.InsertOnSubmit(pti);
                }

                pti.performance_tiers_id = performance_tier_item.performance_tiers_id;

                SQLserverDB_live.SubmitChanges();
                return true;
            }
            else
                return false;
        }

        #endregion

        #region Delete Routines

        public bool DeleteMinPaymentDetail(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, int commissions_type_id) {
            bool retval = false;

            switch (target_level) {
                case Globals.performance_target_level.Employee: {
                        Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(ppd.pay_period_id, entity_id);
                        if (sc != null && SQLserverDB_readonly.commissions_sales_consultant_detail.Any(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && s.commissions_type_id == commissions_type_id)) {
                            SQLserverDB_live.commissions_sales_consultant_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && s.commissions_type_id == commissions_type_id).ToList());
                            retval = true;
                        }
                        break;
                    }
                case Globals.performance_target_level.Store: {
                        Data.commissions_store_leader sl = GetStoreLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (sl != null && SQLserverDB_readonly.commissions_store_leader_detail.Any(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && s.commissions_type_id == commissions_type_id)) {
                            SQLserverDB_live.commissions_store_leader_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && s.commissions_type_id == commissions_type_id).ToList());
                            retval = true;
                        }
                        break;
                    }
                case Globals.performance_target_level.District: {
                        Data.commissions_district_leader dl = GetDistrictLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (dl != null && SQLserverDB_readonly.commissions_district_leader_detail.Any(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == commissions_type_id)) {
                            SQLserverDB_live.commissions_district_leader_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && s.commissions_type_id == commissions_type_id).ToList());
                            retval = true;
                        }
                        break;
                    }
                case Globals.performance_target_level.Region: {
                        Data.commissions_region_leader rl = GetRegionLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (rl != null && SQLserverDB_readonly.commissions_region_leader_detail.Any(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == commissions_type_id)) {
                            SQLserverDB_live.commissions_region_leader_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && s.commissions_type_id == commissions_type_id).ToList());
                            retval = true;
                        }
                        break;
                    }
                case Globals.performance_target_level.Channel: {
                        Data.commissions_channel_leader cl = GetChannelLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (cl != null && SQLserverDB_readonly.commissions_channel_leader_detail.Any(s => s.commissions_channel_leader_id == cl.commissions_channel_leader_id && s.commissions_type_id == commissions_type_id)) {
                            SQLserverDB_live.commissions_region_leader_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == cl.commissions_channel_leader_id && s.commissions_type_id == commissions_type_id).ToList());
                            retval = true;
                        }
                        break;
                    }
            }

            if (retval)
                SQLserverDB_live.SubmitChanges();

            return retval;
        }

        public bool DeleteAllExistingDetails(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, bool delete_header) {
            bool retval = false;

            switch (target_level) {
                case Globals.performance_target_level.Employee: {
                        Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(ppd.pay_period_id, entity_id);
                        if (sc != null && SQLserverDB_readonly.commissions_sales_consultant_detail.Any(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id)) {
                            SQLserverDB_live.commissions_sales_consultant_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id).ToList());
                            if (delete_header)
                                SQLserverDB_live.commissions_sales_consultant.DeleteOnSubmit(sc);
                            retval = true;
                        }
                        break;
                    }
                case Globals.performance_target_level.Store: {
                        Data.commissions_store_leader sl = GetStoreLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (sl != null && SQLserverDB_readonly.commissions_store_leader_detail.Any(s => s.commissions_store_leader_id == sl.commissions_store_leader_id)) {
                            SQLserverDB_live.commissions_store_leader_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id).ToList());
                            if (delete_header)
                                SQLserverDB_live.commissions_store_leader.DeleteOnSubmit(sl);
                            retval = true;
                        }
                        break;
                    }
                case Globals.performance_target_level.District: {
                        Data.commissions_district_leader dl = GetDistrictLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (dl != null && SQLserverDB_readonly.commissions_district_leader_detail.Any(s => s.commissions_district_leader_id == dl.commissions_district_leader_id)) {
                            SQLserverDB_live.commissions_district_leader_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id).ToList());
                            if (delete_header)
                                SQLserverDB_live.commissions_district_leader.DeleteOnSubmit(dl);
                            retval = true;
                        }
                        break;
                    }
                case Globals.performance_target_level.Region: {
                        Data.commissions_region_leader rl = GetRegionLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (rl != null && SQLserverDB_readonly.commissions_region_leader_detail.Any(s => s.commissions_region_leader_id == rl.commissions_region_leader_id)) {
                            SQLserverDB_live.commissions_region_leader_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id).ToList());
                            if (delete_header)
                                SQLserverDB_live.commissions_region_leader.DeleteOnSubmit(rl);
                            retval = true;
                        }
                        break;
                    }
                case Globals.performance_target_level.Channel: {
                        Data.commissions_channel_leader cl = GetChannelLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (cl != null && SQLserverDB_readonly.commissions_region_leader_detail.Any(s => s.commissions_region_leader_id == cl.commissions_channel_leader_id)) {
                            SQLserverDB_live.commissions_channel_leader_detail.DeleteAllOnSubmit(SQLserverDB_live.commissions_channel_leader_detail.Where(s => s.commissions_channel_leader_id == cl.commissions_channel_leader_id).ToList());
                            if (delete_header)
                                SQLserverDB_live.commissions_channel_leader.DeleteOnSubmit(cl);
                            retval = true;
                        }
                        break;
                    }
            }

            if (retval)
                SQLserverDB_live.SubmitChanges();

            return retval;
        }

        public bool DeleteExistingPayout(Globals.performance_target_level target_level, Data.commissions_pay_periods ppd, int entity_id, int performance_target_id, int payout_id, int location_id, bool delete_header = false) {
            bool retval = false;

            switch (target_level) {
                case Globals.performance_target_level.Employee: {
                        Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(ppd.pay_period_id, entity_id);
                        if (sc != null) {
                            List<commissions_sales_consultant_detail> sc_details = SQLserverDB_live.commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && (performance_target_id <= 0 || s.performance_target_id == performance_target_id) && (payout_id <= 0 || s.payout_id == payout_id) && (location_id <= 0 || s.payout_location_id == location_id)).ToList();
                            if (sc_details.Count > 0) {
                                SQLserverDB_live.commissions_sales_consultant_detail.DeleteAllOnSubmit(sc_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_sales_consultant.DeleteOnSubmit(sc);
                                retval = true;
                            }
                        }
                        break;
                    }
                case Globals.performance_target_level.Store: {
                        Data.commissions_store_leader sl = GetStoreLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (sl != null) {
                            List<commissions_store_leader_detail> sl_details = SQLserverDB_live.commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && (performance_target_id <= 0 || s.performance_target_id == performance_target_id) && (payout_id <= 0 || s.payout_id == payout_id)).ToList();
                            if (sl_details.Count > 0) {
                                SQLserverDB_live.commissions_store_leader_detail.DeleteAllOnSubmit(sl_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_store_leader.DeleteOnSubmit(sl);
                                retval = true;
                            }
                        }
                        break;
                    }
                case Globals.performance_target_level.District: {
                        Data.commissions_district_leader dl = GetDistrictLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (dl != null) {
                            List<commissions_district_leader_detail> dl_details = SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && (performance_target_id <= 0 || s.performance_target_id == performance_target_id) && (payout_id <= 0 || s.payout_id == payout_id)).ToList();
                            if (dl_details.Count > 0) {
                                SQLserverDB_live.commissions_district_leader_detail.DeleteAllOnSubmit(dl_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_district_leader.DeleteOnSubmit(dl);
                                retval = true;
                            }
                        }
                        break;
                    }
                case Globals.performance_target_level.Region: {
                        Data.commissions_region_leader rl = GetRegionLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (rl != null) {
                            List<commissions_region_leader_detail> rl_details = SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && (performance_target_id <= 0 || s.performance_target_id == performance_target_id) && (payout_id <= 0 || s.payout_id == payout_id)).ToList();
                            if (rl_details.Count > 0) {
                                SQLserverDB_live.commissions_region_leader_detail.DeleteAllOnSubmit(rl_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_region_leader.DeleteOnSubmit(rl);
                                retval = true;
                            }
                        }
                        break;
                    }
                case Globals.performance_target_level.Channel: {
                        Data.commissions_channel_leader cl = GetChannelLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (cl != null) {
                            List<commissions_channel_leader_detail> cl_details = SQLserverDB_live.commissions_channel_leader_detail.Where(s => s.commissions_channel_leader_id == cl.commissions_channel_leader_id && (performance_target_id <= 0 || s.performance_target_id == performance_target_id) && (payout_id <= 0 || s.payout_id == payout_id)).ToList();
                            if (cl_details.Count > 0) {
                                SQLserverDB_live.commissions_channel_leader_detail.DeleteAllOnSubmit(cl_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_channel_leader.DeleteOnSubmit(cl);
                                retval = true;
                            }
                        }
                        break;
                    }
            }

            if (retval)
                SQLserverDB_live.SubmitChanges();

            return retval;
        }

        public bool DeleteExistingPayoutsNotInThisList(Globals.performance_target_level target_level, commissions_pay_periods ppd, List<int> commissions_type_ids, int entity_id, List<int> performance_target_ids, List<int> payout_ids, List<int> location_ids, bool delete_header = false) {
            bool retval = false;

            switch (target_level) {
                case Globals.performance_target_level.Employee: {
                        Data.commissions_sales_consultant sc = GetSalesConsultantCommissions(ppd.pay_period_id, entity_id);
                        if (sc != null) {
                            List<commissions_sales_consultant_detail> sc_details = SQLserverDB_live.commissions_sales_consultant_detail.Where(s => s.commissions_sales_consultant_id == sc.commissions_sales_consultant_id && ((commissions_type_ids == null || commissions_type_ids.Count == 0) || commissions_type_ids.Contains(s.commissions_type_id))).ToList();

                            if (location_ids == null) {
                                if (performance_target_ids != null)
                                    sc_details = sc_details.Where(s => s.payout_id == 0 && !s.payout_location_id.HasValue && !performance_target_ids.Contains(s.performance_target_id)).ToList();
                                else
                                    if (payout_ids != null)
                                        sc_details = sc_details.Where(s => s.performance_target_id == 0 && !s.payout_location_id.HasValue && !payout_ids.Contains(s.payout_id)).ToList();
                            }
                            else {
                                if (performance_target_ids != null)
                                    sc_details = sc_details.Where(s => s.payout_id == 0 && ((s.payout_location_id.HasValue && !location_ids.Contains(s.payout_location_id.Value)) || !performance_target_ids.Contains(s.performance_target_id))).ToList();
                                else
                                    if (payout_ids != null)
                                        sc_details = sc_details.Where(s => s.performance_target_id == 0 && ((s.payout_location_id.HasValue && !location_ids.Contains(s.payout_location_id.Value)) || !payout_ids.Contains(s.payout_id))).ToList();
                            }
                            
                            if (sc_details.Count > 0) {
                                SQLserverDB_live.commissions_sales_consultant_detail.DeleteAllOnSubmit(sc_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_sales_consultant.DeleteOnSubmit(sc);
                            }

                            retval = (SQLserverDB_live.GetChangeSet().Deletes.Count > 0);  // return true if we deleted anything
                        }
                        break;
                    }
                case Globals.performance_target_level.Store: {
                        Data.commissions_store_leader sl = GetStoreLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (sl != null) {
                            List<commissions_store_leader_detail> sl_details = SQLserverDB_live.commissions_store_leader_detail.Where(s => s.commissions_store_leader_id == sl.commissions_store_leader_id && ((commissions_type_ids == null || commissions_type_ids.Count == 0) || commissions_type_ids.Contains(s.commissions_type_id))).ToList();

                            if (performance_target_ids != null)
                                sl_details = sl_details.Where(s => s.payout_id == 0 && !performance_target_ids.Contains(s.performance_target_id)).ToList();
                            else
                                if (payout_ids != null)
                                    sl_details = sl_details.Where(s => s.performance_target_id == 0 && !payout_ids.Contains(s.payout_id)).ToList();

                            if (sl_details.Count > 0) {
                                SQLserverDB_live.commissions_store_leader_detail.DeleteAllOnSubmit(sl_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_store_leader.DeleteOnSubmit(sl);
                                retval = true;
                            }
                        }
                        break;
                    }
                case Globals.performance_target_level.District: {
                        Data.commissions_district_leader dl = GetDistrictLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (dl != null) {
                            List<commissions_district_leader_detail> dl_details = SQLserverDB_live.commissions_district_leader_detail.Where(s => s.commissions_district_leader_id == dl.commissions_district_leader_id && ((commissions_type_ids == null || commissions_type_ids.Count == 0) || commissions_type_ids.Contains(s.commissions_type_id))).ToList();
                            if (performance_target_ids != null)
                                dl_details = dl_details.Where(s => s.payout_id == 0 && !performance_target_ids.Contains(s.performance_target_id)).ToList();
                            else
                                if (payout_ids != null)
                                    dl_details = dl_details.Where(s => s.performance_target_id == 0 && !payout_ids.Contains(s.payout_id)).ToList();
                            if (dl_details.Count > 0) {
                                SQLserverDB_live.commissions_district_leader_detail.DeleteAllOnSubmit(dl_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_district_leader.DeleteOnSubmit(dl);
                                retval = true;
                            }
                        }
                        break;
                    }
                case Globals.performance_target_level.Region: {
                        Data.commissions_region_leader rl = GetRegionLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (rl != null) {
                            List<commissions_region_leader_detail> rl_details = SQLserverDB_live.commissions_region_leader_detail.Where(s => s.commissions_region_leader_id == rl.commissions_region_leader_id && ((commissions_type_ids == null || commissions_type_ids.Count == 0) || commissions_type_ids.Contains(s.commissions_type_id))).ToList();
                            if (performance_target_ids != null)
                                rl_details = rl_details.Where(s => s.payout_id == 0 && !performance_target_ids.Contains(s.performance_target_id)).ToList();
                            else
                                if (payout_ids != null)
                                    rl_details = rl_details.Where(s => s.performance_target_id == 0 && !payout_ids.Contains(s.payout_id)).ToList();
                            if (rl_details.Count > 0) {
                                SQLserverDB_live.commissions_region_leader_detail.DeleteAllOnSubmit(rl_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_region_leader.DeleteOnSubmit(rl);
                                retval = true;
                            }
                        }
                        break;
                    }
                case Globals.performance_target_level.Channel: {
                        Data.commissions_channel_leader cl = GetChannelLeaderCommissions(ppd.pay_period_id, entity_id);
                        if (cl != null) {
                            List<commissions_channel_leader_detail> cl_details = SQLserverDB_live.commissions_channel_leader_detail.Where(s => s.commissions_channel_leader_id == cl.commissions_channel_leader_id && ((commissions_type_ids == null || commissions_type_ids.Count == 0) || commissions_type_ids.Contains(s.commissions_type_id))).ToList();
                            if (performance_target_ids != null)
                                cl_details = cl_details.Where(s => s.payout_id == 0 && !performance_target_ids.Contains(s.performance_target_id)).ToList();
                            else
                                if (payout_ids != null)
                                    cl_details = cl_details.Where(s => s.performance_target_id == 0 && !payout_ids.Contains(s.payout_id)).ToList();
                            if (cl_details.Count > 0) {
                                SQLserverDB_live.commissions_channel_leader_detail.DeleteAllOnSubmit(cl_details);
                                if (delete_header)
                                    SQLserverDB_live.commissions_channel_leader.DeleteOnSubmit(cl);
                                retval = true;
                            }
                        }
                        break;
                    }
            }

            if (retval)
                SQLserverDB_live.SubmitChanges();

            return retval;
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace CompPlanLib {
    public class CompPlanEngine {
        #region Global variables

        private DateTime startup_date;
        private DateTime run_date;
        const int prior_ppd_recalc_cutoff = 8;  // 8am

        // global lists and variables used by what-if calcs
        private Data.commissions_pay_periods ppd_whatif;
        private List<Data.EmployeeItems> employees_whatif;
        private Globals.performance_target_level target_level_whatif = Globals.performance_target_level.Unknown;
        private bool autocreate_new_month_data = false;

        // data access layer class
        private Data.DAL DAL;

        // logic layer class
        private CompPlanLogic CPL;

        // global objects layer class
        private Data.GOL GOL;

        #endregion

        #region Constructor/Destructor

        public CompPlanEngine(string SQLServerConnectionString_live, string SQLServerConnectionString_readonly, DateTime run) {
            Initialize(SQLServerConnectionString_live, SQLServerConnectionString_readonly, run);
        }

        public CompPlanEngine(string SQLServerConnectionString_live, string SQLServerConnectionString_readonly, DateTime? run = null) {
            Initialize(SQLServerConnectionString_live, SQLServerConnectionString_readonly, run);
        }

        public CompPlanEngine(DateTime? run = null) {
            Initialize("", "", run);
        }

        public CompPlanEngine(int payperiodID) {
            Initialize();
            //startup_date = gotPPd.start_date;
        }

        ~CompPlanEngine() {
        }

        #endregion

        #region Public methods

        public void ProcessCommissions(Globals.performance_target_level target_level) {
            DateTime current_date = DateTime.Now;

            try {
                // ProcessCommissions is called in a continuous loop by the commissions service and we could have ticked over into a new pay period date, so check if payperiod exists and create all required data if not
                if (run_date == null || run_date <= DateTime.MinValue || run_date.Date < current_date.Date) {
                    if (run_date == null || run_date <= DateTime.MinValue)
                        // first run - just set base starting date
                        run_date = startup_date;
                    else {
                        if (!EventLog.SourceExists("CompPlan Service"))
                            EventLog.CreateEventSource("CompPlan Service", "Application");

                        // new day - handle rolling over into a new day/possibly a new pay period
                        EventLog.WriteEntry("CompPlan Service", "CompPlan Service begin new day processing", EventLogEntryType.Information);

                        // set running date to be new date
                        run_date = current_date;

                        // process MSO extract spiff payouts - this requires a search of all RQ4 invoices by serial number so we only do it once-per-day
                        CPL.ProcessMSOExtract(run_date);

                        // clear cached lists so on the next RunCalcs the call to LoadGlobalLists() will get all new data
                        GOL.ClearGlobalLists();

                        EventLog.WriteEntry("CompPlan Service", "CompPlan Service end new day processing", EventLogEntryType.Information);
                    }
                }

                // for the first day of a new pay period, re-calc prior pay period until cut-off time on that day to pick up any last-minute invoices that might have come in while commissions was still running - the RunCalcs method handles the actual logic of deciding if its a new pay period or just a new day
                bool is_start_of_payperiod = (current_date.Day == 1 || (target_level == Globals.performance_target_level.Employee && current_date.Day == 16));
                if (is_start_of_payperiod && current_date.Hour <= prior_ppd_recalc_cutoff) {
                    EventLog.WriteEntry("CompPlan Service", "CompPlan Service start of new pay period - recalculating prior month", EventLogEntryType.Information);
                    DateTime prior_day = current_date.AddDays(-1);
                    RunCalcs(target_level, current_date, true, new DateTime(prior_day.Year, prior_day.Month, prior_day.Day, 23, 59, 59));
                }

                // calc current date/time
                RunCalcs(target_level, run_date, false);
            }

            catch (Exception e) {
                string innermsg = e.InnerException != null ? ", inner exception: " + e.InnerException.Message : "";
                innermsg += Environment.NewLine + "Stack Trace: " + Environment.NewLine + e.StackTrace;
                CPL.AddToErrors("Unexpected error encountered: " + e.Message + innermsg);
            }

            CPL.SendInfoLists();
        }

        public void ProcessCommissions(DateTime run_date, Globals.performance_target_level target_level, int rq_id, Globals.override_parameters optional_overrides) {
            // local data objects
            List<Data.InvoiceItems> RQ4_invoices = new List<Data.InvoiceItems>();
            List<Data.BSRitems> BSR_data = new List<Data.BSRitems>();

            Data.commissions_pay_periods ppd = null;

            switch (target_level) {
                case Globals.performance_target_level.Employee:
                    ppd = LoadPayPeriodData(run_date, true);
                    GOL.LoadGlobalLists(run_date, ppd);
                    LoadAllSalesData(ppd, Globals.BSR_report_level.Employee_Summary, out RQ4_invoices, out BSR_data);
                    ProcessSalesConsultants(ppd, RQ4_invoices, BSR_data, rq_id, optional_overrides);
                    break;
                case Globals.performance_target_level.Store:
                case Globals.performance_target_level.District:
                case Globals.performance_target_level.Region:
                case Globals.performance_target_level.Channel:
                    ppd = LoadPayPeriodData(run_date, false);

                    GOL.LoadGlobalLists(run_date, ppd);

                    ClearAllSalesData(RQ4_invoices, BSR_data);
                    LoadAllSalesData(ppd, Globals.BSR_report_level.Store, out RQ4_invoices, out BSR_data);

                    // filter down the stores list to just what is in BSR
                    CPL.GetActiveStores(Globals.performance_target_level.Store, BSR_data);

                    // check if any stores have hit any targets and add to internal list - to be used by participation index checks for DL and RL so always load here
                    CPL.LoadHitTargetsList(Globals.performance_target_level.Store, ppd, BSR_data);

                    if (target_level == Globals.performance_target_level.Store)
                        ProcessStores(ppd, RQ4_invoices, BSR_data, rq_id, optional_overrides);

                    BSR_data = DAL.GetBSRdata(ppd, Globals.BSR_report_level.District);
                    CPL.LoadHitTargetsList(Globals.performance_target_level.District, ppd, BSR_data);

                    if (target_level == Globals.performance_target_level.District)
                        ProcessDistricts(ppd, RQ4_invoices, BSR_data, rq_id, optional_overrides);

                    if (target_level == Globals.performance_target_level.Region) {
                        BSR_data = DAL.GetBSRdata(ppd, Globals.BSR_report_level.Region);
                        ProcessRegions(ppd, RQ4_invoices, BSR_data, rq_id, optional_overrides);
                    }

                    if (target_level == Globals.performance_target_level.Channel) {
                        BSR_data = DAL.GetBSRdata(ppd, Globals.BSR_report_level.Channel);
                        ProcessChannels(ppd, RQ4_invoices, BSR_data, rq_id, optional_overrides);
                    }

                    break;
            }
        }

        public void ProcessCommissionsTest() {
            CPL.ProcessMSOExtract(startup_date);
        }

        public decimal CalculateWhatIf(List<Data.BSRitems> bsr_data, ref List<Data.CommissionValueDetails> results_list) {
            decimal retval = 0.00m;

            if (results_list == null || bsr_data == null || ppd_whatif == null)
                return retval;

            results_list.Clear();
            List<Data.CommissionValueDetails> targets_res = new List<Data.CommissionValueDetails>();
            /*  NOT USED RIGHT NOW
            bsr_data.GroupBy(b => b.id_field).ToList().ForEach(d => {
                int id = d.Key;

                List<Data.BSRitems> BSRDataItems = bsr_data.Where(b => b.id_field == id).ToList();

                // first get their base gross profit, this can increase/decrease based off the performance targets so we log the original value first
                decimal base_gp = BSRDataItems.Where(b => b.metric_id == (int)Globals.BSR_metrics.GP_metric_id).Sum(b => b.total);

                // load the tier for this level, so the gp % can be calculated
                Data.PerformanceTierDataItem got_tier = CPL.GetPerformanceTier(target_level_whatif, id);

                // calculate GP margin for tier
                decimal gp_margin_percent = got_tier != null ? got_tier.gross_profit_margin : 0.00m;
                decimal gp_margin_amount = (base_gp * gp_margin_percent);

                // if we have a valid tier, get any defined commissions cap value
                decimal comm_cap = got_tier != null ? got_tier.commission_cap : 0.00m;

                // total commissions running total, populated by the various modules
                decimal res = 0.00m;
                decimal target_val = 0.00m;

                Data.EmployeeItems emp = null;

                // get the GP margin value and %
                switch (target_level_whatif) {
                    case Globals.performance_target_level.Employee:
                        emp = employees_whatif != null ? employees_whatif.Where(e => e.IdNumber == id).FirstOrDefault() : null;

                        // no employee for this ID, fail
                        if (emp == null)
                            CPL.AddToErrors("What-if calculator issue: no employee found for ID " + id.ToString());
                        else {
                            DateTime start_date = (emp != null & emp.startDate.HasValue) ? emp.startDate.Value : DateTime.Now;

                            // data validation
                            if (!emp.startDate.HasValue) {
                                CPL.AddToErrors("Employee " + emp.EmployeeName + " has no start date, so their eligibility for commissions cannot be determined!");
                            }

                            // no tier, raise an issue
                            if (got_tier == null) {
                                CPL.AddToErrors("What-if calculator issue: no Tier assigned for " + emp.EmployeeName);
                            }

                            // ProcessAccelerators
                            target_val = CPL.ProcessAccelerators(target_level_whatif, ppd_whatif, id, gp_margin_amount, BSRDataItems, emp.RQ4CommissionGroupID, gp_margin_amount, true, targets_res);
                        }
                        break;

                    case Globals.performance_target_level.Store:
                        Data.StoreItems st = CPL.GetActiveStores(Globals.performance_target_level.Store).Where(s => s.StoreID == id).FirstOrDefault();

                        // no store for this ID, fail
                        if (st == null) {
                            CPL.AddToErrors("What-if calculator issue: no store found for ID " + id.ToString());
                        }
                        else {
                            emp = employees_whatif != null ? employees_whatif.Where(e => e.IdNumber == st.StoreManagerID).FirstOrDefault() : null;

                            if (emp == null) {
                                CPL.AddToErrors("What-if calculator issue: no employee found for ID " + id.ToString());
                                return;
                            }

                            DateTime open_date = (st != null & st.OpenDate.HasValue) ? st.OpenDate.Value : DateTime.Now;

                            // no tier, raise an issue
                            if (got_tier == null) {
                                CPL.AddToErrors("What-if calculator issue: no Tier assigned for " + st.StoreName);
                            }

                            // accelerators
                            target_val = CPL.ProcessAccelerators(Globals.performance_target_level.Store, ppd_whatif, st.StoreID, gp_margin_amount, BSRDataItems, emp.RQ4CommissionGroupID, gp_margin_amount, true, targets_res);
                        }
                        break;

                    case Globals.performance_target_level.District:
                        var district = CPL.GetActiveStores(Globals.performance_target_level.District).Where(di => di.DistrictID == id).GroupBy(s => s.DistrictID).Select(s => new { district_id = s.Key, region_id = s.Max(r => r.RegionID), region_name = s.Max(r => r.RegionName), district_name = s.Max(di => di.DistrictName), district_leader_id = s.Max(di => di.DistrictManagerID), total_stores = s.Count() }).FirstOrDefault();

                        // no district for this ID, fail
                        if (district == null) {
                            CPL.AddToErrors("What-if calculator issue: no district found for ID " + id.ToString());
                        }
                        else {
                            // no tier, raise an issue
                            if (got_tier == null) {
                                CPL.AddToErrors("What-if calculator issue: no Tier assigned for " + district.district_name);
                            }

                            emp = employees_whatif != null ? employees_whatif.Where(e => e.IdNumber == district.district_leader_id.Value).FirstOrDefault() : null;

                            if (emp == null) {
                                CPL.AddToErrors("What-if calculator issue: no employee found for ID " + id.ToString());
                                return;
                            }

                            // accelerators
                            target_val = CPL.ProcessAccelerators(Globals.performance_target_level.District, ppd_whatif, id, gp_margin_amount, BSRDataItems, emp.RQ4CommissionGroupID, gp_margin_amount, true, targets_res);
                        }
                        break;

                    case Globals.performance_target_level.Region:
                        var region = CPL.GetActiveStores(Globals.performance_target_level.Region).Where(ri => ri.RegionID == id).GroupBy(s => s.RegionID).Select(s => new { region_id = s.Key, region_name = s.Max(r => r.RegionName), region_leader_id = s.Max(r => r.RegionManagerID), total_districts = s.GroupBy(di => di.DistrictID).Count() }).FirstOrDefault();

                        // no region for this ID, fail
                        if (region == null) {
                            CPL.AddToErrors("What-if calculator issue: no region found for ID " + id.ToString());
                        }
                        else {
                            // no tier, raise an issue
                            if (got_tier == null) {
                                CPL.AddToErrors("What-if calculator issue: no Tier assigned for " + region.region_name);
                            }

                            emp = employees_whatif != null ? employees_whatif.Where(e => e.IdNumber == region.region_leader_id.Value).FirstOrDefault() : null;

                            if (emp == null) {
                                CPL.AddToErrors("What-if calculator issue: no employee found for ID " + id.ToString());
                                return;
                            }

                            // accelerators
                            target_val = CPL.ProcessAccelerators(Globals.performance_target_level.Region, ppd_whatif, id, gp_margin_amount, BSRDataItems, emp.RQ4CommissionGroupID, gp_margin_amount, true, targets_res);
                        }
                        break;

                    default:
                        target_val = 0.00m;
                        break;
                }

                res += target_val;

                // check if commissions cap is active and apply if it is
                if (comm_cap > 0 && res > comm_cap)
                    res = comm_cap;

                retval = res;
            });
            */
            results_list = targets_res;

            return retval;
        }

        public List<Data.rq4_iQmetrix_EmployeeGroup> GetRqCommissionGroupsList() {
            return DAL.GetRqCommissionsGroups();
        }

        public List<Data.StoreItems> GetLocationsList(DateTime comp_date) {
            return DAL.GetStoreData(comp_date);
        }

        #endregion

        #region Initialization methods

        public void InitialiseWhatIf(Globals.performance_target_level target_level) {
            // this is being used for the what-if calcs, load all the data once so the call the what-if is quicker
            if (target_level_whatif != target_level) {
                bool reload_data = false;

                // only need to reload global data if not ran before, or if switching between employees and SL's upward - SL's upward all share the same pay period so don't need to reload data for these
                if ((target_level_whatif == Globals.performance_target_level.Unknown) || (target_level_whatif == Globals.performance_target_level.Employee && target_level != Globals.performance_target_level.Employee) || (target_level_whatif != Globals.performance_target_level.Employee && target_level == Globals.performance_target_level.Employee))
                    reload_data = true;

                target_level_whatif = target_level;

                if (reload_data) {
                    ppd_whatif = LoadPayPeriodData(startup_date, (target_level == Globals.performance_target_level.Employee));
                    CPL.LoadCommissionsConfig(target_level, ppd_whatif);
                    GOL.LoadGlobalLists(startup_date, ppd_whatif);

                    if (target_level == Globals.performance_target_level.Employee)
                        employees_whatif = DAL.GetEmployeeData();
                }
            }
        }

        private void Initialize(string SQLServerConnectionString_live = "", string SQLServerConnectionString_readonly = "", DateTime? run = null) {
            // set up DB connection
            DAL = new Data.DAL(SQLServerConnectionString_live, SQLServerConnectionString_readonly);

            // global objects to be shared on this run
            GOL = new Data.GOL(DAL);

            // set up logic layer
            CPL = new CompPlanLogic(DAL, GOL);

            // config settings
            autocreate_new_month_data = (ConfigurationManager.AppSettings["AutocreateNewMonthData"] != null && ConfigurationManager.AppSettings["AutocreateNewMonthData"].ToString() == "1");

            if (run.HasValue)
                startup_date = run.Value;
            else
                startup_date = DateTime.Now;
        }

        #endregion

        #region Data loading and clearing methods

        private Data.commissions_pay_periods LoadPayPeriodData(DateTime rundate, bool forSC) {
            // load pay periods, SC's have their own that is split for the month
            return DAL.GetPayPeriod(rundate.Date, forSC);
        }

        private void LoadAllSalesData(Data.commissions_pay_periods ppd, Globals.BSR_report_level report_level, out List<Data.InvoiceItems> RQ4Data, out List<Data.BSRitems> BSRData) {
            RQ4Data = DAL.GetRQ4Invoices(ppd);
            BSRData = DAL.GetBSRdata(ppd, report_level);
        }

        private void ClearAllSalesData(List<Data.InvoiceItems> RQ4Data, List<Data.BSRitems> BSRData) {
            if (RQ4Data != null && RQ4Data.Count > 0)
                RQ4Data.Clear();

            if (BSRData != null && BSRData.Count > 0)
                BSRData.Clear();
        }

        #endregion

        #region Main calc methods

        private void RunCalcs(Globals.performance_target_level target_level, DateTime run_date, bool ForPreviousPayperiod, DateTime? last_run = null) {
            // clear internal notification message lists for info and errors
            CPL.InitMessageLists();

            // local data objects for RQ4 and BSR
            List<Data.InvoiceItems> RQ4_invoices = new List<Data.InvoiceItems>();
            List<Data.BSRitems> BSR_data = new List<Data.BSRitems>();

            Data.commissions_pay_periods ppd_current;
            Data.commissions_pay_periods ppd_prev;

            Data.commissions_pay_periods ppd = null;

            if (target_level == Globals.performance_target_level.All || target_level == Globals.performance_target_level.Employee) {
                // Sales Consultants -
                // sales consultants have their own pay period range, two per month so load invoices for this period to run SC calcs first
                // e.g. if the run date is the 07/31, we need previous invoices for 07/01-07/15

                // setup the pay period we are running for:
                ppd_current = LoadPayPeriodData(run_date, true);

                if (ForPreviousPayperiod && last_run.HasValue) {
                    // get pay period info for last time ran, and compare with current pay period - if they differ we're in a new pay period so set the previous to be re-calculated. This is so we only go back and re-calculate the prior pay period once
                    ppd_prev = LoadPayPeriodData(last_run.Value, true);
                    if (ppd_current.pay_period_id != ppd_prev.pay_period_id) {
                        run_date = last_run.Value;
                        ppd = ppd_prev;
                    }
                }
                else
                    ppd = ppd_current;

                // invalid pay period found, when running for a previous pay period a null pay period just indicates nothing to do so don't both raising an error
                if (ppd == null) {
                    if (!ForPreviousPayperiod)
                        CPL.AddToErrors("No valid pay periods found for Sales Consultants, CompPlan cannot run!");
                }
                else {
                    GOL.LoadGlobalLists(run_date, ppd);
                    ClearAllSalesData(RQ4_invoices, BSR_data);
                    LoadAllSalesData(ppd, Globals.BSR_report_level.Employee_Summary, out RQ4_invoices, out BSR_data);

                    ProcessSalesConsultants(ppd, RQ4_invoices, BSR_data);
                }
            }

            if (target_level == Globals.performance_target_level.All || target_level == Globals.performance_target_level.Store || target_level == Globals.performance_target_level.District || target_level == Globals.performance_target_level.Region || target_level == Globals.performance_target_level.Channel) {
                // The stores upwards - 
                // everyone else runs the pay period range for the whole month, load invoices for the whole month
                // e.g. if the run date is the 07/31, we need previous invoices for 06/01-06/30
                ppd_current = LoadPayPeriodData(run_date, false);

                if (ForPreviousPayperiod) {
                    // get pay period info for last time ran, and compare with current pay period - if they differ we're in a new pay period so set the previous to be re-calculated. This is so we only go back and re-calculate the prior pay period once
                    ppd_prev = LoadPayPeriodData(last_run.Value, false);
                    if (ppd_current.pay_period_id != ppd_prev.pay_period_id) {
                        run_date = last_run.Value;
                        ppd = ppd_prev;
                    }
                }
                else
                    ppd = ppd_current;

                // invalid pay period found, when running for a previous pay period a null pay period just indicates nothing to do so don't both raising an error
                if (ppd == null) {
                    if (!ForPreviousPayperiod)
                        CPL.AddToErrors("No valid pay periods found for Store Leaders upward, CompPlan cannot run!");
                }
                else {
                    // different pay period, so clear out any cached data as it will be for the wrong pay period
                    GOL.LoadGlobalLists(run_date, ppd);
                    ClearAllSalesData(RQ4_invoices, BSR_data);
                    LoadAllSalesData(ppd, Globals.BSR_report_level.Store, out RQ4_invoices, out BSR_data);

                    // filter down the stores list to just what is in BSR
                    CPL.GetActiveStores(Globals.performance_target_level.Store, BSR_data);
                    // check if any stores have hit any targets and add to internal list - to be used by participation index checks
                    CPL.LoadHitTargetsList(Globals.performance_target_level.Store, ppd, BSR_data);
                    // check if any districts have hit any targets and add to internal list - to be used by participation index checks
                    CPL.LoadHitTargetsList(Globals.performance_target_level.District, ppd, BSR_data);

                    // GJK 6/1/2016: added new Business Sales Consultants - paid monthly like SL's and lead their own store, but are assigned to business sales
                    // GJK 2/10/2017: not paid like SL's any more, now there are different levels of business consultants so we just let the regular level methods handle them
                    //if (target_level == Globals.performance_target_level.All || target_level == Globals.performance_target_level.BusinesSalesConsultant)
                    //    ProcessBusinessConsultants(ppd, RQ4_invoices, BSR_data);

                    if (target_level == Globals.performance_target_level.All || target_level == Globals.performance_target_level.Store)
                        ProcessStores(ppd, RQ4_invoices, BSR_data);

                    // only need to re-load BSR data for each level above store, RQ4 invoices just load the once for the entire date range
                    if (target_level == Globals.performance_target_level.All || target_level == Globals.performance_target_level.District) {
                        BSR_data = DAL.GetBSRdata(ppd, Globals.BSR_report_level.District);
                        ProcessDistricts(ppd, RQ4_invoices, BSR_data);
                    }

                    if (target_level == Globals.performance_target_level.All || target_level == Globals.performance_target_level.Region) {
                        BSR_data = DAL.GetBSRdata(ppd, Globals.BSR_report_level.Region);
                        ProcessRegions(ppd, RQ4_invoices, BSR_data);
                    }

                    if (target_level == Globals.performance_target_level.All || target_level == Globals.performance_target_level.Channel) {
                        BSR_data = DAL.GetBSRdata(ppd, Globals.BSR_report_level.Channel);
                        ProcessChannels(ppd, RQ4_invoices, BSR_data);
                    }
                }
            }
        }

        #endregion

        #region Sales Consultants processing

        private void ProcessSalesConsultants(Data.commissions_pay_periods ppd, List<Data.InvoiceItems> RQ4Data, List<Data.BSRitems> BSRData, int rq_id = -1, Globals.override_parameters optional_overrides = null) {
            CPL.LoadCommissionsConfig(Globals.performance_target_level.Employee, ppd);

            // SC target values references store targets, so full store level upward data needs to be loaded
            Data.commissions_pay_periods ppd_store = DAL.GetPayPeriod(ppd.start_date, false);

            // also need the whole month for team bonus stuff
            List<Data.BSRitems> BSRStoreMonthlyData = DAL.GetBSRdata(ppd_store, Globals.BSR_report_level.Store);

            // get list of employees to process - this will apply the location as it is/was in BSR in this pay period
            List<Data.EmployeeItems> employees = (rq_id > 0) ? CPL.GetActiveEmployees(BSRData).Where(e => e.IdNumber == rq_id).ToList() : CPL.GetActiveEmployees(BSRData).OrderBy(o => o.EmployeeName).ToList();

            if (employees.Count == 0)
                return;

            // since there is a lot of employees to process we split the employee list up in to blocks so we can process smaller lists in parallel which means  we'll complete all comp calcs faster
            int blocksize = 1000;  // list are split into blocks of 1000 reps per thread
            var processing_list = new List<List<Data.EmployeeItems>>();
            for (int i = 0; i < employees.Count; i += blocksize)
                processing_list.Add(employees.GetRange(i, Math.Min(blocksize, employees.Count - i)).OrderBy(o => o.LastCompUpdate).ToList());  // put oldest comp to the top so it'll get processed first on this pass
            int item_array_count = processing_list.Count();
            
            ManualResetEvent[] list_done = new ManualResetEvent[item_array_count];  // this holds the completion flag to indicate thread has finished, we need one for each list we'll process

            // assign each list its own thread in the thread pool
            for (int item_idx = 0; item_idx < item_array_count; item_idx++) {
                list_done[item_idx] = new ManualResetEvent(false);  // so this thread can report back when its done, it'll flag as complete in the array

                // create an instance of the calculation class for this thread - we also pass in the final_data list which the thread will update, and as its a pointer to the list passed by the caller it means the updates can be accessed outside the thread
                EmployeeListProcessor list_proc = new EmployeeListProcessor(DAL, GOL, list_done[item_idx], ppd, ppd_store,processing_list[item_idx].ToList(), RQ4Data, BSRData, BSRStoreMonthlyData, optional_overrides);

                // queue up this list processing class object to run - the threadpoolcallback invokes the class and starts it running
                ThreadPool.QueueUserWorkItem(list_proc.RunComp, item_idx);
            };

            // wait for all threads in the pool to complete - this will wait until all elements in the list_done array report complete, each thread sets this as it completes
            WaitHandle.WaitAll(list_done);

            // log last run date for SC in this pay period - saves to commissions_configuration but only if we didn't run for a single person since that's usually a re-calculation and we don't want everyone to show as being updated
            if (rq_id <= 0)
                DAL.SaveLastRunDate(Globals.performance_target_level.Employee, ppd.pay_period_id, DateTime.Now);
        }

        #endregion

        #region Store Leader processing

        private void ProcessStores(Data.commissions_pay_periods ppd, List<Data.InvoiceItems> RQ4Data, List<Data.BSRitems> BSRData, int leader_rq_id = -1, Globals.override_parameters optional_overrides = null) {
            CPL.LoadCommissionsConfig(Globals.performance_target_level.Store, ppd);

            List<Data.StoreItems> stores = (leader_rq_id > 0) ? CPL.GetActiveStores(Globals.performance_target_level.Store, BSRData).Where(s => s.StoreManagerID == leader_rq_id).ToList() : CPL.GetActiveStores(Globals.performance_target_level.Store, BSRData);

            // now loop the filtered stores - by not passing any BSR data this method will just return the store list
            stores.Where(s => s.StoreTypeID != (int)Globals.store_types.not_open && s.StoreTypeID != (int)Globals.store_types.other).OrderBy(o => o.StoreName).ToList().ForEach(s => {
                try {
                    int store_id = (optional_overrides != null && optional_overrides.location_id > 0) ? optional_overrides.location_id : s.StoreID;
                    List<Data.BSRitems> BSRDataItems = null;

                    // GJK 09/30/2015 - part of the Oct 2015 comp updates, all store leaders now have a commission group:
                    // awireless store leaders who are paid the original values and any from the north trial area have different payouts so we allow those through too
                    if (s.StoreManagerCommissionGroupID.HasValue) {
                        if (s.StoreManagerCommissionGroupID.Value == (int)Globals.rq4_commission_groups.non_selling_store_manager || 
                            s.StoreManagerCommissionGroupID.Value == (int)Globals.rq4_commission_groups.store_manager || 
                            s.StoreManagerCommissionGroupID.Value == (int)Globals.rq4_commission_groups.business_consultant ||
                            s.StoreManagerCommissionGroupID.Value == (int)Globals.rq4_commission_groups.trial_store_manager)
                            BSRDataItems = (BSRData != null) ? BSRData.Where(b => b.id_field == store_id).ToList() : null;
                    }

                    if (BSRDataItems != null && BSRDataItems.Count > 0) {
                        // valid store with data, calculate
                        DateTime open_date = (s.OpenDate.HasValue) ? s.OpenDate.Value : DateTime.Now;
                        DateTime? close_date = s.CloseDate;
                        int leader_comm_group_id = (optional_overrides != null && optional_overrides.commission_group_id > 0) ? optional_overrides.commission_group_id : (s.StoreManagerCommissionGroupID.HasValue) ? s.StoreManagerCommissionGroupID.Value : -1;

                        // GJK 9/5/2017 - now we can allow people to override the comp group and/or location we need to first clear down all existing commissions if those are being overridden, since it means a whole different set of payouts
                        if ((s.StoreManagerCommissionGroupID.HasValue && leader_comm_group_id != s.StoreManagerCommissionGroupID.Value) || store_id != s.StoreID)
                            DAL.DeleteAllExistingDetails(Globals.performance_target_level.Store, ppd, store_id, false);

                        decimal target_val = 0.00m;
                        decimal payout_val = 0.00m;
                        decimal payout_val_below_line = 0.00m;

                        // first get their base gross profit, this can increase/decrease based off the performance targets so we log the original value first
                        decimal base_gp = BSRDataItems.Where(b => b.metric_id == (int)Globals.BSR_metrics.GP_metric_id).Sum(b => b.total);

                        // store GP adjusted for commissionable values
                        decimal comm_gp = base_gp;

                        // store any manual adjustment total and add into GP
                        decimal manual_adj = CPL.GetManualAdjustment(Globals.performance_target_level.Store, store_id);
                        comm_gp += manual_adj;

                        // any coupons have to be deducted
                        decimal coupons = CPL.GetCoupons(RQ4Data.Where(rq => rq.StoreID == store_id).ToList());
                        comm_gp -= coupons;

                        // get any payouts that go into GP
                        comm_gp += CPL.ProcessPayoutsSKU(Globals.performance_target_level.Store, ppd, store_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.all_types, true, leader_comm_group_id);
                        comm_gp += CPL.ProcessPayoutsCategory(Globals.performance_target_level.Store, ppd, store_id, BSRDataItems, RQ4Data, true, leader_comm_group_id);

                        // load the tier for this level, so the gp % can be calculated
                        // get the GP margin value and % - first handle auto-allocation to the correct tier if this store doesn't have a tier (no default based on a total exists at this level so this will just create if a tier is not already allocated)
                        if (CPL.UpdateTierAssignment(Globals.performance_target_level.Store, ppd, store_id, BSRDataItems))
                            CPL.ReloadLoadTiersForID(Globals.performance_target_level.Store, ppd, store_id);  // tier assignment has changed - reload the list if required

                        Data.PerformanceTierDataItem got_tier = CPL.GetPerformanceTier(Globals.performance_target_level.Store, store_id);

                        // no tier, raise an issue
                        if (got_tier == null)
                            CPL.AddToNotify("No Tier assigned to Store " + s.StoreName);

                        // if we have a valid tier, get any defined commissions cap value
                        decimal comm_cap = got_tier != null ? got_tier.commission_cap : 0.00m;

                        // get the GP margin value and %
                        decimal gp_margin_percent = got_tier != null ? got_tier.gross_profit_margin : 0.00m;
                        decimal gp_margin_amount = (comm_gp * gp_margin_percent);

                        // total commissions running total, populated by the various modules
                        decimal res = 0.00M;

                        // data validation
                        if (!s.OpenDate.HasValue)
                            CPL.AddToNotify("Store " + s.StoreName + " has no open date, so its eligibility for commissions cannot be determined!");

                        // run performance targets module
                        //target_val = CPL.ProcessAccelerators(Globals.performance_target_level.Store, ppd, store_id, gp_margin_amount, BSRDataItems);
                        //res += target_val;

                        // run the target payouts module, a team bonus payout could occur and if that bonus is to be paid out regardless of min boxes then the team bonus total won't be in the result of the method it will be in the seperate variable instead
                        Data.TargetPayoutResult tp_res = CPL.ProcessTargetPayouts(Globals.performance_target_level.Store, ppd, store_id, BSRDataItems, comm_gp, comm_gp, leader_comm_group_id);
                        target_val = tp_res.total;
                        res += target_val;

                        // run monthly bonus payouts module, this has to be called here so the sub-total from this won't be included in the multiplier
                        Data.TargetPayoutResult tp_tb_res = CPL.ProcessTeamBonuses(Globals.performance_target_level.Store, ppd, ppd, store_id, BSRDataItems, comm_gp, comm_gp, store_id, leader_comm_group_id);
                        res += tp_tb_res.total;

                        // SKU-based payouts - get any that will be blocked if min target not met
                        payout_val = CPL.ProcessPayoutsSKU(Globals.performance_target_level.Store, ppd, store_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.obey_min_checks, false, leader_comm_group_id);
                        res += payout_val;

                        // SKU-based payouts - get any that will always be paid regardless of min check
                        payout_val_below_line = CPL.ProcessPayoutsSKU(Globals.performance_target_level.Store, ppd, store_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.ignore_min_checks, false, leader_comm_group_id);

                        // Category-based payouts
                        payout_val = CPL.ProcessPayoutsCategory(Globals.performance_target_level.Store, ppd, store_id, BSRDataItems, RQ4Data, false, leader_comm_group_id);
                        res += payout_val;

                        // KPIs
                        decimal kpi_total = CPL.ProcessKPI(Globals.performance_target_level.Store, ppd.pay_period_id, store_id, BSRDataItems);
                        res += kpi_total;

                        // check if commissions cap is active and apply if it is
                        if (comm_cap > 0 && res > comm_cap)
                            res = comm_cap;

                        // save the results
                        Data.CommissionValues cv = new Data.CommissionValues();
                        cv.pay_period_id = ppd.pay_period_id;
                        cv.entity_id = s.StoreID;
                        cv.region_id = s.RegionID;
                        cv.region_name = s.RegionName;
                        cv.district_id = s.DistrictID;
                        cv.district_name = s.DistrictName;
                        cv.store_id = s.StoreID;
                        cv.store_name = s.StoreName;
                        cv.employee_id = s.StoreManagerID;
                        cv.base_gross_profit = base_gp;
                        cv.commission_gross_profit = comm_gp;
                        cv.gross_profit_margin_percent = gp_margin_percent;
                        cv.gross_profit_margin = gp_margin_amount;
                        cv.performance_total = target_val;
                        cv.payout_total = 0.00M;
                        cv.manual_adjustment = manual_adj;
                        cv.coupons = coupons;
                        cv.kpi_total = kpi_total;

                        // GJK 6/28/2016: the min payout logic is way out of date now and hasn't been used in over a year, bypassing it now
                        cv.commission_total = res;

                        // only set the total commissions for these if min target met, if there is no min then it will return true
                        //cv.commission_total = CPL.HandleMinBoxPayout(Globals.performance_target_level.Store, ppd, store_id, BSRDataItems, res, comm_gp, out cv.current_boxes_total, out cv.min_boxes_total);

                        // any team bonus amount that should be paid regardless of min check
                        //cv.commission_total += team_bonus_payout_val_ignore_min;

                        // always set the total commissions for these payouts
                        cv.commission_total += payout_val_below_line;

                        // add in any existing MSO spiff payout - these are created outside this loop so have to be added in to the header total
                        cv.commission_total += CPL.GetExistingMSOAmount(Globals.spiff_level.Store, ppd.pay_period_id, store_id);

                        // GJK 6/1/2016 - new hack to handle special multiplier payout
                        //cv.commission_total += CPL.ApplyMultiplier(Globals.performance_target_level.Store, ppd, store_id, BSRDataItems, tp_res.box_attain_percent, cv.commission_total);

                        // run accelerators module if one exists for this pay period
                        if (CPL.AcceleratorsExistForPayPeriod(ppd)) {
                            Globals.accelerator_source_values sv = new Globals.accelerator_source_values();
                            sv.gross_profit = comm_gp;
                            sv.commission_total_month = cv.commission_total;
                            sv.BSRDataItems = BSRDataItems;
                            sv.BSRDataItemsMonthly = BSRDataItems;
                            res = CPL.ProcessAccelerators(Globals.performance_target_level.Store, ppd, store_id, sv, leader_comm_group_id);
                            if (res > 0)
                                cv.commission_total += res;
                        }

                        DAL.SaveCommissionsValuesHeader(Globals.performance_target_level.Store, cv);
                        DAL.Commit();

                        if (s.StoreManagerCommissionGroupID.Value == (int)Globals.rq4_commission_groups.business_consultant) {
                            // now pass a spiff through to the rep who created the lead, the rep ID is supposed to be in the serial number field and then group up to get total invoices to create a single payout which will be (amount * qty) linked to a payout category which holds the $ value
                            foreach (var invoices in RQ4Data.Where(inv => inv.EmployeeID == s.StoreManagerID && inv.Sku == "VECORB000326" && inv.SerialNumber.Length > 0).GroupBy(gp => gp.SerialNumber).Select(inv => new { rep_id = inv.Key, total_sold = inv.Sum(sum => sum.Quantity) }).ToList()) {
                                if (String.IsNullOrWhiteSpace(invoices.rep_id))
                                    continue;

                                Data.EmployeeItems rep = CPL.GetActiveEmployees().FirstOrDefault(e => e.AccountDisabled == false && e.SpecialIdentifier == invoices.rep_id);
                                if (rep == null)
                                    continue;

                                CPL.CreatePendingPayout(Globals.performance_target_level.Employee, ppd, Globals.commission_types.Payouts_cat, "B2B5", rep.IdNumber, invoices.total_sold);
                            }
                        }
                    }
                }
                catch (Exception err) {
                    CPL.AddToErrors(String.Format("ProcessStores - unexpected error occured processing store {0} : {1}", s.StoreName, err.Message));
                }
            });

            // log last run date for SL's in this pay period - saves to commissions_configuration but only if we didn't run for a single person since that's usually a re-calculation and we don't want everyone to show as being updated
            if (leader_rq_id <= 0)
                DAL.SaveLastRunDate(Globals.performance_target_level.Store, ppd.pay_period_id, DateTime.Now);
        }

        #endregion

        #region District Leader processing

        private void ProcessDistricts(Data.commissions_pay_periods ppd, List<Data.InvoiceItems> RQ4Data, List<Data.BSRitems> BSRData, int leader_rq_id = -1, Globals.override_parameters optional_overrides = null) {
            CPL.LoadCommissionsConfig(Globals.performance_target_level.District, ppd);

            // get list of districts from the active stores, only count a store in this district if it has been open three months or longer
            var districts = CPL.GetActiveStores(Globals.performance_target_level.District, BSRData).GroupBy(s => s.DistrictID).Select(s => new { district_id = s.Key, region_id = s.Max(r => r.RegionID), region_name = s.Max(r => r.RegionName), district_name = s.Max(d => d.DistrictName), district_leader_id = s.Max(d => d.DistrictManagerID), leader_commission_group_id = s.Max(d => d.DistrictManagerCommissionGroupID), total_stores = s.Where(st => !st.CloseDate.HasValue && st.totalMonths >= 3).Count() }).ToList();

            if (leader_rq_id > 0)
                districts = districts.Where(d => d.district_leader_id == leader_rq_id).ToList();

            // loop the districts and process commissions
            districts.OrderBy(o => o.district_name).ToList().ForEach(d => {
                try {
                    int district_id = (optional_overrides != null && optional_overrides.location_id > 0) ? optional_overrides.location_id : d.district_id;
                    int total_stores = d.total_stores;
                    List<Data.BSRitems> BSRDataItems = null;

                    // GJK 7/12/2016 - all district leaders now have a commission group, this is to deal with any DLs that have been promoted upward but left assigned to their original district, we now delete any comp for this district if the assigned leader isn't to be paid at this level
                    if (d.leader_commission_group_id.HasValue) {
                        if (d.leader_commission_group_id.Value == (int)Globals.rq4_commission_groups.district_sales_manager ||
                            d.leader_commission_group_id.Value == (int)Globals.rq4_commission_groups.trial_district_sales_manager)
                            BSRDataItems = (BSRData != null) ? BSRData.Where(b => b.id_field == district_id).ToList() : null;
                        else
                            // this manager isn't in the correct commission group and won't be processed - make sure any existing commissions are cleaned up for this district
                            DAL.DeleteAllExistingDetails(Globals.performance_target_level.District, ppd, district_id, true);
                    }

                    if (BSRDataItems != null && BSRDataItems.Count > 0) {
                        // valid district with data, calculate
                        int leader_comm_group_id = (optional_overrides != null && optional_overrides.commission_group_id > 0) ? optional_overrides.commission_group_id : (d.leader_commission_group_id.HasValue) ? d.leader_commission_group_id.Value : -1;

                        // GJK 9/5/2017 - now we can allow people to override the comp group and/or location we need to first clear down all existing commissions if those are being overridden, since it means a whole different set of payouts
                        if ((d.leader_commission_group_id.HasValue && leader_comm_group_id != d.leader_commission_group_id.Value) || district_id != d.district_id)
                            DAL.DeleteAllExistingDetails(Globals.performance_target_level.District, ppd, district_id, false);

                        decimal target_val = 0.00m;
                        decimal payout_val = 0.00m;
                        decimal payout_val_below_line = 0.00m;

                        // first get their base gross profit, this can increase/decrease based off the performance targets so we log the original value first
                        decimal base_gp = BSRDataItems.Where(b => b.metric_id == (int)Globals.BSR_metrics.GP_metric_id).Sum(b => b.total);

                        // store GP adjusted for commissionable values
                        decimal comm_gp = base_gp;

                        // store any manual adjustment total and add into GP
                        decimal manual_adj = CPL.GetManualAdjustment(Globals.performance_target_level.District, district_id);
                        comm_gp += manual_adj;

                        // any coupons have to be deducted
                        decimal coupons = CPL.GetCoupons(RQ4Data.Where(rq => rq.DistrictID == district_id).ToList());
                        comm_gp -= coupons;

                        // get any payouts that go into GP
                        comm_gp += CPL.ProcessPayoutsSKU(Globals.performance_target_level.District, ppd, district_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.all_types, true, leader_comm_group_id);
                        comm_gp += CPL.ProcessPayoutsCategory(Globals.performance_target_level.District, ppd, district_id, BSRDataItems, RQ4Data, true, leader_comm_group_id);

                        // load the tier for this level, so the gp % can be calculated
                        // get the GP margin value and % - first handle auto-allocation to the correct tier if this district doesn't have a tier (no default based on a total exists at this level so this will just create if a tier is not already allocated)
                        if (CPL.UpdateTierAssignment(Globals.performance_target_level.District, ppd, district_id, BSRDataItems))
                            CPL.ReloadLoadTiersForID(Globals.performance_target_level.District, ppd, district_id);  // tier assignment has changed - reload the list if required

                        Data.PerformanceTierDataItem got_tier = CPL.GetPerformanceTier(Globals.performance_target_level.District, district_id);

                        // no tier, raise an issue
                        if (got_tier == null)
                            CPL.AddToNotify("No Tier assigned to District " + d.district_name);

                        // if we have a valid tier, get any defined commissions cap value
                        decimal comm_cap = got_tier != null ? got_tier.commission_cap : 0.00m;

                        // total commissions running total, populated by the various modules
                        decimal res = 0.00M;
                        decimal participation_val = 0.00M;

                        decimal gp_margin_percent = 0.00m;
                        decimal gp_margin_amount = 0.00m;
                        //decimal team_bonus_payout_val_ignore_min = 0.00m;
                        decimal box_attain_pct = 0.00m;
                        decimal team_bonus_total = 0.00m;

                        // accelerators and participation index was discontinued in October 2015 and replaced with target payouts, so only process them if we're running before then
                        Globals.accelerator_source_values sv = new Globals.accelerator_source_values();
                        if (ppd.start_date.Month < 10 && ppd.start_date.Year <= 2015) {
                            // get the GP margin value and %
                            gp_margin_percent = got_tier != null ? got_tier.gross_profit_margin : 0.00m;
                            gp_margin_amount = (comm_gp * gp_margin_percent);

                            // process accelerators
                            sv.gross_profit = gp_margin_amount;
                            sv.commission_total_month = 0.00m;
                            sv.BSRDataItems = BSRDataItems;
                            sv.BSRDataItemsMonthly = BSRDataItems;
                            target_val = CPL.ProcessAccelerators(Globals.performance_target_level.District, ppd, district_id, sv, leader_comm_group_id);
                            res += target_val;

                            // run participation index
                            // return value from above will be the adjusted GP (or zero if it didn't process anything), now pass that in so if they need to paid into GP they will be
                            participation_val = CPL.ProcessParticipationIndex(Globals.performance_target_level.District, ppd.pay_period_id, district_id, (target_val > 0 ? target_val : gp_margin_amount), total_stores);
                            res += participation_val;
                        }
                        else {
                            // run the target payouts module, a team bonus payout could occur and if that bonus is to be paid out regardless of min boxes then the team bonus total won't be in the result of the method it will be in the seperate variable instead
                            Data.TargetPayoutResult tp_res = CPL.ProcessTargetPayouts(Globals.performance_target_level.District, ppd, district_id, BSRDataItems, comm_gp, comm_gp, leader_comm_group_id);
                            target_val = tp_res.total;
                            box_attain_pct = tp_res.box_attain_percent;
                            res += target_val;

                            // run monthly bonus payouts module, this has to be called here so the sub-total from this won't be included in the multiplier
                            Data.TargetPayoutResult tp_tb_res = CPL.ProcessTeamBonuses(Globals.performance_target_level.District, ppd, ppd, district_id, BSRDataItems, comm_gp, comm_gp, district_id, leader_comm_group_id);
                            //team_bonus_payout_val_ignore_min = tp_tb_res.team_bonus_payout_ignore_min;
                            team_bonus_total = tp_tb_res.total;
                            res += team_bonus_total;
                        }

                        // SKU-based payouts - get any that will be blocked if min target not met
                        payout_val = CPL.ProcessPayoutsSKU(Globals.performance_target_level.District, ppd, district_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.obey_min_checks, false, leader_comm_group_id);
                        res += payout_val;

                        // SKU-based payouts - get any that will always be paid regardless of min check
                        payout_val_below_line = CPL.ProcessPayoutsSKU(Globals.performance_target_level.District, ppd, district_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.ignore_min_checks, false, leader_comm_group_id);

                        // Category-based payouts
                        payout_val = CPL.ProcessPayoutsCategory(Globals.performance_target_level.District, ppd, district_id, BSRDataItems, RQ4Data, false, leader_comm_group_id);
                        res += payout_val;

                        // KPIs
                        decimal kpi_total = CPL.ProcessKPI(Globals.performance_target_level.District, ppd.pay_period_id, district_id, BSRDataItems);
                        res += kpi_total;

                        // check if commissions cap is active and apply if it is
                        if (comm_cap > 0 && res > comm_cap)
                            res = comm_cap;

                        // save the results
                        Data.CommissionValues cv = new Data.CommissionValues();
                        cv.pay_period_id = ppd.pay_period_id;
                        cv.entity_id = d.district_id;
                        cv.region_id = d.region_id;
                        cv.region_name = d.region_name;
                        cv.district_id = d.district_id;
                        cv.district_name = d.district_name;
                        cv.employee_id = (d.district_leader_id.HasValue) ? d.district_leader_id.Value : 0;
                        cv.base_gross_profit = base_gp;
                        cv.commission_gross_profit = comm_gp;
                        cv.gross_profit_margin_percent = gp_margin_percent;
                        cv.gross_profit_margin = gp_margin_amount;
                        cv.participation_total = participation_val;
                        cv.manual_adjustment = manual_adj;
                        cv.coupons = coupons;
                        cv.kpi_total = kpi_total;

                        // GJK 6/28/2016: the min payout logic is way out of date now and hasn't been used in over a year, bypassing it now
                        cv.commission_total = res;

                        // only set the total commissions for these if min target met, if there is no min then it will return true
                        //cv.commission_total = CPL.HandleMinBoxPayout(Globals.performance_target_level.District, ppd, district_id, BSRDataItems, res, comm_gp, out cv.current_boxes_total, out cv.min_boxes_total);

                        // any team bonus amount that should be paid regardless of min check
                        //cv.commission_total += team_bonus_payout_val_ignore_min;

                        // always set the total commissions for these payouts
                        cv.commission_total += payout_val_below_line;

                        // GJK 6/1/2016 - new hack to handle special multiplier payout
                        //if (d.district_leader_id.HasValue)
                        //    cv.commission_total += CPL.ApplyMultiplier(Globals.performance_target_level.District, ppd, district_id, BSRDataItems, box_attain_pct, cv.commission_total, false, d.district_leader_id.Value);

                        // run accelerators module if one exists for this pay period
                        if (CPL.AcceleratorsExistForPayPeriod(ppd)) {
                            sv.gross_profit = comm_gp;
                            sv.commission_total_month = cv.commission_total;
                            sv.BSRDataItems = BSRDataItems;
                            sv.BSRDataItemsMonthly = BSRDataItems;
                            res = CPL.ProcessAccelerators(Globals.performance_target_level.District, ppd, district_id, sv, leader_comm_group_id);
                            if (res > 0)
                                cv.commission_total += res;
                        }

                        DAL.SaveCommissionsValuesHeader(Globals.performance_target_level.District, cv);
                        DAL.Commit();
                    }
                }
                catch (Exception err) {
                    CPL.AddToErrors(String.Format("ProcessDistricts - unexpected error occured processing district {0} : {1}", d.district_name, err.Message));
                }
            });

            // log last run date for DL's in this pay period - saves to commissions_configuration but only if we didn't run for a single person since that's usually a re-calculation and we don't want everyone to show as being updated
            if (leader_rq_id <= 0)
                DAL.SaveLastRunDate(Globals.performance_target_level.District, ppd.pay_period_id, DateTime.Now);
        }

        #endregion

        #region Region Leader processing

        private void ProcessRegions(Data.commissions_pay_periods ppd, List<Data.InvoiceItems> RQ4Data, List<Data.BSRitems> BSRData, int leader_rq_id = -1, Globals.override_parameters optional_overrides = null) {
            CPL.LoadCommissionsConfig(Globals.performance_target_level.Region, ppd);

            var regions = CPL.GetActiveStores(Globals.performance_target_level.Region, BSRData).GroupBy(s => s.RegionID).Select(s => new { region_id = s.Key, region_name = s.Max(r => r.RegionName), region_leader_id = s.Max(r => r.RegionManagerID), leader_commission_group_id = s.Max(d => d.RegionManagerCommissionGroupID), total_districts = s.GroupBy(d => d.DistrictID).Count() }).ToList();

            if (leader_rq_id > 0)
                regions = regions.Where(r => r.region_leader_id == leader_rq_id).ToList();

            regions.ForEach(r => {
                try {
                    int region_id = (optional_overrides != null && optional_overrides.location_id > 0) ? optional_overrides.location_id : r.region_id;
                    int total_districts = r.total_districts;
                    List<Data.BSRitems> BSRDataItems = null;

                    // GJK 7/12/2016 - all region leaders now have a commission group, this is to deal with any RLs that have been promoted upward but left assigned to their original region, we now delete any comp for this region if the assigned leader isn't to be paid at this level
                    if (r.leader_commission_group_id.HasValue) {
                        if (r.leader_commission_group_id.Value == (int)Globals.rq4_commission_groups.regional_sales_director || 
                            r.leader_commission_group_id.Value == (int)Globals.rq4_commission_groups.business_territory_manager ||
                            r.leader_commission_group_id.Value == (int)Globals.rq4_commission_groups.trial_regional_sales_director)
                            BSRDataItems = (BSRData != null) ? BSRData.Where(b => b.id_field == region_id).ToList() : null;
                        else
                            // this manager isn't in the correct commission group and won't be processed - make sure any existing commissions are cleaned up for this region
                            DAL.DeleteAllExistingDetails(Globals.performance_target_level.Region, ppd, region_id, true);
                    }

                    if (BSRDataItems != null && BSRDataItems.Count > 0) {
                        // valid region with data, calculate
                        int leader_comm_group_id = (optional_overrides != null && optional_overrides.commission_group_id > 0) ? optional_overrides.commission_group_id : (r.leader_commission_group_id.HasValue) ? r.leader_commission_group_id.Value : -1;

                        // GJK 9/5/2017 - now we can allow people to override the comp group and/or location we need to first clear down all existing commissions if those are being overridden, since it means a whole different set of payouts
                        if ((r.leader_commission_group_id.HasValue && leader_comm_group_id != r.leader_commission_group_id.Value) || region_id != r.region_id)
                            DAL.DeleteAllExistingDetails(Globals.performance_target_level.Region, ppd, region_id, false);

                        decimal target_val = 0.00m;
                        decimal payout_val = 0.00m;
                        decimal payout_val_below_line = 0.00m;

                        // first get their base gross profit, this can increase/decrease based off the performance targets so we log the original value first
                        decimal base_gp = BSRDataItems.Where(b => b.metric_id == (int)Globals.BSR_metrics.GP_metric_id).Sum(b => b.total);

                        // store GP adjusted for commissionable values
                        decimal comm_gp = base_gp;

                        // store any manual adjustment total and add into GP
                        decimal manual_adj = CPL.GetManualAdjustment(Globals.performance_target_level.Region, region_id);
                        comm_gp += manual_adj;

                        // any coupons have to be deducted
                        decimal coupons = CPL.GetCoupons(RQ4Data.Where(rq => rq.RegionID == region_id).ToList());
                        comm_gp -= coupons;

                        // get any payouts that go into GP
                        comm_gp += CPL.ProcessPayoutsSKU(Globals.performance_target_level.Region, ppd, region_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.all_types, true, leader_comm_group_id);
                        comm_gp += CPL.ProcessPayoutsCategory(Globals.performance_target_level.Region, ppd, region_id, BSRDataItems, RQ4Data, true, leader_comm_group_id);

                        // load the tier for this level, so the gp % can be calculated
                        // get the GP margin value and % - first handle auto-allocation to the correct tier if this region doesn't have a tier (no default based on a total exists at this level so this will just create if a tier is not already allocated)
                        if (CPL.UpdateTierAssignment(Globals.performance_target_level.Region, ppd, region_id, BSRDataItems))
                            // tier assignment has changed - reload the list if required
                            CPL.ReloadLoadTiersForID(Globals.performance_target_level.Region, ppd, region_id);  // tier assignment has changed - reload the list if required

                        Data.PerformanceTierDataItem got_tier = CPL.GetPerformanceTier(Globals.performance_target_level.Region, region_id);

                        // no tier, raise an issue
                        if (got_tier == null)
                            CPL.AddToNotify("No Tier assigned to Region " + r.region_name);

                        // get the GP margin value and %
                        decimal gp_margin_percent = 0.00m;
                        decimal gp_margin_amount = 0.00m;

                        // if we have a valid tier, get any defined commissions cap value
                        decimal comm_cap = got_tier != null ? got_tier.commission_cap : 0.00m;

                        // total commissions running total, populated by the various modules
                        decimal res = 0.00M;
                        decimal participation_val = 0.00M;
                        //decimal team_bonus_payout_val_ignore_min = 0.00m;
                        decimal box_attain_pct = 0.00m;
                        decimal team_bonus_total = 0.00m;

                        // accelerators and participation index was discontinued in October 2015 and replaced with target payouts, so only process them if we're running before then
                        Globals.accelerator_source_values sv = new Globals.accelerator_source_values();
                        if (ppd.start_date.Month < 10 && ppd.start_date.Year <= 2015) {
                            gp_margin_percent = got_tier != null ? got_tier.gross_profit_margin : 0.00m;
                            gp_margin_amount = (comm_gp * gp_margin_percent);

                            // process accelerators
                            sv.gross_profit = comm_gp;
                            sv.commission_total_month = 0.00m;
                            sv.BSRDataItems = BSRDataItems;
                            sv.BSRDataItemsMonthly = BSRDataItems;
                            target_val = CPL.ProcessAccelerators(Globals.performance_target_level.Region, ppd, region_id, sv, leader_comm_group_id);
                            res += target_val;

                            // run participation index
                            // return value from above will be the adjusted GP (or zero if it didn't process anything), now pass that in so if they need to paid into GP they will be
                            participation_val = CPL.ProcessParticipationIndex(Globals.performance_target_level.Region, ppd.pay_period_id, region_id, (target_val > 0 ? target_val : gp_margin_amount), total_districts);
                            res += participation_val;
                        }
                        else {
                            // run the target payouts module, a team bonus payout could occur and if that bonus is to be paid out regardless of min boxes then the team bonus total won't be in the result of the method it will be in the seperate variable instead
                            Data.TargetPayoutResult tp_res = CPL.ProcessTargetPayouts(Globals.performance_target_level.Region, ppd, region_id, BSRDataItems, comm_gp, comm_gp, leader_comm_group_id);
                            target_val = tp_res.total;
                            box_attain_pct = tp_res.box_attain_percent;
                            res += target_val;

                            // run monthly bonus payouts module, this has to be called here so the sub-total from this won't be included in the multiplier
                            Data.TargetPayoutResult tp_tb_res = CPL.ProcessTeamBonuses(Globals.performance_target_level.Region, ppd, ppd, region_id, BSRDataItems, comm_gp, comm_gp, region_id, leader_comm_group_id);
                            //team_bonus_payout_val_ignore_min = tp_tb_res.team_bonus_payout_ignore_min;
                            team_bonus_total = tp_tb_res.total;
                            res += team_bonus_total;
                        }

                        // SKU-based payouts - get any that will be blocked if min target not met
                        payout_val = CPL.ProcessPayoutsSKU(Globals.performance_target_level.Region, ppd, region_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.obey_min_checks, false, leader_comm_group_id);
                        res += payout_val;

                        // SKU-based payouts - get any that will always be paid regardless of min check
                        payout_val_below_line = CPL.ProcessPayoutsSKU(Globals.performance_target_level.Region, ppd, region_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.ignore_min_checks, false, leader_comm_group_id);

                        // Category-based payouts
                        payout_val = CPL.ProcessPayoutsCategory(Globals.performance_target_level.Region, ppd, region_id, BSRDataItems, RQ4Data, false, leader_comm_group_id);
                        res += payout_val;

                        // KPIs
                        decimal kpi_total = CPL.ProcessKPI(Globals.performance_target_level.Region, ppd.pay_period_id, region_id, BSRDataItems);
                        res += kpi_total;

                        // check if commissions cap is active and apply if it is
                        if (comm_cap > 0 && res > comm_cap)
                            res = comm_cap;

                        // save the results
                        Data.CommissionValues cv = new Data.CommissionValues();
                        cv.pay_period_id = ppd.pay_period_id;
                        cv.entity_id = r.region_id;
                        cv.region_id = r.region_id;
                        cv.region_name = r.region_name;
                        cv.employee_id = (r.region_leader_id.HasValue) ? r.region_leader_id.Value : 0;
                        cv.base_gross_profit = base_gp;
                        cv.commission_gross_profit = comm_gp;
                        cv.gross_profit_margin_percent = gp_margin_percent;
                        cv.gross_profit_margin = gp_margin_amount;
                        cv.participation_total = participation_val;
                        cv.manual_adjustment = manual_adj;
                        cv.coupons = coupons;
                        cv.kpi_total = kpi_total;

                        // GJK 6/28/2016: the min payout logic is way out of date now and hasn't been used in over a year, bypassing it now
                        cv.commission_total = res;

                        // only set the total commissions for these if min target met, if there is no min then it will return true
                        //cv.commission_total = CPL.HandleMinBoxPayout(Globals.performance_target_level.Region, ppd, region_id, BSRDataItems, res, comm_gp, out cv.current_boxes_total, out cv.min_boxes_total);

                        // any team bonus amount that should be paid regardless of min check
                        //cv.commission_total += team_bonus_payout_val_ignore_min;

                        // always set the total commissions for these payouts
                        cv.commission_total += payout_val_below_line;

                        // GJK 6/1/2016 - new hack to handle special multiplier payout
                        //if (r.region_leader_id.HasValue)
                        //    cv.commission_total += CPL.ApplyMultiplier(Globals.performance_target_level.Region, ppd, region_id, BSRDataItems, box_attain_pct, cv.commission_total, false, r.region_leader_id.Value);

                        // run accelerators module if one exists for this pay period
                        if (CPL.AcceleratorsExistForPayPeriod(ppd)) {
                            sv.gross_profit = comm_gp;
                            sv.commission_total_month = cv.commission_total;
                            sv.BSRDataItems = BSRDataItems;
                            sv.BSRDataItemsMonthly = BSRDataItems;
                            res = CPL.ProcessAccelerators(Globals.performance_target_level.Region, ppd, region_id, sv, leader_comm_group_id);
                            if (res > 0)
                                cv.commission_total += res;
                        }

                        DAL.SaveCommissionsValuesHeader(Globals.performance_target_level.Region, cv);
                        DAL.Commit();
                    }
                }
                catch (Exception err) {
                    CPL.AddToErrors(String.Format("ProcessRegions - unexpected error occured processing region {0} : {1}", r.region_name, err.Message));
                }
            });

            // log last run date for RL's in this pay period - saves to commissions_configuration but only if we didn't run for a single person since that's usually a re-calculation and we don't want everyone to show as being updated
            if (leader_rq_id <= 0)
                DAL.SaveLastRunDate(Globals.performance_target_level.Region, ppd.pay_period_id, DateTime.Now);
        }
        #endregion

        #region Channel/Area Leader processing

        private void ProcessChannels(Data.commissions_pay_periods ppd, List<Data.InvoiceItems> RQ4Data, List<Data.BSRitems> BSRData, int leader_rq_id = -1, Globals.override_parameters optional_overrides = null) {
            CPL.LoadCommissionsConfig(Globals.performance_target_level.Channel, ppd);

            var channels = CPL.GetActiveStores(Globals.performance_target_level.Channel, BSRData).GroupBy(s => s.ChannelID).Select(s => new { channel_id = s.Key, channel_name = s.Max(r => r.ChannelName), channel_leader_id = s.Max(r => r.ChannelLeaderID), leader_commission_group_id = s.Max(d => d.AreaManagerCommissionGroupID), total_regions = s.GroupBy(d => d.RegionID).Count() }).ToList();

            if (leader_rq_id > 0)
                channels = channels.Where(c => c.channel_leader_id == leader_rq_id).ToList();

            channels.ForEach(c => {
                try {
                    int channel_id = (optional_overrides != null && optional_overrides.location_id > 0) ? optional_overrides.location_id : c.channel_id;
                    int total_regions = c.total_regions;
                    List<Data.BSRitems> BSRDataItems = null;

                    // GJK 7/12/2016 - all channel/area leaders now have a commission group, this is to deal with any ALs that have been promoted upward but left assigned to their original area, we now delete any comp for this area if the assigned leader isn't to be paid at this level
                    if (c.leader_commission_group_id.HasValue) {
                        if (c.leader_commission_group_id.Value == (int)Globals.rq4_commission_groups.area_vice_president || c.leader_commission_group_id.Value == (int)Globals.rq4_commission_groups.east_west_area_vice_president || c.leader_commission_group_id.Value == (int)Globals.rq4_commission_groups.business_sales_director)
                            BSRDataItems = (BSRData != null) ? BSRData.Where(b => b.id_field == channel_id).ToList() : null;
                        else
                            // this manager isn't in the correct commission group and won't be processed - make sure any existing commissions are cleaned up for this region
                            DAL.DeleteAllExistingDetails(Globals.performance_target_level.Channel, ppd, channel_id, true);
                    }

                    if (BSRDataItems != null && BSRDataItems.Count > 0) {
                        // valid region with data, calculate
                        int leader_comm_group_id = (optional_overrides != null && optional_overrides.commission_group_id > 0) ? optional_overrides.commission_group_id : (c.leader_commission_group_id.HasValue) ? c.leader_commission_group_id.Value : -1;

                        // GJK 9/5/2017 - now we can allow people to override the comp group and/or location we need to first clear down all existing commissions if those are being overridden, since it means a whole different set of payouts
                        if ((c.leader_commission_group_id.HasValue && leader_comm_group_id != c.leader_commission_group_id.Value) || channel_id != c.channel_id)
                            DAL.DeleteAllExistingDetails(Globals.performance_target_level.Channel, ppd, channel_id, false);

                        decimal target_val = 0.00m;
                        decimal payout_val = 0.00m;
                        decimal payout_val_below_line = 0.00m;

                        // first get their base gross profit, this can increase/decrease based off the performance targets so we log the original value first
                        decimal base_gp = BSRDataItems.Where(b => b.metric_id == (int)Globals.BSR_metrics.GP_metric_id).Sum(b => b.total);

                        // store GP adjusted for commissionable values
                        decimal comm_gp = base_gp;

                        // store any manual adjustment total and add into GP
                        decimal manual_adj = CPL.GetManualAdjustment(Globals.performance_target_level.Channel, channel_id);
                        comm_gp += manual_adj;

                        // any coupons have to be deducted
                        decimal coupons = CPL.GetCoupons(RQ4Data.Where(rq => rq.ChannelID == channel_id).ToList());
                        comm_gp -= coupons;

                        // get any payouts that go into GP
                        comm_gp += CPL.ProcessPayoutsSKU(Globals.performance_target_level.Channel, ppd, channel_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.all_types, true, leader_comm_group_id);
                        comm_gp += CPL.ProcessPayoutsCategory(Globals.performance_target_level.Channel, ppd, channel_id, BSRDataItems, RQ4Data, true, leader_comm_group_id);

                        // load the tier for this level, so the gp % can be calculated
                        // get the GP margin value and % - first handle auto-allocation to the correct tier if this region doesn't have a tier (no default based on a total exists at this level so this will just create if a tier is not already allocated)
                        if (CPL.UpdateTierAssignment(Globals.performance_target_level.Channel, ppd, channel_id, BSRDataItems))
                            // tier assignment has changed - reload the list if required
                            CPL.ReloadLoadTiersForID(Globals.performance_target_level.Channel, ppd, channel_id);  // tier assignment has changed - reload the list if required

                        Data.PerformanceTierDataItem got_tier = CPL.GetPerformanceTier(Globals.performance_target_level.Channel, channel_id);

                        // no tier, raise an issue
                        if (got_tier == null)
                            CPL.AddToNotify("No Tier assigned to Channel " + c.channel_name);

                        // get the GP margin value and %
                        decimal gp_margin_percent = 0.00m; // got_tier != null ? got_tier.gross_profit_margin : 0.00m;
                        decimal gp_margin_amount = 0.00m; // (comm_gp * gp_margin_percent);

                        // if we have a valid tier, get any defined commissions cap value
                        decimal comm_cap = got_tier != null ? got_tier.commission_cap : 0.00m;

                        // total commissions running total, populated by the various modules
                        decimal res = 0.00M;

                        // run the target payouts module, a team bonus payout could occur and if that bonus is to be paid out regardless of min boxes then the team bonus total won't be in the result of the method it will be in the seperate variable instead
                        Data.TargetPayoutResult tp_res = CPL.ProcessTargetPayouts(Globals.performance_target_level.Channel, ppd, channel_id, BSRDataItems, comm_gp, comm_gp, leader_comm_group_id);
                        target_val = tp_res.total;
                        res += target_val;

                        // run monthly bonus payouts module, this has to be called here so the sub-total from this won't be included in the multiplier
                        Data.TargetPayoutResult tp_tb_res = CPL.ProcessTeamBonuses(Globals.performance_target_level.Channel, ppd, ppd, channel_id, BSRDataItems, comm_gp, comm_gp, channel_id, leader_comm_group_id);
                        //decimal team_bonus_payout_val_ignore_min = tp_tb_res.team_bonus_payout_ignore_min;
                        res += tp_tb_res.total;

                        // SKU-based payouts - get any that will be blocked if min target not met
                        payout_val = CPL.ProcessPayoutsSKU(Globals.performance_target_level.Channel, ppd, channel_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.obey_min_checks, false, leader_comm_group_id);
                        res += payout_val;

                        // SKU-based payouts - get any that will always be paid regardless of min check
                        payout_val_below_line = CPL.ProcessPayoutsSKU(Globals.performance_target_level.Channel, ppd, channel_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.ignore_min_checks, false, leader_comm_group_id);

                        // Category-based payouts
                        payout_val = CPL.ProcessPayoutsCategory(Globals.performance_target_level.Channel, ppd, channel_id, BSRDataItems, RQ4Data, false, leader_comm_group_id);
                        res += payout_val;

                        // KPIs
                        decimal kpi_total = CPL.ProcessKPI(Globals.performance_target_level.Channel, ppd.pay_period_id, channel_id, BSRDataItems);
                        res += kpi_total;

                        // check if commissions cap is active and apply if it is
                        if (comm_cap > 0 && res > comm_cap)
                            res = comm_cap;

                        // save the results
                        Data.CommissionValues cv = new Data.CommissionValues();
                        cv.pay_period_id = ppd.pay_period_id;
                        cv.entity_id = c.channel_id;
                        cv.channel_id = c.channel_id;
                        cv.channel_name = c.channel_name;
                        cv.employee_id = c.channel_leader_id;
                        cv.base_gross_profit = base_gp;
                        cv.commission_gross_profit = comm_gp;
                        cv.gross_profit_margin_percent = gp_margin_percent;
                        cv.gross_profit_margin = gp_margin_amount;
                        cv.manual_adjustment = manual_adj;
                        cv.coupons = coupons;
                        cv.kpi_total = kpi_total;

                        // GJK 6/28/2016: the min payout logic is way out of date now and hasn't been used in over a year, bypassing it now
                        cv.commission_total = res;

                        // only set the total commissions for these if min target met, if there is no min then it will return true
                        //cv.commission_total = CPL.HandleMinBoxPayout(Globals.performance_target_level.Channel, ppd, channel_id, BSRDataItems, res, comm_gp, out cv.current_boxes_total, out cv.min_boxes_total);

                        // any team bonus amount that should be paid regardless of min check
                        //cv.commission_total += team_bonus_payout_val_ignore_min;

                        // always set the total commissions for these payouts
                        cv.commission_total += payout_val_below_line;

                        // GJK 6/1/2016 - new hack to handle special multiplier payout
                        //cv.commission_total += CPL.ApplyMultiplier(Globals.performance_target_level.Channel, ppd, channel_id, BSRDataItems, tp_res.box_attain_percent, cv.commission_total, false, c.channel_leader_id);

                        // run accelerators module if any exist for this pay period
                        if (CPL.AcceleratorsExistForPayPeriod(ppd)) {
                            Globals.accelerator_source_values sv = new Globals.accelerator_source_values();
                            sv.gross_profit = comm_gp;
                            sv.commission_total_month = cv.commission_total;
                            sv.BSRDataItems = BSRDataItems;
                            sv.BSRDataItemsMonthly = BSRDataItems;
                            res = CPL.ProcessAccelerators(Globals.performance_target_level.Channel, ppd, channel_id, sv, leader_comm_group_id);
                            if (res > 0)
                                cv.commission_total += res;
                        }

                        DAL.SaveCommissionsValuesHeader(Globals.performance_target_level.Channel, cv);
                        DAL.Commit();
                    }
                }
                catch (Exception err) {
                    CPL.AddToErrors(String.Format("ProcessChannels - unexpected error occured processing channel {0} : {1}", c.channel_name, err.Message));
                }
            });

            // log last run date for CL's in this pay period - saves to commissions_configuration but only if we didn't run for a single person since that's usually a re-calculation and we don't want everyone to show as being updated
            if (leader_rq_id <= 0)
                DAL.SaveLastRunDate(Globals.performance_target_level.Channel, ppd.pay_period_id, DateTime.Now);
        }
        #endregion

        #region Business Consultant processing
        /*  GJK 2/10/2017 - not used now, since business consultants are now assigned to different levels. Originally they were just assigned to stores, but now a whole new structure has been implemented.
            So now we just let comp handle the business reps as part of the regular loops above, using their comp group to identify them where needed         

        private void ProcessBusinessConsultants(Data.commissions_pay_periods ppd, List<Data.InvoiceItems> RQ4Data, List<Data.BSRitems> BSRData, int leader_rq_id = -1) {
            CPL.LoadCommissionsConfig(Globals.performance_target_level.BusinessConsultant, ppd);

            List<Data.StoreItems> stores = (leader_rq_id > 0) ? CPL.GetActiveStores(Globals.performance_target_level.Store, BSRData).Where(s => s.StoreManagerID == leader_rq_id).ToList() : CPL.GetActiveStores(Globals.performance_target_level.Store, BSRData);

            // now loop the filtered business SC stores - by not passing any BSR data this method will just return the store list
            stores.Where(s => s.StoreTypeID == (int)Globals.store_types.business).ToList().ForEach(s => {
                try {
                    int store_id = s.StoreID;
                    List<Data.BSRitems> BSRDataItems = null;

                    // only get BSR data if this leader of this store is in the correct commission group
                    if (s.StoreManagerCommissionGroupID.HasValue) {
                        if (s.StoreManagerCommissionGroupID.Value == (int)Globals.rq4_commission_groups.business_consultant) {
                            BSRDataItems = (BSRData != null) ? BSRData.Where(b => b.id_field == store_id).ToList() : null;
                        }
                        else
                            if (s.StoreManagerCommissionGroupID.Value != (int)Globals.rq4_commission_groups.store_manager)  // store managers get paid in a different module - leave their comp alone for this store if we happen to get here
                                // this store manager isn't in the correct commission group and won't be processed - make sure any existing commissions are cleaned up for this store
                                DAL.DeleteAllExistingDetails(Globals.performance_target_level.Store, ppd, store_id, true);
                    }

                    if (BSRDataItems != null && BSRDataItems.Count > 0) {
                        // valid region with data, calculate
                        int leader_comm_group_id = (s.StoreManagerCommissionGroupID.HasValue) ? s.StoreManagerCommissionGroupID.Value : -1;
                        decimal target_val = 0.00m;
                        decimal payout_val = 0.00m;
                        decimal payout_val_below_line = 0.00m;

                        // first get their base gross profit, this can increase/decrease based off the performance targets so we log the original value first
                        decimal base_gp = BSRDataItems.Where(b => b.metric_id == (int)Globals.BSR_metrics.GP_metric_id).Sum(b => b.total);

                        // store GP adjusted for commissionable values
                        decimal comm_gp = base_gp;

                        // store any manual adjustment total and add into GP
                        decimal manual_adj = CPL.GetManualAdjustment(Globals.performance_target_level.BusinessSalesConsultant, store_id);
                        comm_gp += manual_adj;

                        // any coupons have to be deducted
                        decimal coupons = CPL.GetCoupons(RQ4Data.Where(rq => rq.StoreID == store_id).ToList());
                        comm_gp -= coupons;

                        // get any payouts that go into GP
                        comm_gp += CPL.ProcessPayoutsSKU(Globals.performance_target_level.BusinessSalesConsultant, ppd, store_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.all_types, true, leader_comm_group_id);
                        comm_gp += CPL.ProcessPayoutsCategory(Globals.performance_target_level.BusinessSalesConsultant, ppd, store_id, BSRDataItems, RQ4Data, true, leader_comm_group_id);

                        // get the GP margin value and %
                        decimal gp_margin_percent = 0.00m;
                        decimal gp_margin_amount = 0.00m;

                        // total commissions running total, populated by the various modules
                        decimal res = 0.00M;
                        decimal participation_val = 0.00M;

                        // load the tier for this level, so the gp % can be calculated
                        // get the GP margin value and % - first handle auto-allocation to the correct tier if this store doesn't have a tier (no default based on a total exists at this level so this will just create if a tier is not already allocated)
                        if (CPL.UpdateTierAssignment(Globals.performance_target_level.BusinessSalesConsultant, ppd, store_id, BSRDataItems))
                            CPL.ReloadLoadTiersForID(Globals.performance_target_level.BusinessSalesConsultant, ppd, store_id);  // tier assignment has changed - reload the list if required

                        Data.PerformanceTierDataItem got_tier = CPL.GetPerformanceTier(Globals.performance_target_level.BusinessSalesConsultant, store_id);

                        // no tier, raise an issue
                        if (got_tier == null)
                            CPL.AddToNotify("No Tier assigned to Store " + s.StoreName);

                        // run the target payouts module, a team bonus payout could occur and if that bonus is to be paid out regardless of min boxes then the team bonus total won't be in the result of the method it will be in the seperate variable instead
                        Data.TargetPayoutResult tp_res = CPL.ProcessTargetPayouts(Globals.performance_target_level.BusinessSalesConsultant, ppd, store_id, BSRDataItems, comm_gp, comm_gp, leader_comm_group_id);
                        target_val = tp_res.total;
                        decimal box_attain_pct = tp_res.box_attain_percent;
                        res += target_val;

                        // SKU-based payouts - get any that will be blocked if min target not met
                        payout_val = CPL.ProcessPayoutsSKU(Globals.performance_target_level.BusinessSalesConsultant, ppd, store_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.obey_min_checks, false, leader_comm_group_id);
                        res += payout_val;

                        // SKU-based payouts - get any that will always be paid regardless of min check
                        payout_val_below_line = CPL.ProcessPayoutsSKU(Globals.performance_target_level.BusinessSalesConsultant, ppd, store_id, BSRDataItems, RQ4Data, Globals.payout_sku_type.ignore_min_checks, false, leader_comm_group_id);

                        // Category-based payouts
                        payout_val = CPL.ProcessPayoutsCategory(Globals.performance_target_level.BusinessSalesConsultant, ppd, store_id, BSRDataItems, RQ4Data, false, leader_comm_group_id);
                        res += payout_val;

                        // save the results
                        Data.CommissionValues cv = new Data.CommissionValues();
                        cv.pay_period_id = ppd.pay_period_id;
                        cv.entity_id = s.StoreID;
                        cv.region_id = s.RegionID;
                        cv.region_name = s.RegionName;
                        cv.district_id = s.DistrictID;
                        cv.district_name = s.DistrictName;
                        cv.store_id = s.StoreID;
                        cv.store_name = s.StoreName;
                        cv.employee_id = s.StoreManagerID;
                        cv.base_gross_profit = base_gp;
                        cv.commission_gross_profit = comm_gp;
                        cv.gross_profit_margin_percent = gp_margin_percent;
                        cv.gross_profit_margin = gp_margin_amount;
                        cv.participation_total = participation_val;
                        cv.manual_adjustment = manual_adj;
                        cv.coupons = coupons;
                        cv.kpi_total = 0.00m;

                        // set the total commissions
                        cv.commission_total = res;

                        // always set the total commissions for these payouts
                        cv.commission_total += payout_val_below_line;

                        DAL.SaveCommissionsValuesHeader(Globals.performance_target_level.BusinessSalesConsultant, cv);
                        DAL.Commit();

                        // now pass a spiff through to the rep who created the lead, the rep ID is supposed to be in the serial number field and then group up to get total invoices to create a single payout which will be (amount * qty) linked to a payout category which holds the $ value
                        foreach (var invoices in RQ4Data.Where(inv => inv.EmployeeID == s.StoreManagerID && inv.Sku == "VECORB000326" && inv.SerialNumber.Length > 0).GroupBy(gp => gp.SerialNumber).Select(inv => new { rep_id = inv.Key, total_sold = inv.Sum(sum => sum.Quantity) }).ToList()) {
                            if (String.IsNullOrWhiteSpace(invoices.rep_id))
                                continue;

                            Data.EmployeeItems rep = CPL.GetActiveEmployees().FirstOrDefault(e => e.AccountDisabled == false && e.SpecialIdentifier == invoices.rep_id);
                            if (rep == null)
                                continue;

                            CPL.CreatePendingPayout(Globals.performance_target_level.Employee, ppd, Globals.commission_types.Payouts_cat, "B2B5", rep.IdNumber, invoices.total_sold);
                        }
                    }
                }
                catch (Exception err) {
                    CPL.AddToErrors(String.Format("ProcessBusinessSC - unexpected error occured processing business SC for store {0} : {1}", s.StoreName, err.Message));
                }
            });

            // log last run date for BSC's in this pay period - saves to commissions_configuration but only if we didn't run for a single person since that's usually a re-calculation and we don't want everyone to show as being updated
            if (leader_rq_id <= 0)
                DAL.SaveLastRunDate(Globals.performance_target_level.BusinessSalesConsultant, ppd.pay_period_id, DateTime.Now);
        }
        */
        #endregion

        #region list processing classes - so a threadpool can be used to run multiple lists in parallel

        private class EmployeeListProcessor {
            private Data.DAL _DAL;
            private CompPlanLogic _CPL;
            private ManualResetEvent _list_done;
            private Data.commissions_pay_periods _ppd;
            private Data.commissions_pay_periods _ppd_store;
            private List<Data.EmployeeItems> _employees;
            private List<Data.InvoiceItems> _RQ4Data;
            private List<Data.BSRitems> _BSRData;
            private List<Data.BSRitems> _BSRStoreMonthData;
            private Globals.override_parameters _optional_overrides = null;

            public EmployeeListProcessor(Data.DAL DAL_master, Data.GOL GOL_master, ManualResetEvent list_done, Data.commissions_pay_periods ppd, Data.commissions_pay_periods ppd_store, List<Data.EmployeeItems> employees, List<Data.InvoiceItems> RQ4Data, List<Data.BSRitems> BSRData, List<Data.BSRitems> BSRStoreMonthlyData, Globals.override_parameters optional_overrides = null) {
                _DAL = new Data.DAL(DAL_master.SQLServerConnectionString_live(), DAL_master.SQLServerConnectionString_readonly());
                _CPL = new CompPlanLogic(_DAL, GOL_master);
                _list_done = list_done;
                _ppd = ppd;
                _ppd_store = ppd_store;
                _employees = employees;
                _RQ4Data = RQ4Data;
                _BSRData = BSRData;
                _BSRStoreMonthData = BSRStoreMonthlyData;
                _optional_overrides = optional_overrides;
            }

            // Wrapper method for use with thread pool.
            public void RunComp(Object threadContext) {
                ProcessList();
                _list_done.Set();
            }

            private void ProcessList() {
                _employees.ForEach(e => {
                    try {
                        int emp_id = e.IdNumber;
                        int emp_store_id = (_optional_overrides != null && _optional_overrides.location_id > 0) ? _optional_overrides.location_id : e.StoreID;  // we are using summary BSR data, which means this store ID will be the reps default location from RQ4
                        int emp_comp_group_id = (_optional_overrides != null && _optional_overrides.commission_group_id > 0) ? _optional_overrides.commission_group_id : e.RQ4CommissionGroupID;
                        List<Data.BSRitems> BSRDataItems = null;

                        // validate the rep is in the correct location in RQ4 - i.e. not been left assigned to a closed store that's been moved to corporate...
                        if (e.RegionName == null || e.DistrictName == null)
                            _CPL.AddToNotify("The location for Store " + e.StoreName + " for Employee " + e.EmployeeName + " is invalid - has the store been moved under the Corporate region but the employee not re-assigned to an active store?  Commissions for this rep cannot be calculated until this is resolved");
                        else
                            BSRDataItems = (_BSRData != null) ? _BSRData.Where(b => b.id_field == emp_id).ToList() : null;

                        if (BSRDataItems != null && BSRDataItems.Count > 0) {
                            // GJK 9/5/2017 - now we can allow people to override the comp group and/or location we need to first clear down all existing commissions if those are being overridden, since it means a whole different set of payouts
                            if (emp_comp_group_id != e.RQ4CommissionGroupID || emp_store_id != e.StoreID)
                                _DAL.DeleteAllExistingDetails(Globals.performance_target_level.Employee, _ppd, emp_id, false);

                            // we're dealing with store-level data, but for selling store leaders [paid the same as reps] we need ALL their BSR data from any location they lead
                            // leader ID is in the info_id field in BSR (ID_field will be store ID) - try to get BSR data by that first, if we do then this is the the data for the store(s) they are assigned to
                            List <Data.BSRitems> BSRStoreMonthlyData = _BSRStoreMonthData.Where(bsr => bsr.info_id == emp_id).ToList();
                            // if that fails, just grab monthly data for the default location
                            if (BSRStoreMonthlyData.Count == 0)
                                BSRStoreMonthlyData = _BSRStoreMonthData.Where(bsr => bsr.store_id == emp_store_id).ToList();

                            // valid employee with invoices, calculate everything but don't record a total commission if min box target not met
                            DateTime start_date = (e.startDate.HasValue) ? e.startDate.Value : DateTime.Now;

                            decimal target_val = 0.00m;
                            decimal payout_val = 0.00m;
                            decimal payout_val_below_line = 0.00m;

                            // first get their base gross profit, this can increase/decrease based off the performance targets so we log the original value first
                            decimal base_gp = BSRDataItems.Where(b => b.metric_id == (int)Globals.BSR_metrics.GP_metric_id).Sum(b => b.total);

                            // store GP adjusted for commissionable values
                            decimal comm_gp = base_gp;

                            // store any manual adjustment total and add into GP
                            decimal manual_adj = _CPL.GetManualAdjustment(Globals.performance_target_level.Employee, emp_id);
                            comm_gp += manual_adj;

                            // any coupons have to be deducted
                            decimal coupons = _CPL.GetCoupons(_RQ4Data.Where(rq => rq.EmployeeID == emp_id).ToList());
                            comm_gp -= coupons;

                            // get any payouts that go into GP
                            comm_gp += _CPL.ProcessPayoutsSKU(Globals.performance_target_level.Employee, _ppd, emp_id, BSRDataItems, _RQ4Data, Globals.payout_sku_type.all_types, true, emp_comp_group_id);
                            comm_gp += _CPL.ProcessPayoutsCategory(Globals.performance_target_level.Employee, _ppd, emp_id, BSRDataItems, _RQ4Data, true, e.RQ4CommissionGroupID);

                            // get the GP margin value and % - first handle auto-allocation to the correct tier based on box totals
                            if (_CPL.UpdateTierAssignment(Globals.performance_target_level.Employee, _ppd, emp_id, BSRDataItems))
                                _CPL.ReloadLoadTiersForID(Globals.performance_target_level.Employee, _ppd, emp_id);  // tier assignment has changed - reload the list if required

                            Data.PerformanceTierDataItem got_tier = _CPL.GetPerformanceTier(Globals.performance_target_level.Employee, emp_id);

                            // no tier, raise an issue
                            if (got_tier == null)
                                _CPL.AddToErrors("No Tier assigned to Employee " + e.EmployeeName);

                            decimal gp_margin_percent = got_tier != null ? got_tier.gross_profit_margin : 0.00m;
                            decimal gp_margin_amount = (comm_gp * gp_margin_percent);

                            // data validation
                            if (!e.startDate.HasValue)
                                _CPL.AddToNotify("Employee " + e.EmployeeName + " has no start date, so their eligibility for commissions cannot be determined!");

                            // if we have a valid tier, get any defined commissions cap value
                            decimal comm_cap = got_tier != null ? got_tier.commission_cap : 0.00m;

                            // load the commission GP for the entire month
                            decimal monthly_comm_gp = _CPL.GetMonthlyComissionableGP(Globals.performance_target_level.Employee, _ppd, emp_id, comm_gp);

                            // total commissions running total, populated by the various modules
                            decimal res = 0.00M;

                            // run the target payouts module, a team bonus payout could occur and if that bonus is to be paid out regardless of min boxes then the team bonus total won't be in the result of the method it will be in the seperate variable instead
                            Data.TargetPayoutResult tp_res = _CPL.ProcessTargetPayouts(Globals.performance_target_level.Employee, _ppd, emp_id, BSRDataItems, comm_gp, monthly_comm_gp, emp_comp_group_id, _ppd_store, emp_store_id);
                            target_val = tp_res.total;
                            res += target_val;

                            // run monthly bonus payouts module, this has to be called here so the sub-total from this won't be included in the first multiplier. If this is a selling SL we create a second multiplier just for TB
                            Data.TargetPayoutResult tp_tb_res = _CPL.ProcessTeamBonuses(Globals.performance_target_level.Employee, _ppd, _ppd_store, emp_id, BSRStoreMonthlyData, comm_gp, monthly_comm_gp, emp_store_id, emp_comp_group_id);
                            res += tp_tb_res.total;

                            // run payouts module, first the SKU categories then by category (category is for eSec and Asurion)
                            // target value from above will be the adjusted GP (or zero if it didn't process anything), now pass that to the payouts so if they need to paid into GP they will be
                            // SKU-based payouts - get any that will be blocked if min target not met
                            payout_val = _CPL.ProcessPayoutsSKU(Globals.performance_target_level.Employee, _ppd, emp_id, BSRDataItems, _RQ4Data, Globals.payout_sku_type.obey_min_checks, false, emp_comp_group_id);
                            res += payout_val;

                            // SKU-based payouts - get any that will always be paid regardless of min check
                            payout_val_below_line = _CPL.ProcessPayoutsSKU(Globals.performance_target_level.Employee, _ppd, emp_id, BSRDataItems, _RQ4Data, Globals.payout_sku_type.ignore_min_checks, false, emp_comp_group_id);

                            // Category-based payouts
                            payout_val = _CPL.ProcessPayoutsCategory(Globals.performance_target_level.Employee, _ppd, emp_id, BSRDataItems, _RQ4Data, false, emp_comp_group_id);
                            res += payout_val;

                            // KPIs
                            decimal kpi_total = _CPL.ProcessKPI(Globals.performance_target_level.Employee, _ppd.pay_period_id, emp_id, BSRDataItems);
                            res += kpi_total;

                            // check if commissions cap is active and apply if it is
                            if (comm_cap > 0 && res > comm_cap)
                                res = comm_cap;

                            // save the results
                            Data.CommissionValues cv = new Data.CommissionValues();
                            cv.pay_period_id = _ppd.pay_period_id;
                            cv.entity_id = e.IdNumber;
                            cv.region_id = e.RegionID;
                            cv.region_name = e.RegionName;
                            cv.district_id = e.DistrictID;
                            cv.district_name = e.DistrictName;
                            cv.store_id = e.StoreID;
                            cv.store_name = e.StoreName;
                            cv.employee_id = e.IdNumber;
                            cv.base_gross_profit = base_gp;
                            cv.commission_gross_profit = comm_gp;
                            cv.gross_profit_margin_percent = gp_margin_percent;
                            cv.gross_profit_margin = gp_margin_amount;
                            cv.performance_total = target_val;
                            cv.payout_total = payout_val;
                            cv.manual_adjustment = manual_adj;
                            cv.coupons = coupons;
                            cv.kpi_total = kpi_total;

                            // GJK 6/28/2016: the min payout logic is way out of date now and hasn't been used in over a year, bypassing it now
                            cv.commission_total = res;

                            // only set the total commissions for these if min target met, if not check for a minimum value instead
                            //cv.commission_total = CPL.HandleMinBoxPayout(Globals.performance_target_level.Employee, ppd, emp_id, BSRDataItems, res, comm_gp, out cv.current_boxes_total, out cv.min_boxes_total);

                            // any team bonus amount that should be paid regardless of min check
                            //cv.commission_total += team_bonus_payout_val_ignore_min;

                            // always set the total commissions for these payouts
                            cv.commission_total += payout_val_below_line;

                            // add in any existing MSO spiff payout - these are created outside this loop so have to be added in to the header total
                            cv.commission_total += _CPL.GetExistingMSOAmount(Globals.spiff_level.Consultant, _ppd.pay_period_id, emp_id);

                            // GJK 6/1/2016 - new hack to handle special multiplier payout
                            /*
                            if (emp_comp_group_id == (int)Globals.rq4_commission_groups.selling_store_manager) {
                                // selling SC's need their monthly store bonus breaking out from total comp otherwise we'd pay too much. Instead we create two multipliers. Regular SC's just get one multiplier for their total comp
                                decimal sub_tot_minus_team_bonus = (cv.commission_total - tp_tb_res.total);
                                cv.commission_total += CPL.ApplyMultiplier(Globals.performance_target_level.Employee, ppd, emp_id, BSRDataItems, tp_res.box_attain_percent, sub_tot_minus_team_bonus);
                                cv.commission_total += CPL.ApplyMultiplier(Globals.performance_target_level.Employee, ppd, emp_id, BSRDataItems, tp_tb_res.box_attain_percent, tp_tb_res.total, true);
                            }
                            else
                                cv.commission_total += CPL.ApplyMultiplier(Globals.performance_target_level.Employee, ppd, emp_id, BSRDataItems, tp_res.box_attain_percent, cv.commission_total);
                            */

                            // run accelerators module if one is define for this pay period
                            if (_CPL.AcceleratorsExistForPayPeriod(_ppd)) {
                                Globals.accelerator_source_values sv = new Globals.accelerator_source_values();
                                sv.gross_profit = comm_gp;
                                sv.commission_total_month = _CPL.GetMonthlyComissionTotal(Globals.performance_target_level.Employee, _ppd, emp_id, cv.commission_total);
                                sv.commission_total_payperiod = cv.commission_total;
                                sv.BSRDataItems = BSRDataItems;
                                sv.BSRDataItemsMonthly = BSRStoreMonthlyData;
                                res = _CPL.ProcessAccelerators(Globals.performance_target_level.Employee, _ppd, emp_id, sv, emp_comp_group_id);
                                if (res > 0)
                                    cv.commission_total += res;
                            }

                            _DAL.SaveCommissionsValuesHeader(Globals.performance_target_level.Employee, cv);
                            _DAL.Commit();
                        }
                    }
                    catch (Exception err) {
                        _CPL.AddToErrors(String.Format("EmployeeListProcessor:ProcessList Thread - unexpected error processing employee {0}, error is {1}", e.EmployeeName, err.Message));
                    }
                });
            }
        }
        #endregion
    }
}

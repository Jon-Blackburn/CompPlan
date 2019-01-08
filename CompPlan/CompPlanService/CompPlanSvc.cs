using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CompPlanService {
    public partial class CompPlanSvc : ServiceBase {
        ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        Thread _thread_sc;
        Thread _thread_bc;  // GJK 6/1/2016: added new SMB business consultant module to comp
        Thread _thread_sl;
        Thread _thread_dl;
        Thread _thread_rl;
        Thread _thread_cl;
        bool paused = false;
        string sqlconn_live = "";
        string sqlconn_readonly = "";

        private void WriteToEventLog(EventLogEntryType logtype, string msg) {
            String source = this.ServiceName;
            String log = "Application";
            if (!EventLog.SourceExists(source))
                EventLog.CreateEventSource(source, log);
            EventLog eLog = new EventLog();
            eLog.Source = source;
            eLog.WriteEntry(msg, logtype);
        }

        public CompPlanSvc() {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
            this.ServiceName = "CompPlan Service";
        }

        protected override void OnStart(string[] args) {
            sqlconn_live = System.Configuration.ConfigurationManager.ConnectionStrings["db_live"].ToString();
            sqlconn_readonly = System.Configuration.ConfigurationManager.ConnectionStrings["db_readonly"].ToString();

            // GJK 11/5/2015 - broke this down so each level runs in its own thread
            _thread_sc = new Thread(SCThreadProcessing);
            _thread_sc.Name = "CompPlan_SC";
            _thread_sc.IsBackground = true;
            _thread_sc.Priority = ThreadPriority.Normal;
            _thread_sc.Start();

            _thread_bc = new Thread(BCThreadProcessing);
            _thread_bc.Name = "CompPlan_BC";
            _thread_bc.IsBackground = true;
            _thread_bc.Priority = ThreadPriority.Normal;
            _thread_bc.Start();

            _thread_sl = new Thread(SLThreadProcessing);
            _thread_sl.Name = "CompPlan_SL";
            _thread_sl.IsBackground = true;
            _thread_sl.Priority = ThreadPriority.Normal;
            _thread_sl.Start();

            _thread_dl = new Thread(DLThreadProcessing);
            _thread_dl.Name = "CompPlan_DL";
            _thread_dl.IsBackground = true;
            _thread_dl.Priority = ThreadPriority.Normal;
            _thread_dl.Start();

            _thread_rl = new Thread(RLThreadProcessing);
            _thread_rl.Name = "CompPlan_RL";
            _thread_rl.IsBackground = true;
            _thread_rl.Priority = ThreadPriority.Normal;
            _thread_rl.Start();

            _thread_cl = new Thread(CLThreadProcessing);
            _thread_cl.Name = "CompPlan_CL";
            _thread_cl.IsBackground = true;
            _thread_cl.Priority = ThreadPriority.Normal;
            _thread_cl.Start();

            EventLog.WriteEntry("CompPlan Service Started using SQL connection " + sqlconn_live, EventLogEntryType.Information);
        }

        protected override void OnStop() {
            _shutdownEvent.Set();
            if (_thread_sc != null && !_thread_sc.Join(30000))
                _thread_sc.Abort();
            if (_thread_bc != null && !_thread_bc.Join(30000))
                _thread_bc.Abort(); 
            if (_thread_sl != null && !_thread_sl.Join(30000))
                _thread_sl.Abort();
            if (_thread_dl != null && !_thread_dl.Join(30000))
                _thread_dl.Abort();
            if (_thread_rl != null && !_thread_rl.Join(30000))
                _thread_rl.Abort();
            if (_thread_cl != null && !_thread_cl.Join(30000))
                _thread_cl.Abort();
            WriteToEventLog(EventLogEntryType.Information, "CompPlan Service Stopped");
        }

        protected override void OnPause() {
            paused = true;
            WriteToEventLog(EventLogEntryType.Information, "CompPlan Service Paused");
        }

        protected override void OnContinue() {
            paused = false;
            WriteToEventLog(EventLogEntryType.Information, "CompPlan Service Continued");
        }

        private void SCThreadProcessing() {
            CompPlanLib.CompPlanEngine CompPlan_SC = new CompPlanLib.CompPlanEngine(sqlconn_live, sqlconn_readonly);

            while (!_shutdownEvent.WaitOne(300000))  // wait for five minutes between each run
            {
                try {
                    if (!paused) {
                        WriteToEventLog(EventLogEntryType.Information, "Start calculating Employee commissions");
                        CompPlan_SC.ProcessCommissions(CompPlanLib.Globals.performance_target_level.Employee);
                        WriteToEventLog(EventLogEntryType.Information, "Completed calculating Employee commissions");
                    }
                    else
                        Thread.Sleep(10000);
                }
                catch (Exception ex) {
                    CompPlanLib.Tools.Mailer mailer = new CompPlanLib.Tools.Mailer();
                    mailer.SendEmail(null, "CompPlan Service has encountered an error during SC processing", ex.ToString());
                }
            }
        }

        private void BCThreadProcessing() {
            /*
            CompPlanLib.CompPlanEngine CompPlan_BC = new CompPlanLib.CompPlanEngine(sqlconn_live, sqlconn_readonly);

            while (!_shutdownEvent.WaitOne(300000))  // wait for five minutes between each run
            {
                try {
                    if (!paused) {
                        WriteToEventLog(EventLogEntryType.Information, "Start calculating Business Sales Consultant commissions");
                        CompPlan_BC.ProcessCommissions(CompPlanLib.Globals.performance_target_level.BusinessSalesConsultant);
                        WriteToEventLog(EventLogEntryType.Information, "Completed calculating Business Sales Consultant commissions");
                    }
                    else
                        Thread.Sleep(10000);
                }
                catch (Exception ex) {
                    CompPlanLib.Tools.Mailer mailer = new CompPlanLib.Tools.Mailer();
                    mailer.SendEmail(null, "CompPlan Service has encountered an error during BC processing", ex.ToString());
                }
            }
            */
        }

        private void SLThreadProcessing() {
            CompPlanLib.CompPlanEngine CompPlan_SL = new CompPlanLib.CompPlanEngine(sqlconn_live, sqlconn_readonly);

            while (!_shutdownEvent.WaitOne(300000))  // wait for five minutes between each run
            {
                try {
                    if (!paused) {
                        WriteToEventLog(EventLogEntryType.Information, "Start calculating Store commissions");
                        CompPlan_SL.ProcessCommissions(CompPlanLib.Globals.performance_target_level.Store);
                        WriteToEventLog(EventLogEntryType.Information, "Completed calculating Store commissions");
                    }
                    else
                        Thread.Sleep(10000);
                }
                catch (Exception ex) {
                    CompPlanLib.Tools.Mailer mailer = new CompPlanLib.Tools.Mailer();
                    mailer.SendEmail(null, "CompPlan Service has encountered an error during SL processing", ex.ToString());
                }
            }
        }

        private void DLThreadProcessing() {
            CompPlanLib.CompPlanEngine CompPlan_DL = new CompPlanLib.CompPlanEngine(sqlconn_live, sqlconn_readonly);

            while (!_shutdownEvent.WaitOne(300000))  // wait for five minutes between each run
            {
                try {
                    if (!paused) {
                        WriteToEventLog(EventLogEntryType.Information, "Start calculating District commissions");
                        CompPlan_DL.ProcessCommissions(CompPlanLib.Globals.performance_target_level.District);
                        WriteToEventLog(EventLogEntryType.Information, "Completed calculating District commissions");
                    }
                    else
                        Thread.Sleep(10000);
                }
                catch (Exception ex) {
                    CompPlanLib.Tools.Mailer mailer = new CompPlanLib.Tools.Mailer();
                    mailer.SendEmail(null, "CompPlan Service has encountered an error during DL processing", ex.ToString());
                }
            }
        }

        private void RLThreadProcessing() {
            CompPlanLib.CompPlanEngine CompPlan_RL = new CompPlanLib.CompPlanEngine(sqlconn_live, sqlconn_readonly);

            while (!_shutdownEvent.WaitOne(300000))  // wait for five minutes between each run
            {
                try {
                    if (!paused) {
                        WriteToEventLog(EventLogEntryType.Information, "Start calculating Region commissions");
                        CompPlan_RL.ProcessCommissions(CompPlanLib.Globals.performance_target_level.Region);
                        WriteToEventLog(EventLogEntryType.Information, "Completed calculating Region commissions");
                    }
                    else
                        Thread.Sleep(10000);
                }
                catch (Exception ex) {
                    CompPlanLib.Tools.Mailer mailer = new CompPlanLib.Tools.Mailer();
                    mailer.SendEmail(null, "CompPlan Service has encountered an error during RL processing", ex.ToString());
                }
            }
        }

        private void CLThreadProcessing() {
            CompPlanLib.CompPlanEngine CompPlan_CL = new CompPlanLib.CompPlanEngine(sqlconn_live, sqlconn_readonly);

            while (!_shutdownEvent.WaitOne(300000))  // wait for five minutes between each run
            {
                try {
                    if (!paused) {
                        WriteToEventLog(EventLogEntryType.Information, "Start calculating Area commissions");
                        CompPlan_CL.ProcessCommissions(CompPlanLib.Globals.performance_target_level.Channel);
                        WriteToEventLog(EventLogEntryType.Information, "Completed calculating Area commissions");
                    }
                    else
                        Thread.Sleep(10000);
                }
                catch (Exception ex) {
                    CompPlanLib.Tools.Mailer mailer = new CompPlanLib.Tools.Mailer();
                    mailer.SendEmail(null, "CompPlan Service has encountered an error during CL processing", ex.ToString());
                }
            }
        }
    }
}

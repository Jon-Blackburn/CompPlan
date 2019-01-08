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

namespace CompPlanService_SL {
    public partial class CompPlanSvc_SL : ServiceBase {
        ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        Thread _thread_sl;
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

        public CompPlanSvc_SL() {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
            this.ServiceName = "CompPlan SL Service";
        }

        protected override void OnStart(string[] args) {
            sqlconn_live = System.Configuration.ConfigurationManager.ConnectionStrings["db_live"].ToString();
            sqlconn_readonly = System.Configuration.ConfigurationManager.ConnectionStrings["db_readonly"].ToString();

            // GJK 11/5/2015 - broke this down so each level runs in its own thread
            _thread_sl = new Thread(SLThreadProcessing);
            _thread_sl.Name = "CompPlan_SL";
            _thread_sl.IsBackground = true;
            _thread_sl.Priority = ThreadPriority.Normal;
            _thread_sl.Start();

            WriteToEventLog(EventLogEntryType.Information, this.ServiceName + " Started using SQL connection " + sqlconn_live);
        }

        protected override void OnStop() {
            _shutdownEvent.Set();
            if (_thread_sl != null && !_thread_sl.Join(30000))
                _thread_sl.Abort();

            WriteToEventLog(EventLogEntryType.Information, this.ServiceName + " Stopped");
        }

        protected override void OnPause() {
            paused = true;
            WriteToEventLog(EventLogEntryType.Information, this.ServiceName + " Paused");
        }

        protected override void OnContinue() {
            paused = false;
            WriteToEventLog(EventLogEntryType.Information, this.ServiceName + " Continued");
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
    }
}

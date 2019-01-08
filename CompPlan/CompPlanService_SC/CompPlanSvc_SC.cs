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

namespace CompPlanService_SC {
    public partial class CompPlanSvc_SC : ServiceBase {
        ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        Thread _thread_sc;
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

        public CompPlanSvc_SC() {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
            this.ServiceName = "CompPlan SC Service";
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

            WriteToEventLog(EventLogEntryType.Information, this.ServiceName + " Started using SQL connection " + sqlconn_live);
        }

        protected override void OnStop() {
            _shutdownEvent.Set();
            if (_thread_sc != null && !_thread_sc.Join(30000))
                _thread_sc.Abort();

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
    }
}

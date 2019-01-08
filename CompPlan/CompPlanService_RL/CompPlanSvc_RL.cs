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

namespace CompPlanService_RL {
    public partial class CompPlanSvc_RL : ServiceBase {
        ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        Thread _thread_rl;
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

        public CompPlanSvc_RL() {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
            this.ServiceName = "CompPlan RL Service";
        }

        protected override void OnStart(string[] args) {
            sqlconn_live = System.Configuration.ConfigurationManager.ConnectionStrings["db_live"].ToString();
            sqlconn_readonly = System.Configuration.ConfigurationManager.ConnectionStrings["db_readonly"].ToString();

            // GJK 11/5/2015 - broke this down so each level runs in its own thread
            _thread_rl = new Thread(RLThreadProcessing);
            _thread_rl.Name = "CompPlan_RL";
            _thread_rl.IsBackground = true;
            _thread_rl.Priority = ThreadPriority.Normal;
            _thread_rl.Start();

            WriteToEventLog(EventLogEntryType.Information, this.ServiceName + " Started using SQL connection " + sqlconn_live);
        }

        protected override void OnStop() {
            _shutdownEvent.Set();
            if (_thread_rl != null && !_thread_rl.Join(30000))
                _thread_rl.Abort();

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
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace CompPlanService_SL {
    [RunInstaller(true)]
    public class CompPlanSvc_SL_installer : System.Configuration.Install.Installer {
        private ServiceProcessInstaller processInstaller;
        private ServiceInstaller serviceInstaller;
        public CompPlanSvc_SL_installer() {
            processInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();
            processInstaller.Account = ServiceAccount.User;
            processInstaller.Username = "sdadmin@vzawireless.net";
            processInstaller.Password = "@bc123!";
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "CompPlan SL Service";
            serviceInstaller.Description = "AWireless Store Leaders commissions calculator";
            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }

        public override void Install(System.Collections.IDictionary stateSaver) {
            base.Install(stateSaver);
            ServiceController controller = new ServiceController("CompPlan SL Service");
            try {
                controller.Start();
            }
            catch (Exception ex) {
                String source = "CompPlan SL Service";
                String log = "Application";
                if (!EventLog.SourceExists(source)) {
                    EventLog.CreateEventSource(source, log);
                }
                EventLog eLog = new EventLog();
                eLog.Source = source;
                eLog.WriteEntry(@"The service could not be started. Please start the service manually. Error: " + ex.Message, EventLogEntryType.Error);
            }
        }
    }
}

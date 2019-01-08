using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;

namespace CompPlanService
{
    [RunInstaller(true)]
    public class CompPlanServiceInstaller : System.Configuration.Install.Installer
    {
        private ServiceProcessInstaller processInstaller;
        private ServiceInstaller serviceInstaller;
        public CompPlanServiceInstaller()
        {
            processInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();
            processInstaller.Account = ServiceAccount.User;
            processInstaller.Username = "sdadmin@vzawireless.net";
            processInstaller.Password = "@bc123!";
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "CompPlan Service";
            serviceInstaller.Description = "AWireless commissions calculator";
            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }

        public override void Install(System.Collections.IDictionary stateSaver)
        {
            base.Install(stateSaver);
            ServiceController controller = new ServiceController("CompPlan Service");
            try
            {
                controller.Start();
            }
            catch (Exception ex)
            {
                String source = "CompPlan Service";
                String log = "Application";
                if (!EventLog.SourceExists(source))
                {
                    EventLog.CreateEventSource(source, log);
                }
                EventLog eLog = new EventLog();
                eLog.Source = source;
                eLog.WriteEntry(@"The service could not be started. Please start the service manually. Error: " + ex.Message, EventLogEntryType.Error);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Configuration;
using System.Diagnostics;
using System.Net;

namespace CompPlanLib.Tools
{
    public class Mailer
    {
        SmtpClient server;

        public Mailer()
        {
            server = new SmtpClient(ConfigurationManager.AppSettings["exchangeServer"]);
            server.DeliveryMethod = SmtpDeliveryMethod.Network;
        }

        ~Mailer()
        {
            server.Dispose();
        }

        public void emailMessage(string message, string to, string subject)
        {
            MailMessage msg = new MailMessage(ConfigurationManager.AppSettings["fromAddress"].ToString(), to, "Your task has completed", message);
            server.Send(msg);
        }

        public void SendEmail(string To, string SubjectText, string MessageText, string CC = "", bool isHTML = false)
        {
            try
            {
                MailMessage message = new MailMessage();
                message.To.Add((To != null && To.Length > 0) ? To : ConfigurationManager.AppSettings["emailTo"].ToString());
                if (CC != "")
                {
                    message.CC.Add(CC);
                }
                message.From = new MailAddress(ConfigurationManager.AppSettings["fromAddress"].ToString());
                message.Body = MessageText;
                message.Subject = SubjectText;
                message.IsBodyHtml = isHTML;
                server.Send(message);
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
                eLog.WriteEntry(@String.Format("Error '{0}' occured trying to send email: {1}, {2}", ex.Message, SubjectText, MessageText), EventLogEntryType.Error);
            }
        }
    }
}

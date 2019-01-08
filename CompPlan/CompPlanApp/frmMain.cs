using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.DirectoryServices.AccountManagement;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CompPlanApp
{
    public partial class frmMain : Form
    {
        private string sqlconn_live = System.Configuration.ConfigurationManager.ConnectionStrings["db_live"].ToString();
        private string sqlconn_readonly = System.Configuration.ConfigurationManager.ConnectionStrings["db_readonly"].ToString();
        private CompPlanLib.CompPlanEngine compplan;

        private bool calcs_running;

        public frmMain()
        {
            InitializeComponent();
            calcs_running = false;
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            frmPleaseWait pw = new frmPleaseWait();
            pw.SetMessage("Loading Comp Calculator - please wait...");
            pw.Show(this);
            Application.DoEvents();
            if (CurrentUserHasPermissions())
            {
                compplan = new CompPlanLib.CompPlanEngine(sqlconn_live, sqlconn_readonly);
                LoadAllFormData();
                SetDefaults();
                pw.Close();
            }
            else
                Close();
        }

        private void btnRunCalcs_Click(object sender, EventArgs e)
        {
            RunCompPlan();
        }

        private void SetDefaults()
        {
            sbLabel.Text = "Ready";
            pbAnimate.Enabled = false;
            pbAnimate.Visible = false;
            pnlControls.Visible = true;
            pnlPayPeriods.Enabled = true;
            pnlReportLevel.Enabled = true;
            pnlEmployee.Enabled = true;
            Cursor.Current = Cursors.Default;
        }

        private void LoadAllFormData()
        {
            LoadReportLevels();
            if (cbReportLevel.SelectedValue != null)
            {
                CompPlanLib.Globals.performance_target_level level = (CompPlanLib.Globals.performance_target_level)cbReportLevel.SelectedValue;
                LoadPayPeriods(level);
                LoadEmployees(level);
            }
        }

        private void LoadReportLevels()
        {
            cbReportLevel.DataSource = Enum.GetValues(typeof(CompPlanLib.Globals.performance_target_level)).Cast<CompPlanLib.Globals.performance_target_level>().Where(e => e == CompPlanLib.Globals.performance_target_level.Channel || e == CompPlanLib.Globals.performance_target_level.Region || e == CompPlanLib.Globals.performance_target_level.District || e == CompPlanLib.Globals.performance_target_level.Store || e == CompPlanLib.Globals.performance_target_level.Employee).OrderBy(o => o.ToString()).ToList();
        }

        private void LoadPayPeriods(CompPlanLib.Globals.performance_target_level level)
        {
            DateTime current_date = DateTime.Now;

            // Going to modify this so that for sales consultants we can have current pay period recalcs
            int start_day = (level == CompPlanLib.Globals.performance_target_level.Employee) ? (current_date.Day >= 16) ? 1 : 1 : 1;
            if (level == CompPlanLib.Globals.performance_target_level.Employee && current_date.Day >= 1) {
                current_date = current_date.AddMonths(1);
            }

            if (level == CompPlanLib.Globals.performance_target_level.Store || level == CompPlanLib.Globals.performance_target_level.Channel) {
                if (current_date.Month != 12)
                    current_date = current_date.AddMonths(1);
            }

            DateTime pp_start_date = new DateTime(current_date.Year, current_date.Month, start_day, 0, 0, 0);
            int for_sc = (level == CompPlanLib.Globals.performance_target_level.Employee) ? 1 : 0;

            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(sqlconn_readonly))
            using (SqlDataAdapter dat = new SqlDataAdapter(String.Format("select convert(varchar, start_date, 101) + ' - ' + convert(varchar, end_date, 101) as DateRange, start_date from dbo.commissions_pay_periods where start_date<'{0}' and for_consultants={1} order by end_date desc", pp_start_date, for_sc), con))
                dat.Fill(dt);
            
            cbPayPeriods.DataSource = dt;
            cbPayPeriods.DisplayMember = "DateRange";
            cbPayPeriods.ValueMember = "start_date";
        }

        private void LoadEmployees(CompPlanLib.Globals.performance_target_level level)
        {
            // channel is level 7 in the enum, but user level ID for channel is actually 1
            int employee_level = (level == CompPlanLib.Globals.performance_target_level.Channel) ? 1 : (int)level;
            
            // for reps, we include anyone in these commissions group(s) since there are now selling store managers who are level 3 [store] but have to paid as a rep
            string filter = (level == CompPlanLib.Globals.performance_target_level.Employee) ? "commission_group_id in (3,7,10,11,12,13, 29, 30, 32, 37, 38)" : String.Format("user_location_level={0} and isnull(commission_group_id,0) not in (3,7,10,11,12,13,29,30, 32, 37, 38)", employee_level);

            DateTime ppd_start = (DateTime)cbPayPeriods.SelectedValue;

            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(sqlconn_readonly))
            using (SqlDataAdapter dat = new SqlDataAdapter(String.Format("select distinct rq_id, display_name from dbo.users with(nolock) where rq_id is not null and display_name is not null and {0} and start_date<='{1}' order by display_name", filter, ppd_start.AddDays(3)), con))
                dat.Fill(dt);

            cbEmployee.DataSource = dt;
            cbEmployee.DisplayMember = "display_name";
            cbEmployee.ValueMember = "rq_id";
        }


        private void RunCompPlan()
        {
            if (!FormOK())
                return;

            try
            {
                calcs_running = false;
                pnlPayPeriods.Enabled = false;
                pnlReportLevel.Enabled = false;
                pnlEmployee.Enabled = false;
                sbLabel.Text = "Running calculations... please wait...";
                pnlControls.Visible = false;
                pbAnimate.Enabled = true;
                pbAnimate.Visible = true;

                DateTime run = (DateTime)cbPayPeriods.SelectedValue;
                CompPlanLib.Globals.performance_target_level level = (CompPlanLib.Globals.performance_target_level)cbReportLevel.SelectedValue;
                int rq_id = (int)cbEmployee.SelectedValue;

                ProcessingThread pt = new ProcessingThread(compplan, run, level, rq_id);
                Thread th = new Thread(pt.DoCalcs);

                Cursor.Current = Cursors.WaitCursor;

                th.Start();

                while (th.IsAlive)
                {
                    calcs_running = true;
                    Application.DoEvents();
                }

                calcs_running = th.IsAlive;

                SetDefaults();

                this.WindowState = FormWindowState.Minimized;
                this.Show();
                this.WindowState = FormWindowState.Normal;

                MessageBox.Show("Run Complete!", "All Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception err)
            {
                string innermsg = err.InnerException != null ? ", inner exception: " + err.InnerException.Message : "";
                MessageBox.Show(String.Format("An unexpected error occured during recalculation: {0}{1}", err.Message, innermsg), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool FormOK()
        {
            bool res = true;

            List<string> msg = new List<string>();

            if (cbPayPeriods.SelectedItem == null)
            {
                msg.Add("Invalid Pay Period selected");
                res = false;
            }

            if (cbReportLevel.SelectedItem == null)
            {
                msg.Add("Invalid Report Level selected");
                res = false;
            }


            if (cbEmployee.SelectedItem == null)
            {
                msg.Add("Invalid Employee selected");
                res = false;
            }

            if (!res)
                MessageBox.Show("Cannot proceed, there are errors on the form:" + Environment.NewLine + String.Join(Environment.NewLine, msg), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            msg.Clear();
            msg.Add("Report Level: " + cbReportLevel.Text);
            msg.Add("Pay Period: " + cbPayPeriods.Text);
            msg.Add("Employee: " + cbEmployee.Text);
            DialogResult msg_res = MessageBox.Show("Ready to run - please confirm you want to recalculate the following:" + Environment.NewLine + Environment.NewLine + String.Join(Environment.NewLine, msg) + Environment.NewLine, "Ready to run", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            res = (msg_res == System.Windows.Forms.DialogResult.Yes);

            return res;
        }

        private bool CurrentUserHasPermissions()
        {
            bool res = false;

            try
            {
                string currentuser = Environment.UserName;
                PrincipalContext ADcontext = new PrincipalContext(ContextType.Domain, "vzawireless.net");
                if (ADcontext.ConnectedServer != null)
                {
                    // grab the user object for this AD user
                    UserPrincipal ad_user = UserPrincipal.FindByIdentity(ADcontext, IdentityType.SamAccountName, currentuser);
                    if (ad_user != null)
                    {
                        int startIndex = ad_user.DistinguishedName.IndexOf("OU=", 1) + 3; //+3 for  length of "OU="
                        int endIndex = ad_user.DistinguishedName.IndexOf(",", startIndex);
                        string user_ou = ad_user.DistinguishedName.Substring((startIndex), (endIndex - startIndex)).ToLower();

                        List<string> allowed_ous = new List<string> { "informationtechnology", "humanresources", "accounting", "training" };
                        res = (allowed_ous.Contains(user_ou));
                    }

                }
            }
            catch { }

            if (!res)
                MessageBox.Show("You do not have permissions to run this application.", "Cannot Continue", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

            return res;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (calcs_running == true)
                MessageBox.Show("Comp plan is currently running, please wait for it to complete.", "Cannot Close", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            e.Cancel = (calcs_running);
        }

        private void cbReportLevel_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (cbReportLevel.SelectedValue != null)
            {
                CompPlanLib.Globals.performance_target_level level = (CompPlanLib.Globals.performance_target_level)cbReportLevel.SelectedValue;
                LoadPayPeriods(level);
                LoadEmployees(level);
            }
        }
        
        private void cbPayPeriods_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (cbReportLevel.SelectedValue != null)
            {
                CompPlanLib.Globals.performance_target_level level = (CompPlanLib.Globals.performance_target_level)cbReportLevel.SelectedValue;
                LoadEmployees(level);
            }
        }
    }

    public class ProcessingThread
    {
        CompPlanLib.CompPlanEngine compplan;
        DateTime run;
        CompPlanLib.Globals.performance_target_level level;
        int id;

        public ProcessingThread(CompPlanLib.CompPlanEngine compplanobj, DateTime run_date, CompPlanLib.Globals.performance_target_level target_level, int rq_id)
        {
            compplan = compplanobj;
            run = run_date;
            level = target_level;
            id = rq_id;
        }

        // This method will be called when the thread is started. 
        public void DoCalcs()
        {
            compplan.ProcessCommissions(run, level, id, null);
        }
    }
}

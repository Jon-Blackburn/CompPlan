using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace CompPlanConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            DateTime start = DateTime.Now;
            Console.WriteLine("Running CompPlan...");

            string sqlconn_live;
            string sqlconn_readonly;

#if DEBUG           
            sqlconn_live = System.Configuration.ConfigurationManager.ConnectionStrings["db_development"].ToString();
            sqlconn_readonly = System.Configuration.ConfigurationManager.ConnectionStrings["db_development"].ToString();
#else
            sqlconn_live = System.Configuration.ConfigurationManager.ConnectionStrings["db_live"].ToString();
            sqlconn_readonly = System.Configuration.ConfigurationManager.ConnectionStrings["db_readonly"].ToString();
#endif

            //CompPlanLib.CompPlanEngine compplantest1 = new CompPlanLib.CompPlanEngine(sqlconn_live, sqlconn_readonly, new DateTime(2017, 03, 31, 23, 59, 59));
            //compplantest1.ProcessCommissions(CompPlanLib.Globals.performance_target_level.Region);

            CompPlanLib.CompPlanEngine compplantest = new CompPlanLib.CompPlanEngine(sqlconn_live, sqlconn_readonly, new DateTime(2018, 10, 31, 23, 59, 59));// DateTime.Now.AddDays(-3));
            compplantest.ProcessCommissions(CompPlanLib.Globals.performance_target_level.Store);
            compplantest.ProcessCommissions(CompPlanLib.Globals.performance_target_level.Employee);

            /* test what-if calc 
            string sqlconn = System.Configuration.ConfigurationManager.ConnectionStrings["GVLMSS01_DB"].ToString();

            CompPlanLib.CompPlanEngine compplan = new CompPlanLib.CompPlanEngine(sqlconn, new DateTime(2014, 01, 01, 00, 00, 00));

            decimal res = 0.00m;

            // results list - could be multiple accelerators
            List<CompPlanLib.Data.CommissionValueDetails> res_list = new List<CompPlanLib.Data.CommissionValueDetails>();

            // fake BSR data
            CompPlanLib.Data.BSRitems test1 = new CompPlanLib.Data.BSRitems();
            CompPlanLib.Data.BSRitems test2 = new CompPlanLib.Data.BSRitems();
            CompPlanLib.Data.BSRitems test3 = new CompPlanLib.Data.BSRitems();
            List<CompPlanLib.Data.BSRitems> bsr_data = new List<CompPlanLib.Data.BSRitems>();


            // sales consultants
            compplan.InitialiseWhatIf(CompPlanLib.Globals.performance_target_level.Employee);

            // always add gross profit
            test1.id_field = 1486;  // employee ID
            test1.metric_id = (int)CompPlanLib.Globals.BSR_metrics.GP_metric_id;
            test1.total = 1210.7900m;

            bsr_data.Add(test1);

            // add a valid box total so it will go over minimum
            test2.id_field = 1486;  // employee ID
            test2.metric_id = (int)CompPlanLib.Globals.BSR_metrics.box_metric_id;
            test2.total = 5;

            bsr_data.Add(test2);

            // now add an APB amount
            test3.id_field = 1486;  // employee ID
            test3.metric_id = (int)CompPlanLib.Globals.BSR_metrics.apb_metric_id;  // APB metric ID
            test3.total = 45.7350m;  // APB total

            bsr_data.Add(test3);

            res = compplan.CalculateWhatIf(bsr_data, ref res_list);

            // for one accelerator, just assume one row - for multiple need to loop
            CompPlanLib.Data.CommissionValueDetails cvd = res_list.FirstOrDefault();

            int got_metric_id;
            decimal got_accl_val;

            got_metric_id = cvd != null ? cvd.metric_id : 0;
            got_accl_val = cvd != null ? cvd.performance_total : 0.00m;

            res_list.ForEach(r => {
                    got_metric_id = r.metric_id;
                    got_accl_val = r.performance_total;
            });

            // District Leaders
            compplan.InitialiseWhatIf(CompPlanLib.Globals.performance_target_level.District);

            bsr_data.Clear();
            res_list.Clear();

            // always add gross profit
            test1.id_field = 16;  // RQ district ID
            test1.metric_id = (int)CompPlanLib.Globals.BSR_metrics.GP_metric_id;
            test1.total = 175019.9000m;  

            bsr_data.Add(test1);

            // DL's and RL's just use box targets
            test2.id_field = 16;  // RQ district ID
            test2.metric_id = (int)CompPlanLib.Globals.BSR_metrics.box_metric_id;
            test2.total = 945.4000m;  

            bsr_data.Add(test2);

            res = compplan.CalculateWhatIf(bsr_data, ref res_list);

            
            // Region Leaders
            compplan.InitialiseWhatIf(CompPlanLib.Globals.performance_target_level.Region);

            bsr_data.Clear();
            res_list.Clear();

            // always add gross profit
            test1.id_field = 8;  // RQ region ID
            test1.metric_id = (int)CompPlanLib.Globals.BSR_metrics.GP_metric_id;
            test1.total = 640915.2700m;  

            bsr_data.Add(test1);

            // DL's and RL's just use box targets
            test2.id_field = 8;  // RQ region ID
            test2.metric_id = (int)CompPlanLib.Globals.BSR_metrics.box_metric_id;
            test2.total = 3618.6000m;  

            bsr_data.Add(test2);

            res = compplan.CalculateWhatIf(bsr_data, ref res_list);

            Console.WriteLine(res.ToString());
            */
            DateTime end = DateTime.Now;
            TimeSpan runtime = end - start;
            Console.WriteLine("RUNTIME: " + runtime.ToString());
            Console.WriteLine("DONE");
            Console.Read();
        }
    }
}

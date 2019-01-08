using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CompPlanApp
{
    public partial class frmPleaseWait : Form
    {
        public frmPleaseWait()
        {
            InitializeComponent();
        }

        public void SetMessage(string msg)
        {
            lblPleaseWait.Text = msg;
            Application.DoEvents();
        }

        public new void Show(IWin32Window owner)
        {
            base.Show(owner);

            if (Owner != null)
                Location = new Point(Owner.Location.X + Owner.Width / 2 - Width / 2,
                    Owner.Location.Y + Owner.Height / 2 - Height / 2);
        }
    }
}

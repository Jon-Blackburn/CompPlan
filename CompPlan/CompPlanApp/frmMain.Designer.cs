namespace CompPlanApp
{
    partial class frmMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            this.pnlPayPeriods = new System.Windows.Forms.Panel();
            this.lblPayPeriodDivider = new System.Windows.Forms.Label();
            this.lblSelectPayPeriods = new System.Windows.Forms.Label();
            this.cbPayPeriods = new System.Windows.Forms.ComboBox();
            this.btnRunCalcs = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.pnlControls = new System.Windows.Forms.Panel();
            this.pbAnimate = new System.Windows.Forms.PictureBox();
            this.sbMainStatusbar = new System.Windows.Forms.StatusStrip();
            this.sbLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.pnlReportLevel = new System.Windows.Forms.Panel();
            this.lblLevelDivider = new System.Windows.Forms.Label();
            this.lblSelectLevel = new System.Windows.Forms.Label();
            this.cbReportLevel = new System.Windows.Forms.ComboBox();
            this.pnlEmployee = new System.Windows.Forms.Panel();
            this.label3 = new System.Windows.Forms.Label();
            this.lblSelectEmployeee = new System.Windows.Forms.Label();
            this.cbEmployee = new System.Windows.Forms.ComboBox();
            this.pnlPayPeriods.SuspendLayout();
            this.pnlControls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbAnimate)).BeginInit();
            this.sbMainStatusbar.SuspendLayout();
            this.pnlReportLevel.SuspendLayout();
            this.pnlEmployee.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlPayPeriods
            // 
            this.pnlPayPeriods.Controls.Add(this.lblPayPeriodDivider);
            this.pnlPayPeriods.Controls.Add(this.lblSelectPayPeriods);
            this.pnlPayPeriods.Controls.Add(this.cbPayPeriods);
            this.pnlPayPeriods.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pnlPayPeriods.ForeColor = System.Drawing.SystemColors.ControlText;
            this.pnlPayPeriods.Location = new System.Drawing.Point(13, 75);
            this.pnlPayPeriods.Name = "pnlPayPeriods";
            this.pnlPayPeriods.Size = new System.Drawing.Size(259, 49);
            this.pnlPayPeriods.TabIndex = 1;
            // 
            // lblPayPeriodDivider
            // 
            this.lblPayPeriodDivider.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblPayPeriodDivider.Location = new System.Drawing.Point(125, 11);
            this.lblPayPeriodDivider.Name = "lblPayPeriodDivider";
            this.lblPayPeriodDivider.Size = new System.Drawing.Size(120, 2);
            this.lblPayPeriodDivider.TabIndex = 4;
            // 
            // lblSelectPayPeriods
            // 
            this.lblSelectPayPeriods.AutoSize = true;
            this.lblSelectPayPeriods.Location = new System.Drawing.Point(3, 4);
            this.lblSelectPayPeriods.Name = "lblSelectPayPeriods";
            this.lblSelectPayPeriods.Size = new System.Drawing.Size(118, 13);
            this.lblSelectPayPeriods.TabIndex = 3;
            this.lblSelectPayPeriods.Text = "2. Select Pay Period";
            // 
            // cbPayPeriods
            // 
            this.cbPayPeriods.BackColor = System.Drawing.SystemColors.Info;
            this.cbPayPeriods.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbPayPeriods.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbPayPeriods.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbPayPeriods.FormattingEnabled = true;
            this.cbPayPeriods.Location = new System.Drawing.Point(16, 20);
            this.cbPayPeriods.Name = "cbPayPeriods";
            this.cbPayPeriods.Size = new System.Drawing.Size(229, 24);
            this.cbPayPeriods.TabIndex = 1;
            this.cbPayPeriods.SelectionChangeCommitted += new System.EventHandler(this.cbPayPeriods_SelectionChangeCommitted);
            // 
            // btnRunCalcs
            // 
            this.btnRunCalcs.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRunCalcs.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRunCalcs.Location = new System.Drawing.Point(6, 3);
            this.btnRunCalcs.Name = "btnRunCalcs";
            this.btnRunCalcs.Size = new System.Drawing.Size(75, 47);
            this.btnRunCalcs.TabIndex = 3;
            this.btnRunCalcs.Text = "Run Calcs";
            this.btnRunCalcs.UseVisualStyleBackColor = true;
            this.btnRunCalcs.Click += new System.EventHandler(this.btnRunCalcs_Click);
            // 
            // btnClose
            // 
            this.btnClose.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClose.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClose.Location = new System.Drawing.Point(169, 4);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 47);
            this.btnClose.TabIndex = 4;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // pnlControls
            // 
            this.pnlControls.Controls.Add(this.btnClose);
            this.pnlControls.Controls.Add(this.btnRunCalcs);
            this.pnlControls.Location = new System.Drawing.Point(13, 199);
            this.pnlControls.Name = "pnlControls";
            this.pnlControls.Size = new System.Drawing.Size(259, 53);
            this.pnlControls.TabIndex = 3;
            // 
            // pbAnimate
            // 
            this.pbAnimate.Image = ((System.Drawing.Image)(resources.GetObject("pbAnimate.Image")));
            this.pbAnimate.Location = new System.Drawing.Point(182, 256);
            this.pbAnimate.Name = "pbAnimate";
            this.pbAnimate.Size = new System.Drawing.Size(75, 54);
            this.pbAnimate.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pbAnimate.TabIndex = 6;
            this.pbAnimate.TabStop = false;
            this.pbAnimate.Visible = false;
            // 
            // sbMainStatusbar
            // 
            this.sbMainStatusbar.BackColor = System.Drawing.SystemColors.ControlLight;
            this.sbMainStatusbar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sbLabel});
            this.sbMainStatusbar.Location = new System.Drawing.Point(0, 351);
            this.sbMainStatusbar.Name = "sbMainStatusbar";
            this.sbMainStatusbar.Size = new System.Drawing.Size(333, 22);
            this.sbMainStatusbar.SizingGrip = false;
            this.sbMainStatusbar.TabIndex = 7;
            // 
            // sbLabel
            // 
            this.sbLabel.Name = "sbLabel";
            this.sbLabel.Size = new System.Drawing.Size(318, 17);
            this.sbLabel.Spring = true;
            this.sbLabel.Text = "Ready.";
            // 
            // pnlReportLevel
            // 
            this.pnlReportLevel.Controls.Add(this.lblLevelDivider);
            this.pnlReportLevel.Controls.Add(this.lblSelectLevel);
            this.pnlReportLevel.Controls.Add(this.cbReportLevel);
            this.pnlReportLevel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pnlReportLevel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.pnlReportLevel.Location = new System.Drawing.Point(13, 12);
            this.pnlReportLevel.Name = "pnlReportLevel";
            this.pnlReportLevel.Size = new System.Drawing.Size(259, 48);
            this.pnlReportLevel.TabIndex = 1;
            // 
            // lblLevelDivider
            // 
            this.lblLevelDivider.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblLevelDivider.Location = new System.Drawing.Point(98, 11);
            this.lblLevelDivider.Name = "lblLevelDivider";
            this.lblLevelDivider.Size = new System.Drawing.Size(147, 2);
            this.lblLevelDivider.TabIndex = 2;
            // 
            // lblSelectLevel
            // 
            this.lblSelectLevel.AutoSize = true;
            this.lblSelectLevel.Location = new System.Drawing.Point(3, 4);
            this.lblSelectLevel.Name = "lblSelectLevel";
            this.lblSelectLevel.Size = new System.Drawing.Size(88, 13);
            this.lblSelectLevel.TabIndex = 1;
            this.lblSelectLevel.Text = "1. Select Level";
            // 
            // cbReportLevel
            // 
            this.cbReportLevel.BackColor = System.Drawing.SystemColors.Info;
            this.cbReportLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbReportLevel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbReportLevel.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbReportLevel.FormattingEnabled = true;
            this.cbReportLevel.Location = new System.Drawing.Point(16, 20);
            this.cbReportLevel.Name = "cbReportLevel";
            this.cbReportLevel.Size = new System.Drawing.Size(229, 24);
            this.cbReportLevel.TabIndex = 0;
            this.cbReportLevel.SelectionChangeCommitted += new System.EventHandler(this.cbReportLevel_SelectionChangeCommitted);
            // 
            // pnlEmployee
            // 
            this.pnlEmployee.Controls.Add(this.label3);
            this.pnlEmployee.Controls.Add(this.lblSelectEmployeee);
            this.pnlEmployee.Controls.Add(this.cbEmployee);
            this.pnlEmployee.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pnlEmployee.ForeColor = System.Drawing.SystemColors.ControlText;
            this.pnlEmployee.Location = new System.Drawing.Point(12, 139);
            this.pnlEmployee.Name = "pnlEmployee";
            this.pnlEmployee.Size = new System.Drawing.Size(259, 49);
            this.pnlEmployee.TabIndex = 2;
            this.pnlEmployee.Text = "Select Employee";
            // 
            // label3
            // 
            this.label3.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label3.Location = new System.Drawing.Point(119, 10);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(126, 2);
            this.label3.TabIndex = 4;
            // 
            // lblSelectEmployeee
            // 
            this.lblSelectEmployeee.AutoSize = true;
            this.lblSelectEmployeee.Location = new System.Drawing.Point(3, 3);
            this.lblSelectEmployeee.Name = "lblSelectEmployeee";
            this.lblSelectEmployeee.Size = new System.Drawing.Size(113, 13);
            this.lblSelectEmployeee.TabIndex = 3;
            this.lblSelectEmployeee.Text = "3. Select Employee";
            // 
            // cbEmployee
            // 
            this.cbEmployee.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.cbEmployee.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.cbEmployee.BackColor = System.Drawing.SystemColors.Info;
            this.cbEmployee.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbEmployee.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbEmployee.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbEmployee.FormattingEnabled = true;
            this.cbEmployee.Location = new System.Drawing.Point(16, 20);
            this.cbEmployee.Name = "cbEmployee";
            this.cbEmployee.Size = new System.Drawing.Size(229, 24);
            this.cbEmployee.TabIndex = 2;
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(333, 373);
            this.Controls.Add(this.pnlEmployee);
            this.Controls.Add(this.pnlReportLevel);
            this.Controls.Add(this.sbMainStatusbar);
            this.Controls.Add(this.pbAnimate);
            this.Controls.Add(this.pnlPayPeriods);
            this.Controls.Add(this.pnlControls);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "frmMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "AWireless CompPlan";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
            this.Load += new System.EventHandler(this.frmMain_Load);
            this.pnlPayPeriods.ResumeLayout(false);
            this.pnlPayPeriods.PerformLayout();
            this.pnlControls.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pbAnimate)).EndInit();
            this.sbMainStatusbar.ResumeLayout(false);
            this.sbMainStatusbar.PerformLayout();
            this.pnlReportLevel.ResumeLayout(false);
            this.pnlReportLevel.PerformLayout();
            this.pnlEmployee.ResumeLayout(false);
            this.pnlEmployee.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox cbPayPeriods;
        private System.Windows.Forms.Button btnRunCalcs;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Panel pnlControls;
        private System.Windows.Forms.PictureBox pbAnimate;
        private System.Windows.Forms.StatusStrip sbMainStatusbar;
        private System.Windows.Forms.ToolStripStatusLabel sbLabel;
        private System.Windows.Forms.ComboBox cbReportLevel;
        private System.Windows.Forms.ComboBox cbEmployee;
        private System.Windows.Forms.Panel pnlPayPeriods;
        private System.Windows.Forms.Panel pnlReportLevel;
        private System.Windows.Forms.Panel pnlEmployee;
        private System.Windows.Forms.Label lblPayPeriodDivider;
        private System.Windows.Forms.Label lblSelectPayPeriods;
        private System.Windows.Forms.Label lblLevelDivider;
        private System.Windows.Forms.Label lblSelectLevel;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblSelectEmployeee;
    }
}


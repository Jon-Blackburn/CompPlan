namespace CompPlanApp
{
    partial class frmPleaseWait
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmPleaseWait));
            this.pnlControls = new System.Windows.Forms.Panel();
            this.lblPleaseWait = new System.Windows.Forms.Label();
            this.pnlControls.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlControls
            // 
            this.pnlControls.AutoSize = true;
            this.pnlControls.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlControls.Controls.Add(this.lblPleaseWait);
            this.pnlControls.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlControls.Location = new System.Drawing.Point(0, 0);
            this.pnlControls.Name = "pnlControls";
            this.pnlControls.Size = new System.Drawing.Size(288, 59);
            this.pnlControls.TabIndex = 1;
            // 
            // lblPleaseWait
            // 
            this.lblPleaseWait.AutoSize = true;
            this.lblPleaseWait.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPleaseWait.Location = new System.Drawing.Point(10, 19);
            this.lblPleaseWait.Name = "lblPleaseWait";
            this.lblPleaseWait.Size = new System.Drawing.Size(96, 16);
            this.lblPleaseWait.TabIndex = 1;
            this.lblPleaseWait.Text = "Please Wait...";
            this.lblPleaseWait.UseWaitCursor = true;
            // 
            // frmPleaseWait
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(288, 59);
            this.Controls.Add(this.pnlControls);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmPleaseWait";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Loading...";
            this.pnlControls.ResumeLayout(false);
            this.pnlControls.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel pnlControls;
        private System.Windows.Forms.Label lblPleaseWait;

    }
}
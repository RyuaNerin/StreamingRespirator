namespace StreamingRespirator.Core.Windows
{
    partial class ConfigWindow
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.ctlProxyGroup = new System.Windows.Forms.GroupBox();
            this.ctlProxyGroupPanel = new System.Windows.Forms.TableLayoutPanel();
            this.ctlUseHTTPS = new System.Windows.Forms.CheckBox();
            this.ctlPortLabel = new System.Windows.Forms.Label();
            this.ctlPort = new System.Windows.Forms.NumericUpDown();
            this.ctlAutoStartup = new System.Windows.Forms.CheckBox();
            this.ctlStreamingGroup = new System.Windows.Forms.GroupBox();
            this.ctlStreamingGroupPanel = new System.Windows.Forms.TableLayoutPanel();
            this.ctlShowMyRetweet = new System.Windows.Forms.CheckBox();
            this.ctlShowRetweet = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.ctlOK = new System.Windows.Forms.Button();
            this.ctlCancel = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.ctlProxyGroup.SuspendLayout();
            this.ctlProxyGroupPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ctlPort)).BeginInit();
            this.ctlStreamingGroup.SuspendLayout();
            this.ctlStreamingGroupPanel.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.ctlProxyGroup, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.ctlAutoStartup, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.ctlStreamingGroup, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 3);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(5);
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(237, 232);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // ctlProxyGroup
            // 
            this.ctlProxyGroup.AutoSize = true;
            this.ctlProxyGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlProxyGroup.Controls.Add(this.ctlProxyGroupPanel);
            this.ctlProxyGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlProxyGroup.Location = new System.Drawing.Point(8, 33);
            this.ctlProxyGroup.Name = "ctlProxyGroup";
            this.ctlProxyGroup.Size = new System.Drawing.Size(221, 76);
            this.ctlProxyGroup.TabIndex = 1;
            this.ctlProxyGroup.TabStop = false;
            this.ctlProxyGroup.Text = "프록시 설정";
            // 
            // ctlProxyGroupPanel
            // 
            this.ctlProxyGroupPanel.AutoSize = true;
            this.ctlProxyGroupPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlProxyGroupPanel.ColumnCount = 2;
            this.ctlProxyGroupPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.ctlProxyGroupPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.ctlProxyGroupPanel.Controls.Add(this.ctlUseHTTPS, 0, 1);
            this.ctlProxyGroupPanel.Controls.Add(this.ctlPortLabel, 0, 0);
            this.ctlProxyGroupPanel.Controls.Add(this.ctlPort, 1, 0);
            this.ctlProxyGroupPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.ctlProxyGroupPanel.Location = new System.Drawing.Point(3, 19);
            this.ctlProxyGroupPanel.Name = "ctlProxyGroupPanel";
            this.ctlProxyGroupPanel.RowCount = 2;
            this.ctlProxyGroupPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.ctlProxyGroupPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.ctlProxyGroupPanel.Size = new System.Drawing.Size(215, 54);
            this.ctlProxyGroupPanel.TabIndex = 0;
            // 
            // ctlUseHTTPS
            // 
            this.ctlUseHTTPS.AutoSize = true;
            this.ctlProxyGroupPanel.SetColumnSpan(this.ctlUseHTTPS, 2);
            this.ctlUseHTTPS.Location = new System.Drawing.Point(3, 32);
            this.ctlUseHTTPS.Name = "ctlUseHTTPS";
            this.ctlUseHTTPS.Size = new System.Drawing.Size(129, 19);
            this.ctlUseHTTPS.TabIndex = 9;
            this.ctlUseHTTPS.Text = "HTTPS 프록시 사용";
            this.ctlUseHTTPS.UseVisualStyleBackColor = true;
            this.ctlUseHTTPS.CheckedChanged += new System.EventHandler(this.ctlUseHTTPS_CheckedChanged);
            // 
            // ctlPortLabel
            // 
            this.ctlPortLabel.AutoSize = true;
            this.ctlPortLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlPortLabel.Location = new System.Drawing.Point(3, 0);
            this.ctlPortLabel.Margin = new System.Windows.Forms.Padding(3, 0, 10, 0);
            this.ctlPortLabel.Name = "ctlPortLabel";
            this.ctlPortLabel.Size = new System.Drawing.Size(71, 29);
            this.ctlPortLabel.TabIndex = 7;
            this.ctlPortLabel.Text = "프록시 포트";
            this.ctlPortLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ctlPort
            // 
            this.ctlPort.Location = new System.Drawing.Point(87, 3);
            this.ctlPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.ctlPort.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.ctlPort.Name = "ctlPort";
            this.ctlPort.Size = new System.Drawing.Size(91, 23);
            this.ctlPort.TabIndex = 8;
            this.ctlPort.Value = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            // 
            // ctlAutoStartup
            // 
            this.ctlAutoStartup.AutoSize = true;
            this.ctlAutoStartup.Location = new System.Drawing.Point(8, 8);
            this.ctlAutoStartup.Name = "ctlAutoStartup";
            this.ctlAutoStartup.Size = new System.Drawing.Size(162, 19);
            this.ctlAutoStartup.TabIndex = 0;
            this.ctlAutoStartup.Text = "윈도우 시작 시 자동 시작";
            this.ctlAutoStartup.UseVisualStyleBackColor = true;
            // 
            // ctlStreamingGroup
            // 
            this.ctlStreamingGroup.AutoSize = true;
            this.ctlStreamingGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlStreamingGroup.Controls.Add(this.ctlStreamingGroupPanel);
            this.ctlStreamingGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlStreamingGroup.Location = new System.Drawing.Point(8, 115);
            this.ctlStreamingGroup.Name = "ctlStreamingGroup";
            this.ctlStreamingGroup.Size = new System.Drawing.Size(221, 72);
            this.ctlStreamingGroup.TabIndex = 3;
            this.ctlStreamingGroup.TabStop = false;
            this.ctlStreamingGroup.Text = "스트리밍 옵션";
            // 
            // ctlStreamingGroupPanel
            // 
            this.ctlStreamingGroupPanel.AutoSize = true;
            this.ctlStreamingGroupPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlStreamingGroupPanel.ColumnCount = 1;
            this.ctlStreamingGroupPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.ctlStreamingGroupPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.ctlStreamingGroupPanel.Controls.Add(this.ctlShowMyRetweet, 0, 1);
            this.ctlStreamingGroupPanel.Controls.Add(this.ctlShowRetweet, 0, 0);
            this.ctlStreamingGroupPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.ctlStreamingGroupPanel.Location = new System.Drawing.Point(3, 19);
            this.ctlStreamingGroupPanel.Name = "ctlStreamingGroupPanel";
            this.ctlStreamingGroupPanel.RowCount = 2;
            this.ctlStreamingGroupPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.ctlStreamingGroupPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.ctlStreamingGroupPanel.Size = new System.Drawing.Size(215, 50);
            this.ctlStreamingGroupPanel.TabIndex = 0;
            // 
            // ctlShowMyRetweet
            // 
            this.ctlShowMyRetweet.AutoSize = true;
            this.ctlShowMyRetweet.Location = new System.Drawing.Point(3, 28);
            this.ctlShowMyRetweet.Name = "ctlShowMyRetweet";
            this.ctlShowMyRetweet.Size = new System.Drawing.Size(134, 19);
            this.ctlShowMyRetweet.TabIndex = 3;
            this.ctlShowMyRetweet.Text = "내 리트윗 다시 표시";
            this.ctlShowMyRetweet.UseVisualStyleBackColor = true;
            // 
            // ctlShowRetweet
            // 
            this.ctlShowRetweet.AutoSize = true;
            this.ctlShowRetweet.Location = new System.Drawing.Point(3, 3);
            this.ctlShowRetweet.Name = "ctlShowRetweet";
            this.ctlShowRetweet.Size = new System.Drawing.Size(146, 19);
            this.ctlShowRetweet.TabIndex = 2;
            this.ctlShowRetweet.Text = "리트윗된 내 트윗 표시";
            this.ctlShowRetweet.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.Controls.Add(this.ctlOK, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.ctlCancel, 1, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Right;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(95, 193);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(134, 31);
            this.tableLayoutPanel2.TabIndex = 5;
            // 
            // ctlOK
            // 
            this.ctlOK.AutoSize = true;
            this.ctlOK.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ctlOK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlOK.Location = new System.Drawing.Point(3, 3);
            this.ctlOK.Name = "ctlOK";
            this.ctlOK.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.ctlOK.Size = new System.Drawing.Size(61, 25);
            this.ctlOK.TabIndex = 0;
            this.ctlOK.Text = "확인";
            this.ctlOK.UseVisualStyleBackColor = true;
            this.ctlOK.Click += new System.EventHandler(this.ctlOK_Click);
            // 
            // ctlCancel
            // 
            this.ctlCancel.AutoSize = true;
            this.ctlCancel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.ctlCancel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlCancel.Location = new System.Drawing.Point(70, 3);
            this.ctlCancel.Name = "ctlCancel";
            this.ctlCancel.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.ctlCancel.Size = new System.Drawing.Size(61, 25);
            this.ctlCancel.TabIndex = 1;
            this.ctlCancel.Text = "취소";
            this.ctlCancel.UseVisualStyleBackColor = true;
            this.ctlCancel.Click += new System.EventHandler(this.ctlCancel_Click);
            // 
            // ConfigWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(237, 247);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MinimumSize = new System.Drawing.Size(250, 39);
            this.Name = "ConfigWindow";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "스트리밍 호흡기 설정";
            this.Load += new System.EventHandler(this.ConfigWindow_Load);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ctlProxyGroup.ResumeLayout(false);
            this.ctlProxyGroup.PerformLayout();
            this.ctlProxyGroupPanel.ResumeLayout(false);
            this.ctlProxyGroupPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ctlPort)).EndInit();
            this.ctlStreamingGroup.ResumeLayout(false);
            this.ctlStreamingGroup.PerformLayout();
            this.ctlStreamingGroupPanel.ResumeLayout(false);
            this.ctlStreamingGroupPanel.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.CheckBox ctlAutoStartup;
        private System.Windows.Forms.GroupBox ctlStreamingGroup;
        private System.Windows.Forms.TableLayoutPanel ctlStreamingGroupPanel;
        private System.Windows.Forms.CheckBox ctlShowMyRetweet;
        private System.Windows.Forms.CheckBox ctlShowRetweet;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Button ctlOK;
        private System.Windows.Forms.Button ctlCancel;
        private System.Windows.Forms.GroupBox ctlProxyGroup;
        private System.Windows.Forms.TableLayoutPanel ctlProxyGroupPanel;
        private System.Windows.Forms.CheckBox ctlUseHTTPS;
        private System.Windows.Forms.Label ctlPortLabel;
        private System.Windows.Forms.NumericUpDown ctlPort;
    }
}
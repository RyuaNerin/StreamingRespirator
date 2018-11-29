namespace StreamingRespirator.Core.Windows
{
    partial class LoginWindow
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
            this.pnl = new System.Windows.Forms.TableLayoutPanel();
            this.ctlUsername = new System.Windows.Forms.TextBox();
            this.ctlPassword = new System.Windows.Forms.TextBox();
            this.ctlLogin = new System.Windows.Forms.Button();
            this.pnl.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnl
            // 
            this.pnl.AutoSize = true;
            this.pnl.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnl.ColumnCount = 2;
            this.pnl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.pnl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.pnl.Controls.Add(this.ctlUsername, 0, 0);
            this.pnl.Controls.Add(this.ctlPassword, 0, 1);
            this.pnl.Controls.Add(this.ctlLogin, 1, 0);
            this.pnl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnl.Location = new System.Drawing.Point(0, 0);
            this.pnl.Margin = new System.Windows.Forms.Padding(0);
            this.pnl.Name = "pnl";
            this.pnl.Padding = new System.Windows.Forms.Padding(4);
            this.pnl.RowCount = 2;
            this.pnl.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.pnl.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.pnl.Size = new System.Drawing.Size(228, 67);
            this.pnl.TabIndex = 0;
            // 
            // ctlUsername
            // 
            this.ctlUsername.Location = new System.Drawing.Point(4, 4);
            this.ctlUsername.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.ctlUsername.Name = "ctlUsername";
            this.ctlUsername.Size = new System.Drawing.Size(160, 23);
            this.ctlUsername.TabIndex = 0;
            // 
            // ctlPassword
            // 
            this.ctlPassword.Location = new System.Drawing.Point(4, 31);
            this.ctlPassword.Margin = new System.Windows.Forms.Padding(0);
            this.ctlPassword.Name = "ctlPassword";
            this.ctlPassword.Size = new System.Drawing.Size(160, 23);
            this.ctlPassword.TabIndex = 1;
            this.ctlPassword.UseSystemPasswordChar = true;
            // 
            // ctlLogin
            // 
            this.ctlLogin.AutoSize = true;
            this.ctlLogin.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlLogin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlLogin.Location = new System.Drawing.Point(167, 4);
            this.ctlLogin.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.ctlLogin.Name = "ctlLogin";
            this.ctlLogin.Padding = new System.Windows.Forms.Padding(5);
            this.pnl.SetRowSpan(this.ctlLogin, 2);
            this.ctlLogin.Size = new System.Drawing.Size(63, 59);
            this.ctlLogin.TabIndex = 2;
            this.ctlLogin.Text = "로그인";
            this.ctlLogin.UseVisualStyleBackColor = true;
            this.ctlLogin.Click += new System.EventHandler(this.ctlLogin_Click);
            // 
            // LoginWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(228, 67);
            this.Controls.Add(this.pnl);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoginWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "스트리밍 호흡기 로그인";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.LoginWindow_KeyDown);
            this.pnl.ResumeLayout(false);
            this.pnl.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel pnl;
        private System.Windows.Forms.TextBox ctlUsername;
        private System.Windows.Forms.TextBox ctlPassword;
        private System.Windows.Forms.Button ctlLogin;
    }
}

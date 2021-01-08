
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoginWindow));
            this.ctlCookie = new System.Windows.Forms.TextBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.ctlOkFile = new System.Windows.Forms.Button();
            this.ctlOkText = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.ctlHelp = new System.Windows.Forms.Label();
            this.ofg = new System.Windows.Forms.OpenFileDialog();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // ctlCookie
            // 
            this.ctlCookie.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlCookie.Location = new System.Drawing.Point(8, 39);
            this.ctlCookie.Multiline = true;
            this.ctlCookie.Name = "ctlCookie";
            this.ctlCookie.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.ctlCookie.Size = new System.Drawing.Size(568, 271);
            this.ctlCookie.TabIndex = 3;
            this.ctlCookie.WordWrap = false;
            this.ctlCookie.TextChanged += new System.EventHandler(this.ctlCookie_TextChanged);
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.Controls.Add(this.ctlOkFile, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.ctlOkText, 1, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Right;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(386, 316);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(190, 37);
            this.tableLayoutPanel2.TabIndex = 1;
            // 
            // ctlOkFile
            // 
            this.ctlOkFile.AutoSize = true;
            this.ctlOkFile.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlOkFile.Location = new System.Drawing.Point(3, 3);
            this.ctlOkFile.Name = "ctlOkFile";
            this.ctlOkFile.Padding = new System.Windows.Forms.Padding(10, 3, 10, 3);
            this.ctlOkFile.Size = new System.Drawing.Size(117, 31);
            this.ctlOkFile.TabIndex = 1;
            this.ctlOkFile.Text = "쿠키 파일 선택";
            this.ctlOkFile.UseVisualStyleBackColor = true;
            this.ctlOkFile.Click += new System.EventHandler(this.ctlOkFile_Click);
            // 
            // ctlOkText
            // 
            this.ctlOkText.AutoSize = true;
            this.ctlOkText.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlOkText.Location = new System.Drawing.Point(126, 3);
            this.ctlOkText.Name = "ctlOkText";
            this.ctlOkText.Padding = new System.Windows.Forms.Padding(10, 3, 10, 3);
            this.ctlOkText.Size = new System.Drawing.Size(61, 31);
            this.ctlOkText.TabIndex = 0;
            this.ctlOkText.Text = "추가";
            this.ctlOkText.UseVisualStyleBackColor = true;
            this.ctlOkText.Click += new System.EventHandler(this.ctlOkText_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.ctlCookie, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.ctlHelp, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(5);
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(584, 361);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // ctlHelp
            // 
            this.ctlHelp.AutoSize = true;
            this.ctlHelp.Cursor = System.Windows.Forms.Cursors.Hand;
            this.ctlHelp.Dock = System.Windows.Forms.DockStyle.Top;
            this.ctlHelp.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.ctlHelp.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.ctlHelp.Location = new System.Drawing.Point(8, 5);
            this.ctlHelp.Name = "ctlHelp";
            this.ctlHelp.Padding = new System.Windows.Forms.Padding(5);
            this.ctlHelp.Size = new System.Drawing.Size(568, 31);
            this.ctlHelp.TabIndex = 4;
            this.ctlHelp.Text = "반드시 도움말을 읽어주세요";
            this.ctlHelp.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.ctlHelp.Click += new System.EventHandler(this.ctlHelp_Click);
            // 
            // ofg
            // 
            this.ofg.Filter = "twitter.com_cookies.txt|twitter.com_cookies.txt|*|*";
            this.ofg.Title = "twitter.com_cookies.txt 파일 선택";
            // 
            // LoginWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 361);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MinimizeBox = false;
            this.Name = "LoginWindow";
            this.ShowIcon = false;
            this.Text = "계정 추가";
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox ctlCookie;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Button ctlOkFile;
        private System.Windows.Forms.Button ctlOkText;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label ctlHelp;
        private System.Windows.Forms.OpenFileDialog ofg;
    }
}
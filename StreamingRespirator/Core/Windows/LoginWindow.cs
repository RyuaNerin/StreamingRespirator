using System;
using System.Windows.Forms;

namespace StreamingRespirator.Core.Windows
{
    public partial class LoginWindow : Form
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        public string Username => this.ctlUsername.Text;
        public string Password => this.ctlPassword.Text;

        private void ctlLogin_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void LoginWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (string.IsNullOrWhiteSpace(this.ctlUsername.Text))
                {
                    this.ctlUsername.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(this.ctlPassword.Text))
                {
                    this.ctlPassword.Focus();
                    return;
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}

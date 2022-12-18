﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChattyVibes
{
    public partial class FrmLog : ChildForm
    {
        public FrmLog()
        {
            InitializeComponent();
        }

        private void FrmLog_Load(object sender, EventArgs e)
        {
            tbLog.Lines = MainFrm._logMessages.ToArray();
            tbLog.Update();
        }

        internal void AddLogMsg(string msg)
        {
            tbLog.AppendText(msg);
        }
    }
}

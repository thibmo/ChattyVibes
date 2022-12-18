﻿using ST.Library.UI.NodeEditor;

namespace ChattyVibes.Nodes.Branch
{
    [STNode("/Branch", "LauraRozier", "", "", "Flow branch node")]
    internal class FlowBranchNode : BranchNode
    {
        private object _val = null;

        protected override void OnCreate()
        {
            _type = typeof(object);
            base.OnCreate();
            Title = "Flow Branch";
            AutoSize = false;
            Width = 152;
            Height = 80;

            m_op_in.DataTransfer += new STNodeOptionEventHandler(m_in_DataTransfer);
        }

        private void m_in_DataTransfer(object sender, STNodeOptionEventArgs e)
        {
            if (e.Status == ConnectionStatus.Connected)
                _val = e.TargetOption.Data;
            else
                _val = null;

            HandleCondition();
        }

        protected override void HandleCondition()
        {
            if (_condition)
                m_op_true_out.TransferData(_val);
            else
                m_op_false_out.TransferData(_val);
        }
    }
}

﻿using ST.Library.UI.NodeEditor;
using SysMath = System.Math;

namespace ChattyVibes.Nodes.Math.Int
{
    [STNode("/Math/Int", "LauraRozier", "", "", "Returns the larger of two numbers.")]
    internal class IntMaxNode : IntNode
    {
        private int _aVal = 0;
        [STNodeProperty("A", "A value")]
        public int Min
        {
            get { return _aVal; }
            set
            {
                _aVal = value;
                ProcessResult();
            }
        }
        private int _bVal = 0;
        [STNodeProperty("B", "B value")]
        public int Max
        {
            get { return _bVal; }
            set
            {
                _bVal = value;
                ProcessResult();
            }
        }

        private STNodeOption m_in_A;
        private STNodeOption m_in_B;
        private STNodeOption m_out;

        protected override void OnCreate()
        {
            base.OnCreate();
            Title = "Int Max";

            m_in_A = InputOptions.Add("0", typeof(int), true);
            m_in_B = InputOptions.Add("0", typeof(int), true);
            m_out = OutputOptions.Add("0", typeof(int), false);

            m_in_A.DataTransfer += new STNodeOptionEventHandler(m_in_DataTransfer);
            m_in_B.DataTransfer += new STNodeOptionEventHandler(m_in_DataTransfer);
            m_out.TransferData(_aVal);
        }

        private void m_in_DataTransfer(object sender, STNodeOptionEventArgs e)
        {
            if (e.Status == ConnectionStatus.Connected && e.TargetOption.Data != null)
            {
                if (sender == m_in_A)
                    _aVal = (int)e.TargetOption.Data;
                else
                    _bVal = (int)e.TargetOption.Data;
            }
            else
            {
                if (sender == m_in_A)
                    _aVal = 0;
                else
                    _bVal = 0;
            }

            ProcessResult();
        }

        private void ProcessResult()
        {
            int result = SysMath.Max(_aVal, _bVal);
            SetOptionText(m_in_A, _aVal.ToString());
            SetOptionText(m_in_B, _bVal.ToString());
            SetOptionText(m_out, result.ToString());
            m_out.TransferData(result);
        }
    }
}

﻿using ST.Library.UI.NodeEditor;
using System.Drawing;

namespace ChattyVibes.Nodes
{
    internal class FloatNode : STNode
    {
        protected readonly StringFormat _sf = new StringFormat
        {
            LineAlignment = StringAlignment.Center,
            Alignment = StringAlignment.Center,
        };

        protected override void OnCreate()
        {
            base.OnCreate();
            TitleColor = Color.FromArgb(200, FrmBindingGraphs.C_COLOR_FLOAT);
        }
    }
}

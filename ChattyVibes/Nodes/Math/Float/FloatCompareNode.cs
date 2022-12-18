﻿using ST.Library.UI.NodeEditor;
using System.Drawing;

namespace ChattyVibes.Nodes.Math.Float
{
    [STNode("/Math/Float", "LauraRozier", "", "", "Compare two numbers")]
    internal class FloatCompareNode : CompareNode<float>
    {
        protected override void OnCreate()
        {
            _defaultVal = 0.0f;
            base.OnCreate();
            Title = "Float Compare";
            TitleColor = Color.FromArgb(200, FrmBindingGraphs.C_COLOR_FLOAT);
        }
    }
}

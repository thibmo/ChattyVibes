﻿using ST.Library.UI.NodeEditor;
using System.Drawing;
using SysColor = System.Drawing.Color;

namespace ChattyVibes.Nodes.Graphics.Color
{
    //[STNode("/Graphics/Color", "LauraRozier", "", "", "This is a color input node")]
    internal class ColorInputNode : STNode
    {
        private SysColor _color = SysColor.LightGray;
        [STNodeProperty("Color", "The color value", DescriptorType = typeof(DescriptorForColor))]
        public SysColor Color
        {
            get { return _color; }
            set
            {
                _color = value;
                m_ctrl_btn.BackColor = value;
            }
        }

        private NodeColorButton m_ctrl_btn;
        private STNodeOption m_out_color;

        protected override void OnCreate()
        {
            base.OnCreate();
            AutoSize = false;
            Width = 120;
            Height = 46;
            TitleColor = SysColor.FromArgb(200, FrmBindingGraphs.C_COLOR_COLOR);
            Title = "Color Input";

            m_out_color = OutputOptions.Add("Color", typeof(SysColor), true);
            m_out_color.TransferData(_color);

            m_ctrl_btn = new NodeColorButton
            {
                Text = "",
                BackColor = _color,
                DisplayRectangle = new Rectangle(5, 5, 50, 16)
            };
            m_ctrl_btn.ValueChanged += (s, e) =>
            {
                _color = m_ctrl_btn.BackColor;
                m_out_color.TransferData(_color);
                Invalidate();
            };
            Controls.Add(m_ctrl_btn);
        }
    }
}
﻿using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;

namespace ChattyVibes
{
    public partial class FrmBindingGraphs : ChildForm
    {
        public static class Keyboard
        {
            private static readonly HashSet<Keys> _keys = new HashSet<Keys>();

            public static void OnKeyDown(Keys key)
            {
                if (!_keys.Contains(key))
                    _keys.Add(key);
            }

            public static void OnKeyUp(Keys key)
            {
                if (_keys.Contains(key))
                    _keys.Remove(key);
            }

            public static bool IsKeyDown(Keys key) =>
                _keys.Contains(key);

            public static bool IsAnyKeyDown
            {
                get { return _keys.Count > 0; }
            }
        }

        private readonly Timer _timer = new Timer();
        private const int C_TIMER_DELAY_THRESHOLD = 50;
        private readonly static Color C_ALERT_ERR = Color.FromArgb(125, Color.Red);
        private readonly static Color C_ALERT_WARN = Color.FromArgb(125, Color.Yellow);
        private readonly static Color C_ALERT_OK = Color.FromArgb(125, Color.Green);

        private int _timerDelayTick = 0;
        private string _currentGraph = string.Empty;

        public FrmBindingGraphs()
        {
            KeyPreview = true;
            InitializeComponent();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Keyboard.IsKeyDown(keyData & ~Keys.Shift))
                return true;

            bool InitMove()
            {
                if (!Keyboard.IsAnyKeyDown)
                {
                    _timerDelayTick = 0;
                    _timer.Start();
                }

                Keyboard.OnKeyDown(keyData & ~Keys.Shift);
                return true;
            }

            switch (keyData)
            {
                case Keys.Up:
                case Keys.Shift | Keys.Up:
                    {
                        foreach (var node in nodeEditorPanel.Editor.GetSelectedNode())
                            node.Top -= 1;

                        return InitMove();
                    }
                case Keys.Down:
                case Keys.Shift | Keys.Down:
                    {
                        foreach (var node in nodeEditorPanel.Editor.GetSelectedNode())
                            node.Top += 1;

                        return InitMove();
                    }
                case Keys.Left:
                case Keys.Shift | Keys.Left:
                    {
                        foreach (var node in nodeEditorPanel.Editor.GetSelectedNode())
                            node.Left -= 1;

                        return InitMove();
                    }
                case Keys.Right:
                case Keys.Shift | Keys.Right:
                    {
                        foreach (var node in nodeEditorPanel.Editor.GetSelectedNode())
                            node.Left += 1;

                        return InitMove();
                    }
                default: return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.A:
                    {
                        if ((e.Modifiers & Keys.Control) == Keys.Control)
                        {
                            foreach (STNode node in nodeEditorPanel.Editor.Nodes)
                                node.SetSelected(true, true);

                            e.Handled = true;
                        }

                        return;
                    }
                case Keys.S:
                    {
                        if ((e.Modifiers & Keys.Control) == Keys.Control)
                        {
                            SaveGraph();
                            e.Handled = true;
                        }

                        return;
                    }
                case Keys.R:
                    {
                        if ((e.Modifiers & Keys.Control) == Keys.Control)
                        {
                            nodeEditorPanel.Editor.Nodes.Clear();
                            nodeEditorPanel.Editor.Modified = false;

                            if (!string.IsNullOrEmpty(_currentGraph))
                            {
                                try
                                {
                                    nodeEditorPanel.Editor.LoadCanvas(Path.Combine(MainForm._graphDir, $"{_currentGraph}{MainForm._graphFileExt}"));
                                    nodeEditorPanel.Editor.ShowAlert($"Reloaded `{_currentGraph}` successfully", Color.White, C_ALERT_OK);
                                }
                                catch
                                {
                                    nodeEditorPanel.Editor.ShowAlert($"Unable to reload `{_currentGraph}`", Color.White, C_ALERT_ERR);
                                }
                            }

                            e.Handled = true;
                        }

                        return;
                    }
                case Keys.L:
                    {
                        if ((e.Modifiers & Keys.Control) == Keys.Control)
                        {
                            foreach (STNode node in nodeEditorPanel.Editor.GetSelectedNode())
                                node.LockLocation = true;

                            nodeEditorPanel.Editor.Modified = true;
                            nodeEditorPanel.Editor.ShowAlert("Locked selected node(s)", Color.White, C_ALERT_WARN);
                            e.Handled = true;
                        }

                        return;
                    }
                case Keys.U:
                    {
                        if ((e.Modifiers & Keys.Control) == Keys.Control)
                        {
                            foreach (STNode node in nodeEditorPanel.Editor.GetSelectedNode())
                                node.LockLocation = false;

                            nodeEditorPanel.Editor.Modified = true;
                            nodeEditorPanel.Editor.ShowAlert("Unlocked selected node(s)", Color.White, C_ALERT_WARN);
                            e.Handled = true;
                        }
                        return;
                    }
                case Keys.Delete:
                    {
                        var nodes = nodeEditorPanel.Editor.GetSelectedNode();

                        foreach (STNode node in nodes)
                            nodeEditorPanel.Editor.Nodes.Remove(node);

                        nodeEditorPanel.Editor.ShowAlert("Deleted selected node(s)", Color.White, C_ALERT_WARN);
                        e.Handled = true;
                        return;
                    }
                case Keys.Up:
                case Keys.Shift | Keys.Up:
                case Keys.Down:
                case Keys.Shift | Keys.Down:
                case Keys.Left:
                case Keys.Shift | Keys.Left:
                case Keys.Right:
                case Keys.Shift | Keys.Right:
                    {
                        Keyboard.OnKeyUp(e.KeyCode & ~Keys.Shift);

                        if (!Keyboard.IsAnyKeyDown)
                            _timer.Stop();

                        e.Handled = true;
                        return;
                    }
                default:
                    {
                        base.OnKeyUp(e);
                        return;
                    }
            }
        }

        protected override void OnLoad(EventArgs ea)
        {
            base.OnLoad(ea);
            nodeEditorPanel.LoadAssembly(Application.ExecutablePath);
            nodeEditorPanel.Y = Height - 100;
            nodeEditorPanel.Editor.OptionConnected += new STNodeEditorOptionEventHandler((s, e) =>
            {
                string msg = "";

                switch (e.Status)
                {
                    case ConnectionStatus.NoOwner: msg = "No Owner"; break;
                    case ConnectionStatus.SameOwner: msg = "Same Owner"; break;
                    case ConnectionStatus.SameInputOrOutput: msg = "Both are input or output options"; break;
                    case ConnectionStatus.ErrorType: msg = "Different data types"; break;
                    case ConnectionStatus.SingleOption: msg = "Single link node"; break;
                    case ConnectionStatus.Loop: msg = "Circular path"; break;
                    case ConnectionStatus.Exists: msg = "Existing connection"; break;
                    case ConnectionStatus.EmptyOption: msg = "Blank option"; break;
                    case ConnectionStatus.Connected: msg = "Connected"; break;
                    case ConnectionStatus.DisConnected: msg = "Disconnected"; break;
                    case ConnectionStatus.Locked: msg = "Node is locked"; break;
                    case ConnectionStatus.Reject: msg = "Operation denied"; break;
                    case ConnectionStatus.Connecting: msg = "Being connected"; break;
                    case ConnectionStatus.DisConnecting: msg = "Disconnecting"; break;
                    case ConnectionStatus.InvalidType: msg = "Invalid data type"; break;
                }
                nodeEditorPanel.Editor.ShowAlert(
                    msg, Color.White,
                    e.Status == ConnectionStatus.Connected ? C_ALERT_OK : C_ALERT_ERR
                );
            });
            nodeEditorPanel.Editor.CanvasScaled += new EventHandler((s, e) =>
            {
                nodeEditorPanel.Editor.ShowAlert(
                    nodeEditorPanel.Editor.CanvasScale.ToString("F2"),
                    Color.White, C_ALERT_WARN
                );
            });
            nodeEditorPanel.Editor.NodeAdded += new STNodeEditorEventHandler((s, e) => e.Node.ContextMenuStrip = contextMenuStrip1);

            nodeEditorPanel.PropertyGrid.SetInfoKey("Author", "Mail", "Link", "Show Help");
            nodeEditorPanel.Editor.SetTypeColor(typeof(bool), Constants.C_COLOR_BOOL);
            nodeEditorPanel.Editor.SetTypeColor(typeof(float), Constants.C_COLOR_FLOAT);
            nodeEditorPanel.Editor.SetTypeColor(typeof(int), Constants.C_COLOR_INT);
            nodeEditorPanel.Editor.SetTypeColor(typeof(uint), Constants.C_COLOR_UINT);
            nodeEditorPanel.Editor.SetTypeColor(typeof(char), Constants.C_COLOR_CHAR);
            nodeEditorPanel.Editor.SetTypeColor(typeof(string), Constants.C_COLOR_STRING);
            nodeEditorPanel.Editor.SetTypeColor(typeof(DateTime), Constants.C_COLOR_DATETIME);
            nodeEditorPanel.Editor.SetTypeColor(typeof(Enum), Constants.C_COLOR_ENUM);

            nodeEditorPanel.TreeView.PropertyGrid.SetInfoKey("Author", "Mail", "Link", "Show Help");
            nodeEditorPanel.TreeView.Editor.SetTypeColor(typeof(bool), Constants.C_COLOR_BOOL);
            nodeEditorPanel.TreeView.Editor.SetTypeColor(typeof(float), Constants.C_COLOR_FLOAT);
            nodeEditorPanel.TreeView.Editor.SetTypeColor(typeof(int), Constants.C_COLOR_INT);
            nodeEditorPanel.TreeView.Editor.SetTypeColor(typeof(uint), Constants.C_COLOR_UINT);
            nodeEditorPanel.TreeView.Editor.SetTypeColor(typeof(char), Constants.C_COLOR_CHAR);
            nodeEditorPanel.TreeView.Editor.SetTypeColor(typeof(string), Constants.C_COLOR_STRING);
            nodeEditorPanel.TreeView.Editor.SetTypeColor(typeof(DateTime), Constants.C_COLOR_DATETIME);
            nodeEditorPanel.TreeView.Editor.SetTypeColor(typeof(Enum), Constants.C_COLOR_ENUM);

            contextMenuStrip1.ShowImageMargin = false;
            contextMenuStrip1.Renderer = new ToolStripRendererEx();

            foreach (var file in Directory.GetFiles(MainForm._graphDir, MainForm._graphFilePtrn, SearchOption.TopDirectoryOnly))
                lbGraphs.Items.Add(Path.GetFileNameWithoutExtension(file));

            _timer.Interval = 10;
            _timer.Tick += new EventHandler(Timer_Tick);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            SaveGraph();
            nodeEditorPanel.Editor.Nodes.Clear();
            base.OnFormClosing(e);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!Keyboard.IsAnyKeyDown)
                return;

            // We wait (_timer.Interval * C_TIMER_DELAY_THRESHOLD) ms before we process button hold scroll
            if (_timerDelayTick < C_TIMER_DELAY_THRESHOLD)
            {
                _timerDelayTick++;
                return;
            }

            foreach (var node in nodeEditorPanel.Editor.GetSelectedNode())
            {
                if (Keyboard.IsKeyDown(Keys.Up))
                    node.Top -= 2;
                else if (Keyboard.IsKeyDown(Keys.Down))
                    node.Top += 2;

                if (Keyboard.IsKeyDown(Keys.Left))
                    node.Left -= 2;
                else if (Keyboard.IsKeyDown(Keys.Right))
                    node.Left += 2;
            }
        }

        private string AskNewGraphName()
        {
            string name = string.Empty;
            var result = ShowInputDialogBox(ref name, "Enter the new graph's name", "New Graph");

            if (result == DialogResult.OK)
            {
                if (name.EndsWith(MainForm._graphFileExt))
                    name = Path.GetFileNameWithoutExtension(name);

                if (File.Exists(Path.Combine(MainForm._graphDir, $"{name}{MainForm._graphFileExt}")))
                {
                    MessageBox.Show(
                        "Error - This graph already exists!",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error
                    );
                    return AskNewGraphName();
                }
                else
                {
                    return name;
                }
            }

            return string.Empty;
        }

        private void SaveGraph()
        {
            if (nodeEditorPanel.Editor.Modified)
            {
                var result = MessageBox.Show(
                    $"Do you wish to save your changes?",
                    "Confirm Save Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    if (string.IsNullOrEmpty(_currentGraph))
                    {
                        string name = AskNewGraphName();

                        if (!string.IsNullOrEmpty(name))
                        {
                            _currentGraph = name;
                            lbGraphs.Items.Add(name);
                        }
                    }

                    if (!string.IsNullOrEmpty(_currentGraph))
                    {
                        try
                        {
                            nodeEditorPanel.Editor.SaveCanvas(Path.Combine(MainForm._graphDir, $"{_currentGraph}{MainForm._graphFileExt}"));
                            nodeEditorPanel.Editor.ShowAlert($"Saved `{_currentGraph}` successfully", Color.White, C_ALERT_OK);
                        }
                        catch
                        {
                            nodeEditorPanel.Editor.ShowAlert($"Unable to save `{_currentGraph}`", Color.White, C_ALERT_ERR);
                        }
                    }
                }
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            
            string name = AskNewGraphName();

            if (!string.IsNullOrEmpty(name))
            {
                SaveGraph();
                _currentGraph = name;
                lbGraphs.SelectedIndex = lbGraphs.Items.Add(name);
                nodeEditorPanel.Editor.Nodes.Clear();
                nodeEditorPanel.Editor.Modified = false;

                try
                {
                    var path = Path.Combine(MainForm._graphDir, $"{name}{MainForm._graphFileExt}");
                    nodeEditorPanel.Editor.SaveCanvas(path);
                    nodeEditorPanel.Editor.ShowAlert($"Created `{name}` successfully", Color.White, C_ALERT_OK);
                }
                catch
                {
                    nodeEditorPanel.Editor.ShowAlert($"Unable to create `{name}`", Color.White, C_ALERT_ERR);
                }
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentGraph))
                return;

            string path = Path.Combine(MainForm._graphDir, $"{_currentGraph}{MainForm._graphFileExt}");

            if (!File.Exists(path))
                return;

            var result = MessageBox.Show(
                $"Are you sure you wish to delete `{_currentGraph}`?",
                "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question
            );

            if (result == DialogResult.No)
                return;

            File.Delete(path);
            _currentGraph = string.Empty;
            nodeEditorPanel.Editor.Nodes.Clear();
            nodeEditorPanel.Editor.Modified = false;
            lbGraphs.Items.RemoveAt(lbGraphs.SelectedIndex);
            lbGraphs.SelectedIndex = -1;
        }

        private void lbGraphs_SelectedIndexChanged(object sender, EventArgs e)
        {
            string curItem = (string)lbGraphs.SelectedItem;
            SaveGraph();
            nodeEditorPanel.Editor.Nodes.Clear();
            nodeEditorPanel.Editor.Modified = false;

            if (!string.IsNullOrEmpty(curItem))
            {
                _currentGraph = curItem;

                try
                {
                    nodeEditorPanel.Editor.LoadCanvas(Path.Combine(MainForm._graphDir, $"{curItem}{MainForm._graphFileExt}"));
                    nodeEditorPanel.Editor.ShowAlert($"Loaded `{_currentGraph}` successfully", Color.White, C_ALERT_OK);
                }
                catch
                {
                    nodeEditorPanel.Editor.ShowAlert($"Unable to load `{_currentGraph}`", Color.White, C_ALERT_ERR);
                }
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (nodeEditorPanel.Editor.ActiveNode == null)
                return;

            nodeEditorPanel.Editor.Nodes.Remove(nodeEditorPanel.Editor.ActiveNode);
        }

        private void lockLocationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (nodeEditorPanel.Editor.ActiveNode == null)
                return;

            nodeEditorPanel.Editor.ActiveNode.LockLocation = !nodeEditorPanel.Editor.ActiveNode.LockLocation;
            nodeEditorPanel.Editor.Modified = true;
        }

        private static DialogResult ShowInputDialogBox(ref string input, string prompt, string title = "Title", int width = 300, int height = 80)
        {
            Size size = new Size(width, height);

            Form inputBox = new Form
            {
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ClientSize = size,
                Text = title
            };

            Label label = new Label
            {
                Text = prompt,
                Location = new Point(5, 5),
                Width = size.Width - 10
            };
            TextBox textBox = new TextBox
            {
                Size = new Size(size.Width - 10, 23),
                Location = new Point(5, label.Location.Y + 20),
                Text = input,
                ForeColor = Color.White,
                BackColor = Color.Red,
                MaxLength = 250
            };
            Button okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Name = "okButton",
                Size = new Size(75, 23),
                Text = "&OK",
                Location = new Point(size.Width - 80 - 80, size.Height - 30),
                Enabled = false
            };
            Button cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Name = "cancelButton",
                Size = new Size(75, 23),
                Text = "&Cancel",
                Location = new Point(size.Width - 80, size.Height - 30)
            };

            textBox.TextChanged += new EventHandler((s, e) =>
            {
                if (textBox.Text.Length <= 0 || textBox.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    textBox.ForeColor = Color.White;
                    textBox.BackColor = Color.Red;
                    okButton.Enabled = false;
                }
                else
                {
                    textBox.ResetForeColor();
                    textBox.ResetBackColor();
                    okButton.Enabled = true;
                }
            });

            inputBox.Controls.Add(label);
            inputBox.Controls.Add(textBox);
            inputBox.Controls.Add(okButton);
            inputBox.Controls.Add(cancelButton);

            inputBox.AcceptButton = okButton;
            inputBox.CancelButton = cancelButton;

            DialogResult result = inputBox.ShowDialog();

            if (result == DialogResult.OK)
                input = textBox.Text;

            return result;
        }
    }
}

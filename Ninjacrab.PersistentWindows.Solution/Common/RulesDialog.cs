using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PersistentWindows.Common
{
    /// <summary>
    /// Editor for the three lists of <see cref="Rules"/>, one entry per line.
    /// </summary>
    public class RulesDialog : Form
    {
        private TextBox pauseProcessBox;
        private TextBox excludeProcessBox;
        private TextBox excludeTitleBox;

        public string[] PauseProcess { get { return pauseProcessBox.Lines; } }
        public string[] ExcludeProcess { get { return excludeProcessBox.Lines; } }
        public string[] ExcludeTitle { get { return excludeTitleBox.Lines; } }

        public RulesDialog()
        {
            Text = Lang.T("rules.title");
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(960, 470);
            MinimumSize = new Size(640, 360);
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            Padding = new Padding(10);

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4,
            };
            for (int i = 0; i < 3; ++i)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            pauseProcessBox = AddList(table, 0, "rules.pauseProcess", "rules.pauseProcessHint", Rules.PauseProcess, false);
            excludeProcessBox = AddList(table, 1, "rules.excludeProcess", "rules.excludeProcessHint", Rules.ExcludeProcess, false);
            excludeTitleBox = AddList(table, 2, "rules.excludeTitle", "rules.excludeTitleHint", Rules.ExcludeTitle, true);

            var note = new Label
            {
                Text = Lang.T("rules.onePerLine"),
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(0, 0, 0, 6),
            };

            var ok = new Button { Text = Lang.T("dlg.ok"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 30) };
            var cancel = new Button { Text = Lang.T("dlg.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 30) };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0),
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            var top = new Panel { Dock = DockStyle.Top, AutoSize = true };
            top.Controls.Add(note);

            Controls.Add(table);
            Controls.Add(top);
            Controls.Add(buttons);

            // start with the caret at the end instead of the whole first list selected
            Shown += (s, e) => pauseProcessBox.Select(pauseProcessBox.TextLength, 0);

            AcceptButton = null; // Enter adds a new line in the lists
            CancelButton = cancel;
        }

        private TextBox AddList(TableLayoutPanel table, int column, string titleKey, string hintKey, string[] items, bool byTitle)
        {
            var title = new Label
            {
                Text = Lang.T(titleKey),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(3, 3, 3, 2),
            };

            var hint = new Label
            {
                Text = Lang.T(hintKey),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 0, 3, 6),
            };

            var box = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                WordWrap = false,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Lines = items,
            };

            var add = new Button
            {
                Text = Lang.T("rules.addRunning") + " ▾",
                Height = 30,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            };
            add.Click += (s, e) => ShowRunningWindows(add, box, byTitle);

            // keep the three hint labels wrapping inside their own column
            table.Layout += (s, e) =>
            {
                int width = Math.Max(50, table.GetColumnWidths()[column] - 8);
                title.MaximumSize = new Size(width, 0);
                hint.MaximumSize = new Size(width, 0);
            };

            table.Controls.Add(title, column, 0);
            table.Controls.Add(hint, column, 1);
            table.Controls.Add(box, column, 2);
            table.Controls.Add(add, column, 3);
            return box;
        }

        private void ShowRunningWindows(Control anchor, TextBox box, bool byTitle)
        {
            var menu = new ContextMenuStrip();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<KeyValuePair<string, string>>(); // value to add, text to show

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string windowTitle = p.MainWindowTitle;
                    if (string.IsNullOrEmpty(windowTitle))
                        continue;

                    string value = byTitle ? windowTitle : p.ProcessName;
                    if (!seen.Add(value))
                        continue;

                    string shown = byTitle ? windowTitle + "   (" + p.ProcessName + ")" : p.ProcessName + "   -   " + windowTitle;
                    if (shown.Length > 100)
                        shown = shown.Substring(0, 100) + "...";
                    entries.Add(new KeyValuePair<string, string>(value, shown.Replace("&", "&&")));
                }
                catch (Exception)
                {
                    // process exited or is not accessible
                }
                finally
                {
                    p.Dispose();
                }
            }

            entries.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.CurrentCultureIgnoreCase));
            foreach (var entry in entries)
            {
                string value = entry.Key;
                menu.Items.Add(entry.Value, null, (s, e) => AddLine(box, value));
            }

            menu.Closed += (s, e) => BeginInvoke((Action)menu.Dispose);
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private static void AddLine(TextBox box, string value)
        {
            var lines = new List<string>();
            foreach (var line in box.Lines)
            {
                if (line.Trim().Length == 0)
                    continue;
                if (string.Equals(line.Trim(), value, StringComparison.OrdinalIgnoreCase))
                    return;
                lines.Add(line);
            }
            lines.Add(value);
            box.Lines = lines.ToArray();
        }
    }
}

using DiscordRPC;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Button = System.Windows.Forms.Button;

namespace CustomRPC
{
    /// <summary>
    /// Editor for user-defined Dynamic Presence presets.
    /// </summary>
    public sealed class DynamicPresenceForm : Form
    {
        readonly List<DynamicStatus> statuses;
        CheckBox checkBoxEnabled;
        NumericUpDown numericUpDownInterval;
        ComboBox comboBoxMode;
        ListBox listBoxStatuses;
        TextBox textBoxName;
        TextBox textBoxDetails;
        TextBox textBoxDetailsUrl;
        TextBox textBoxState;
        TextBox textBoxStateUrl;
        ComboBox comboBoxType;
        ComboBox comboBoxDisplay;
        NumericUpDown numericUpDownPartySize;
        NumericUpDown numericUpDownPartyMax;
        ComboBox comboBoxTimestamp;
        DateTimePicker dateTimePickerTimestampStart;
        DateTimePicker dateTimePickerTimestampEnd;
        CheckBox checkBoxTimestampEnd;
        TextBox textBoxLargeKey;
        TextBox textBoxLargeText;
        TextBox textBoxLargeUrl;
        TextBox textBoxSmallKey;
        TextBox textBoxSmallText;
        TextBox textBoxSmallUrl;
        TextBox textBoxButton1Text;
        TextBox textBoxButton1Url;
        TextBox textBoxButton2Text;
        TextBox textBoxButton2Url;
        CheckBox checkBoxProcessTrigger;
        CheckBox checkBoxIdleTrigger;
        CheckBox checkBoxFallback;
        NumericUpDown numericUpDownPriority;
        TextBox textBoxProcessName;
        ListBox listBoxProcesses;
        Label processHint;
        Button buttonAddProcess;
        Button buttonRemoveProcess;
        Button buttonRemove;
        Button buttonDuplicate;
        Button buttonMoveUp;
        Button buttonMoveDown;
        Button buttonApply;
        Button buttonCancel;
        ContextMenuStrip placeholderMenu;

        int selectedIndex = -1;
        bool loadingEditor;

        public bool DynamicEnabled => checkBoxEnabled.Checked;
        public int DynamicInterval => (int)numericUpDownInterval.Value;
        public string DynamicMode => comboBoxMode.SelectedItem?.ToString() ?? "Sequential";
        public List<DynamicStatus> DynamicStatuses { get; private set; }

        static readonly string[] HardwarePlaceholders =
        {
            "{cpu_usage}",
            "{cpu_temp}",
            "{gpu_usage}",
            "{gpu_temp}",
            "{ram_used}",
            "{ram_total}",
            "{ram_usage}",
            "{vram_used}",
            "{vram_total}",
            "{vram_usage}"
        };

        static readonly string[] ActivityTypeNames =
        {
            "Playing",
            "Listening",
            "Watching",
            "Competing"
        };

                static readonly ActivityType[] ActivityTypeValues =
                {
            ActivityType.Playing,
            ActivityType.Listening,
            ActivityType.Watching,
            ActivityType.Competing
        };

        static readonly string[] DisplayTypes =
        {
            "Name",
            "Details",
            "State"
        };

        static readonly string[] TimestampTypes =
        {
            "Since last connection",
            "Since startup",
            "Local time",
            "Custom",
            "Since presence update"
        };

        public DynamicPresenceForm(
            bool dynamicEnabled,
            int dynamicInterval,
            string dynamicMode,
            IEnumerable<DynamicStatus> initialStatuses
        )
        {
            CurrentColors.Update();

            Text = "Dynamic Presence";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(900, 650);
            Size = new Size(980, 720);
            ShowInTaskbar = false;
            MaximizeBox = true;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;

            statuses = new List<DynamicStatus>();
            foreach (DynamicStatus status in initialStatuses ?? Enumerable.Empty<DynamicStatus>())
                statuses.Add(CloneStatus(status));

            checkBoxEnabled = new CheckBox
            {
                Text = "Enable dynamic updates",
                AutoSize = true,
                Checked = dynamicEnabled,
                Margin = new Padding(0, 5, 16, 0)
            };

            Label labelInterval = CreateTopLabel("Interval:");
            numericUpDownInterval = new NumericUpDown
            {
                Minimum = 3,
                Maximum = 60,
                Value = Math.Min(60, Math.Max(3, dynamicInterval)),
                Width = 60,
                Margin = new Padding(0, 2, 16, 0)
            };

            Label labelMode = CreateTopLabel("Mode:");
            comboBoxMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 135,
                Margin = new Padding(0, 2, 0, 0)
            };
            comboBoxMode.Items.AddRange(new object[] { "Sequential", "Random", "Process Priority" });
            comboBoxMode.SelectedItem = string.Equals(dynamicMode, "Random", StringComparison.OrdinalIgnoreCase)
                ? "Random"
                : string.Equals(dynamicMode, "Process Priority", StringComparison.OrdinalIgnoreCase)
                    ? "Process Priority"
                    : "Sequential";
            comboBoxMode.SelectedIndexChanged += (s, e) => UpdateTriggerControls();

            FlowLayoutPanel topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false
            };
            topBar.Controls.Add(checkBoxEnabled);
            topBar.Controls.Add(labelInterval);
            topBar.Controls.Add(numericUpDownInterval);
            topBar.Controls.Add(labelMode);
            topBar.Controls.Add(comboBoxMode);

            listBoxStatuses = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle,
                FormattingEnabled = true
            };
            listBoxStatuses.SelectedIndexChanged += StatusSelectionChanged;

            Button buttonAdd = CreateButton("Add Preset");
            buttonDuplicate = CreateButton("Duplicate Preset");
            buttonRemove = CreateButton("Remove");
            buttonMoveUp = CreateButton("Move Up");
            buttonMoveDown = CreateButton("Move Down");
            buttonAdd.Click += AddStatus;
            buttonDuplicate.Click += DuplicateStatus;
            buttonRemove.Click += RemoveStatus;
            buttonMoveUp.Click += MoveStatusUp;
            buttonMoveDown.Click += MoveStatusDown;

            FlowLayoutPanel statusButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                Padding = new Padding(0, 6, 0, 0)
            };
            statusButtons.Controls.Add(buttonAdd);
            statusButtons.Controls.Add(buttonDuplicate);
            statusButtons.Controls.Add(buttonRemove);
            statusButtons.Controls.Add(buttonMoveUp);
            statusButtons.Controls.Add(buttonMoveDown);

            TableLayoutPanel leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            leftLayout.Controls.Add(listBoxStatuses, 0, 0);
            leftLayout.Controls.Add(statusButtons, 0, 1);

            GroupBox statusListGroup = new GroupBox
            {
                Text = "Presets",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 22, 8, 8)
            };
            statusListGroup.Controls.Add(leftLayout);

            textBoxName = CreateTextBox(128);
            textBoxDetails = CreateTextBox(128);
            textBoxDetailsUrl = CreateTextBox(512);
            textBoxState = CreateTextBox(128);
            textBoxStateUrl = CreateTextBox(512);
            textBoxLargeKey = CreateTextBox(512);
            textBoxLargeText = CreateTextBox(128);
            textBoxLargeUrl = CreateTextBox(512);
            textBoxSmallKey = CreateTextBox(512);
            textBoxSmallText = CreateTextBox(128);
            textBoxSmallUrl = CreateTextBox(512);
            textBoxButton1Text = CreateTextBox(32);
            textBoxButton1Url = CreateTextBox(512);
            textBoxButton2Text = CreateTextBox(32);
            textBoxButton2Url = CreateTextBox(512);

            placeholderMenu = CreatePlaceholderMenu();

            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };
            tabs.TabPages.Add(CreatePresenceTab());
            tabs.TabPages.Add(CreateImagesTab());
            tabs.TabPages.Add(CreateButtonsTab());
            tabs.TabPages.Add(CreateTriggerTab());

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 245,
                BorderStyle = BorderStyle.None,
                IsSplitterFixed = false
            };
            split.Panel1.Padding = new Padding(0, 0, 8, 0);
            split.Panel2.Padding = new Padding(8, 0, 0, 0);
            split.Panel1.Controls.Add(statusListGroup);
            split.Panel2.Controls.Add(tabs);

            Label bottomHint = new Label
            {
                Text = "Sequential follows the preset list. Random selects another preset. Process Priority selects the matching process with the lowest priority number; a fallback preset is used when none match.",
                AutoSize = true,
                ForeColor = CurrentColors.TextInactive,
                MaximumSize = new Size(620, 40),
                Margin = new Padding(0, 6, 0, 0)
            };

            buttonApply = CreateButton("Apply & Update");
            buttonCancel = CreateButton("Cancel");
            buttonApply.Click += ApplyChanges;
            buttonCancel.DialogResult = DialogResult.Cancel;

            Panel bottomBar = new Panel
            {
                Dock = DockStyle.Fill
            };

            FlowLayoutPanel leftButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 205,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 6, 0, 0)
            };

            leftButtons.Controls.Add(buttonAddProcess);
            leftButtons.Controls.Add(buttonRemoveProcess);

            FlowLayoutPanel actionButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 190,
                WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 6, 0, 0)
            };

            actionButtons.Controls.Add(buttonApply);
            actionButtons.Controls.Add(buttonCancel);

            bottomBar.Controls.Add(leftButtons);
            bottomBar.Controls.Add(actionButtons);
            bottomBar.Controls.Add(bottomHint);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.Controls.Add(topBar, 0, 0);
            root.Controls.Add(split, 0, 1);
            root.Controls.Add(bottomBar, 0, 2);

            Controls.Add(root);
            AcceptButton = buttonApply;
            CancelButton = buttonCancel;

            ApplyTheme();
            PopulateStatusList();
            UpdateTriggerControls();
        }

        static Label CreateTopLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, 7, 5, 0)
            };
        }

        static Button CreateButton(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                UseVisualStyleBackColor = true,
                Margin = new Padding(0, 0, 6, 0)
            };
        }

        static TextBox CreateTextBox(int maxLength)
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = maxLength,
                Margin = new Padding(0, 2, 4, 2)
            };
        }

        TabPage CreatePresenceTab()
        {
            comboBoxType = CreateComboBox(ActivityTypeNames);
            comboBoxDisplay = CreateComboBox(DisplayTypes);
            numericUpDownPartySize = CreateNumeric(0, 999, 0, 60);
            numericUpDownPartyMax = CreateNumeric(0, 999, 0, 60);
            comboBoxTimestamp = CreateComboBox(TimestampTypes);
            dateTimePickerTimestampStart = CreateDateTimePicker();
            dateTimePickerTimestampEnd = CreateDateTimePicker();
            checkBoxTimestampEnd = new CheckBox
            {
                Text = "Use end timestamp",
                AutoSize = true,
                Margin = new Padding(0, 7, 0, 0)
            };
            checkBoxTimestampEnd.CheckedChanged += (s, e) => UpdateTimestampControls();

            TableLayoutPanel table = CreateFieldTable(2);
            AddTextField(table, 0, "Name:", textBoxName);
            AddTextField(table, 1, "Details:", textBoxDetails);
            AddTextField(table, 2, "Details URL:", textBoxDetailsUrl);
            AddTextField(table, 3, "State:", textBoxState);
            AddTextField(table, 4, "State URL:", textBoxStateUrl);
            AddLabeledControl(table, 5, "Type:", comboBoxType);
            AddLabeledControl(table, 6, "Display:", comboBoxDisplay);
            AddLabeledControl(table, 7, "Party size:", numericUpDownPartySize);
            AddLabeledControl(table, 8, "Party max:", numericUpDownPartyMax);
            AddLabeledControl(table, 9, "Timestamp:", comboBoxTimestamp);
            AddLabeledControl(table, 10, "Start:", dateTimePickerTimestampStart);
            AddLabeledControl(table, 11, "End:", dateTimePickerTimestampEnd);
            AddLabeledControl(table, 12, "", checkBoxTimestampEnd);

            Label note = CreateHint("Application ID is shared with the main CustomRP connection and is intentionally not duplicated per preset.");
            table.Controls.Add(note, 0, 13);
            table.SetColumnSpan(note, 2);

            comboBoxTimestamp.SelectedIndexChanged += (s, e) => UpdateTimestampControls();

            AddPlaceholderHandler(textBoxName);
            AddPlaceholderHandler(textBoxDetails);
            AddPlaceholderHandler(textBoxDetailsUrl);
            AddPlaceholderHandler(textBoxState);
            AddPlaceholderHandler(textBoxStateUrl);

            return WrapTab("Presence", table);
        }

        TabPage CreateImagesTab()
        {
            TableLayoutPanel table = CreateFieldTable(2);
            AddTextField(table, 0, "Large key:", textBoxLargeKey);
            AddTextField(table, 1, "Large text:", textBoxLargeText);
            AddTextField(table, 2, "Large URL:", textBoxLargeUrl);
            AddTextField(table, 3, "Small key:", textBoxSmallKey);
            AddTextField(table, 4, "Small text:", textBoxSmallText);
            AddTextField(table, 5, "Small URL:", textBoxSmallUrl);

            AddPlaceholderHandler(textBoxLargeKey);
            AddPlaceholderHandler(textBoxLargeText);
            AddPlaceholderHandler(textBoxLargeUrl);
            AddPlaceholderHandler(textBoxSmallKey);
            AddPlaceholderHandler(textBoxSmallText);
            AddPlaceholderHandler(textBoxSmallUrl);

            return WrapTab("Images", table);
        }

        TabPage CreateButtonsTab()
        {
            TableLayoutPanel table = CreateFieldTable(2);
            AddTextField(table, 0, "Button 1 text:", textBoxButton1Text);
            AddTextField(table, 1, "Button 1 URL:", textBoxButton1Url);
            AddTextField(table, 2, "Button 2 text:", textBoxButton2Text);
            AddTextField(table, 3, "Button 2 URL:", textBoxButton2Url);
            AddPlaceholderHandler(textBoxButton1Text);
            AddPlaceholderHandler(textBoxButton1Url);
            AddPlaceholderHandler(textBoxButton2Text);
            AddPlaceholderHandler(textBoxButton2Url);

            return WrapTab("Buttons", table);
        }

        TabPage CreateTriggerTab()
        {
            checkBoxProcessTrigger = new CheckBox
            {
                Text = "Use process detection for this preset",
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 10)
            };
            checkBoxProcessTrigger.CheckedChanged += (s, e) => UpdateTriggerControls();

            checkBoxIdleTrigger = new CheckBox
            {
                Text = "Use this preset when I am idle",
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 10)
            };

            checkBoxFallback = new CheckBox
            {
                Text = "Use as fallback when no process matches",
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 10)
            };

            Label labelPriority = new Label
            {
                Text = "Priority:",
                AutoSize = true,
                Margin = new Padding(0, 7, 5, 0)
            };
            numericUpDownPriority = CreateNumeric(1, 9999, 100, 65);

            textBoxProcessName = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 128,
                Margin = new Padding(0, 2, 6, 2)
            };

            textBoxProcessName.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    AddProcess(s, e);
                }
            };

            buttonAddProcess = CreateButton("Add Process");
            buttonAddProcess.AutoSize = false;
            buttonAddProcess.Width = 95;
            buttonAddProcess.Height = 28;
            buttonAddProcess.Click += AddProcess;

            buttonRemoveProcess = CreateButton("Remove");
            buttonRemoveProcess.Click += RemoveProcess;

            FlowLayoutPanel priorityPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                WrapContents = false
            };
            priorityPanel.Controls.Add(labelPriority);
            priorityPanel.Controls.Add(numericUpDownPriority);
            priorityPanel.Controls.Add(checkBoxFallback);

            TableLayoutPanel processInput = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                ColumnCount = 1,
                RowCount = 1
            };

            processInput.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100)
            );

            processInput.Controls.Add(textBoxProcessName, 0, 0);

            listBoxProcesses = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            listBoxProcesses.SelectedIndexChanged += (s, e) => buttonRemoveProcess.Enabled = listBoxProcesses.SelectedIndex >= 0;

            processHint = CreateHint("Enter an executable name such as brave.exe or Photoshop.exe. Matching is case-insensitive and .exe is optional.");

            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(12)
            };

            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            table.Controls.Add(checkBoxProcessTrigger, 0, 0);
            table.Controls.Add(checkBoxIdleTrigger, 0, 1);
            table.Controls.Add(priorityPanel, 0, 2);
            table.Controls.Add(processInput, 0, 3);
            table.Controls.Add(listBoxProcesses, 0, 4);
            table.Controls.Add(processHint, 0, 5);

            return WrapTab("Process Trigger", table);
        }

        static TabPage WrapTab(string title, Control control)
        {
            TabPage page = new TabPage(title) { Padding = new Padding(6) };
            page.Controls.Add(control);
            return page;
        }

        static TableLayoutPanel CreateFieldTable(int columns)
        {
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = columns,
                Padding = new Padding(12)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 14; i++)
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            return table;
        }

        static void AddLabeledControl(TableLayoutPanel table, int row, string labelText, Control control)
        {
            Label label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = CurrentColors.TextColor,
                Margin = new Padding(0, 7, 6, 0)
            };
            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
        }

        void AddTextField(TableLayoutPanel table, int row, string labelText, TextBox textBox)
        {
            Label label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = CurrentColors.TextColor,
                Margin = new Padding(0, 7, 6, 0)
            };
            table.Controls.Add(label, 0, row);
            table.Controls.Add(CreateTextFieldWithMenu(textBox), 1, row);
        }

        Control CreateTextFieldWithMenu(TextBox textBox)
        {
            TableLayoutPanel line = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            Button menuButton = new Button
            {
                Text = "▼",
                Width = 28,
                Height = 27,
                Margin = new Padding(0, 2, 0, 2),
                TabStop = false
            };
            menuButton.Click += (s, e) => ShowPlaceholderMenu(menuButton, textBox);
            line.Controls.Add(textBox, 0, 0);
            line.Controls.Add(menuButton, 1, 0);
            return line;
        }

        void AddPlaceholderHandler(TextBox textBox)
        {
            // Placeholder insertion is provided by the field menu button.
        }

        ComboBox CreateComboBox(IEnumerable<string> items)
        {
            ComboBox comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 4, 2)
            };
            comboBox.Items.AddRange(items.Cast<object>().ToArray());
            comboBox.SelectedIndex = 0;
            return comboBox;
        }

        static NumericUpDown CreateNumeric(int minimum, int maximum, int value, int width)
        {
            return new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = Math.Min(maximum, Math.Max(minimum, value)),
                Width = width,
                Margin = new Padding(0, 2, 4, 2)
            };
        }

        static DateTimePicker CreateDateTimePicker()
        {
            return new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm:ss",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 4, 2)
            };
        }

        Label CreateHint(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                BackColor = CurrentColors.BgColor,
                ForeColor = CurrentColors.TextColor,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 0, 0)
            };
        }

        ContextMenuStrip CreatePlaceholderMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (string placeholder in HardwarePlaceholders)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(placeholder);
                item.Click += (s, e) =>
                {
                    if (menu.Tag is TextBox textBox)
                        InsertPlaceholder(textBox, placeholder);
                };
                menu.Items.Add(item);
            }
            return menu;
        }

        void ShowPlaceholderMenu(Button button, TextBox textBox)
        {
            placeholderMenu.Tag = textBox;
            placeholderMenu.Show(button, new Point(0, button.Height));
        }

        static void InsertPlaceholder(TextBox textBox, string placeholder)
        {
            string newText = textBox.Text.Insert(textBox.SelectionStart, placeholder);
            if (newText.Length > textBox.MaxLength)
                return;
            textBox.Text = newText;
            textBox.SelectionStart = Math.Min(textBox.SelectionStart + placeholder.Length, textBox.TextLength);
            textBox.SelectionLength = 0;
            textBox.Focus();
        }

        void PopulateStatusList()
        {
            SaveCurrentEditor();
            RefreshStatusList();
        }

        void RefreshStatusList()
        {
            loadingEditor = true;
            try
            {
                listBoxStatuses.Items.Clear();
                for (int i = 0; i < statuses.Count; i++)
                {
                    listBoxStatuses.Items.Add(FormatStatusListTitle(statuses[i], i));
                }

                if (statuses.Count > 0)
                {
                    selectedIndex = Math.Min(Math.Max(selectedIndex, 0), statuses.Count - 1);
                    listBoxStatuses.SelectedIndex = selectedIndex;
                }
                else
                {
                    selectedIndex = -1;
                }
            }
            finally
            {
                loadingEditor = false;
            }

            LoadCurrentEditor();
            UpdateButtons();
        }

        static string FormatStatusListTitle(DynamicStatus status, int index)
        {
            string name = status?.Name?.Trim() ?? "";
            return string.IsNullOrWhiteSpace(name)
                ? $"Preset {index + 1}"
                : $"Preset {index + 1} - {name}";
        }

        void StatusSelectionChanged(object sender, EventArgs e)
        {
            if (loadingEditor)
                return;
            SaveCurrentEditor();
            selectedIndex = listBoxStatuses.SelectedIndex;
            LoadCurrentEditor();
            UpdateButtons();
        }

        void LoadCurrentEditor()
        {
            loadingEditor = true;
            DynamicStatus status = selectedIndex >= 0 && selectedIndex < statuses.Count
                ? statuses[selectedIndex]
                : new DynamicStatus();

            textBoxName.Text = status.Name ?? "";
            textBoxDetails.Text = status.Details ?? "";
            textBoxDetailsUrl.Text = status.DetailsURL ?? "";
            textBoxState.Text = status.State ?? "";
            textBoxStateUrl.Text = status.StateURL ?? "";
            int typeIndex = Array.IndexOf(
                ActivityTypeValues,
                (ActivityType)status.Type
            );

            comboBoxType.SelectedIndex =
                typeIndex >= 0 ? typeIndex : 0;

            comboBoxDisplay.SelectedIndex = Math.Min(DisplayTypes.Length - 1, Math.Max(0, status.Display));
            numericUpDownPartySize.Value = Math.Min(numericUpDownPartySize.Maximum, Math.Max(numericUpDownPartySize.Minimum, status.PartySize));
            numericUpDownPartyMax.Value = Math.Min(numericUpDownPartyMax.Maximum, Math.Max(numericUpDownPartyMax.Minimum, status.PartyMax));
            comboBoxTimestamp.SelectedIndex = TimestampIndexFromEnum(status.Timestamps);
            dateTimePickerTimestampStart.Value = ClampDate(status.CustomTimestamp);
            dateTimePickerTimestampEnd.Value = ClampDate(status.CustomTimestampEnd);
            checkBoxTimestampEnd.Checked = status.CustomTimestampEndEnabled;

            textBoxLargeKey.Text = status.LargeKey ?? "";
            textBoxLargeText.Text = status.LargeText ?? "";
            textBoxLargeUrl.Text = status.LargeURL ?? "";
            textBoxSmallKey.Text = status.SmallKey ?? "";
            textBoxSmallText.Text = status.SmallText ?? "";
            textBoxSmallUrl.Text = status.SmallURL ?? "";
            textBoxButton1Text.Text = status.Button1Text ?? "";
            textBoxButton1Url.Text = status.Button1URL ?? "";
            textBoxButton2Text.Text = status.Button2Text ?? "";
            textBoxButton2Url.Text = status.Button2URL ?? "";

            checkBoxProcessTrigger.Checked = status.ProcessTriggerEnabled;
            checkBoxIdleTrigger.Checked = status.IdleTriggerEnabled;
            checkBoxFallback.Checked = status.Fallback;
            numericUpDownPriority.Value = Math.Min(numericUpDownPriority.Maximum, Math.Max(numericUpDownPriority.Minimum, status.Priority <= 0 ? 100 : status.Priority));
            listBoxProcesses.Items.Clear();
            foreach (string process in status.Processes ?? new List<string>())
                listBoxProcesses.Items.Add(process);
            textBoxProcessName.Text = "";
            buttonRemoveProcess.Enabled = false;

            loadingEditor = false;
            UpdateTimestampControls();
            UpdateTriggerControls();
        }

        void SaveCurrentEditor()
        {
            if (loadingEditor || selectedIndex < 0 || selectedIndex >= statuses.Count)
                return;

            DynamicStatus status = statuses[selectedIndex];
            status.Name = textBoxName.Text ?? "";
            status.Details = textBoxDetails.Text ?? "";
            status.DetailsURL = textBoxDetailsUrl.Text ?? "";
            status.State = textBoxState.Text ?? "";
            status.StateURL = textBoxStateUrl.Text ?? "";
            status.Type =
                comboBoxType.SelectedIndex >= 0 &&
                comboBoxType.SelectedIndex < ActivityTypeValues.Length
                    ? (int)ActivityTypeValues[comboBoxType.SelectedIndex]
                    : (int)ActivityType.Playing;
            status.Display = comboBoxDisplay.SelectedIndex;
            status.PartySize = (int)numericUpDownPartySize.Value;
            status.PartyMax = (int)numericUpDownPartyMax.Value;
            status.Timestamps = TimestampEnumFromIndex(comboBoxTimestamp.SelectedIndex);
            status.CustomTimestamp = dateTimePickerTimestampStart.Value;
            status.CustomTimestampEnd = dateTimePickerTimestampEnd.Value;
            status.CustomTimestampEndEnabled = checkBoxTimestampEnd.Checked;

            status.LargeKey = textBoxLargeKey.Text ?? "";
            status.LargeText = textBoxLargeText.Text ?? "";
            status.LargeURL = textBoxLargeUrl.Text ?? "";
            status.SmallKey = textBoxSmallKey.Text ?? "";
            status.SmallText = textBoxSmallText.Text ?? "";
            status.SmallURL = textBoxSmallUrl.Text ?? "";
            status.Button1Text = textBoxButton1Text.Text ?? "";
            status.Button1URL = textBoxButton1Url.Text ?? "";
            status.Button2Text = textBoxButton2Text.Text ?? "";
            status.Button2URL = textBoxButton2Url.Text ?? "";

            status.ProcessTriggerEnabled = checkBoxProcessTrigger.Checked;
            status.IdleTriggerEnabled = checkBoxIdleTrigger.Checked;
            status.Fallback = checkBoxFallback.Checked;
            status.Priority = (int)numericUpDownPriority.Value;
            status.Processes = listBoxProcesses.Items.Cast<string>()
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (listBoxStatuses != null &&
                selectedIndex >= 0 &&
                selectedIndex < listBoxStatuses.Items.Count &&
                !loadingEditor)
            {
                loadingEditor = true;
                try
                {
                    int currentSelection = listBoxStatuses.SelectedIndex;
                    listBoxStatuses.Items[selectedIndex] = FormatStatusListTitle(status, selectedIndex);
                    
                    if (listBoxStatuses.SelectedIndex != currentSelection)
                    {
                        listBoxStatuses.SelectedIndex = currentSelection;
                    }
                }
                finally
                {
                    loadingEditor = false;
                }
            }
        }

        void AddStatus(object sender, EventArgs e)
        {
            SaveCurrentEditor();

            DynamicStatus newStatus = new DynamicStatus
            {
                IdleTriggerEnabled = false,
                Priority = statuses.Count + 1
            };

            statuses.Add(newStatus);
            selectedIndex = statuses.Count - 1;
            RefreshStatusList();
        }

        void DuplicateStatus(object sender, EventArgs e)
        {
            SaveCurrentEditor();

            int index = listBoxStatuses.SelectedIndex;
            if (index < 0 || index >= statuses.Count)
                return;

            DynamicStatus duplicate = CloneStatus(statuses[index]);
            int insertIndex = index + 1;

            statuses.Insert(insertIndex, duplicate);
            selectedIndex = insertIndex;
            RefreshStatusList();
        }

        void RemoveStatus(object sender, EventArgs e)
        {
            SaveCurrentEditor();

            int index = listBoxStatuses.SelectedIndex;
            if (index < 0 || index >= statuses.Count)
                return;

            statuses.RemoveAt(index);
            selectedIndex = statuses.Count == 0
                ? -1
                : Math.Min(index, statuses.Count - 1);

            RefreshStatusList();
        }

        void MoveStatusUp(object sender, EventArgs e) => MoveStatus(-1);
        void MoveStatusDown(object sender, EventArgs e) => MoveStatus(1);

        void MoveStatus(int direction)
        {
            SaveCurrentEditor();
            int index = listBoxStatuses.SelectedIndex;
            int target = index + direction;
            if (index < 0 || target < 0 || target >= statuses.Count)
                return;
            DynamicStatus temp = statuses[index];
            statuses[index] = statuses[target];
            statuses[target] = temp;
            selectedIndex = target;
            RefreshStatusList();
        }

        void AddProcess(object sender, EventArgs e)
        {
            string value = NormalizeProcessName(textBoxProcessName.Text);
            if (string.IsNullOrEmpty(value))
                return;
            if (!listBoxProcesses.Items.Cast<string>().Any(p => string.Equals(p, value, StringComparison.OrdinalIgnoreCase)))
                listBoxProcesses.Items.Add(value);
            textBoxProcessName.Clear();
            textBoxProcessName.Focus();
            SaveCurrentEditor();
        }

        void RemoveProcess(object sender, EventArgs e)
        {
            if (listBoxProcesses.SelectedIndex >= 0)
                listBoxProcesses.Items.RemoveAt(listBoxProcesses.SelectedIndex);
            SaveCurrentEditor();
        }

        static string NormalizeProcessName(string value)
        {
            value = (value ?? "").Trim();
            if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 4);
            return value;
        }

        void UpdateButtons()
        {
            int index = listBoxStatuses.SelectedIndex;
            buttonDuplicate.Enabled = index >= 0 && index < statuses.Count;
            buttonRemove.Enabled = statuses.Count > 1 || index >= 0;
            buttonMoveUp.Enabled = index > 0;
            buttonMoveDown.Enabled = index >= 0 && index < statuses.Count - 1;
        }

        void UpdateTimestampControls()
        {
            bool custom = comboBoxTimestamp.SelectedIndex == 3;
            dateTimePickerTimestampStart.Enabled = custom;
            checkBoxTimestampEnd.Enabled = custom;
            dateTimePickerTimestampEnd.Enabled = custom && checkBoxTimestampEnd.Checked;
        }

        void UpdateTriggerControls()
        {
            if (checkBoxProcessTrigger == null)
                return;

            bool enabled = checkBoxProcessTrigger.Checked;

            // Process trigger settings can be configured at any time.
            // The selected Dynamic Presence mode determines whether the trigger is used.
            checkBoxProcessTrigger.Enabled = true;
            checkBoxFallback.Enabled = true;
            numericUpDownPriority.Enabled = true;
            textBoxProcessName.Enabled = enabled;
            listBoxProcesses.Enabled = enabled;
            buttonRemoveProcess.Enabled = enabled && listBoxProcesses.SelectedIndex >= 0;

            if (processHint != null)
            {
                processHint.Enabled = true;
                processHint.BackColor = CurrentColors.BgColor;
                processHint.ForeColor = CurrentColors.TextColor;
            }
        }

        void ApplyChanges(object sender, EventArgs e)
        {
            SaveCurrentEditor();

            if (string.Equals(DynamicMode, "Process Priority", StringComparison.OrdinalIgnoreCase))
            {
                foreach (DynamicStatus status in statuses)
                {
                    if (status.ProcessTriggerEnabled && status.Processes.Count == 0 && !status.Fallback)
                    {
                        MessageBox.Show(this, "A process-triggered preset must contain at least one process, or be marked as fallback.", "Dynamic Presence", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            DynamicStatuses = new List<DynamicStatus>();
            foreach (DynamicStatus status in statuses)
                DynamicStatuses.Add(CloneStatus(status));
            DialogResult = DialogResult.OK;
            Close();
        }

        static DynamicStatus CloneStatus(DynamicStatus status)
        {
            if (status == null)
                return new DynamicStatus();
            DynamicStatus clone = new DynamicStatus
            {
                Name = status.Name ?? "",
                Details = status.Details ?? "",
                DetailsURL = status.DetailsURL ?? "",
                State = status.State ?? "",
                StateURL = status.StateURL ?? "",
                Type = status.Type,
                Display = status.Display,
                PartySize = status.PartySize,
                PartyMax = status.PartyMax,
                Timestamps = status.Timestamps,
                CustomTimestamp = status.CustomTimestamp,
                CustomTimestampEndEnabled = status.CustomTimestampEndEnabled,
                CustomTimestampEnd = status.CustomTimestampEnd,
                LargeKey = status.LargeKey ?? "",
                LargeText = status.LargeText ?? "",
                LargeURL = status.LargeURL ?? "",
                SmallKey = status.SmallKey ?? "",
                SmallText = status.SmallText ?? "",
                SmallURL = status.SmallURL ?? "",
                Button1Text = status.Button1Text ?? "",
                Button1URL = status.Button1URL ?? "",
                Button2Text = status.Button2Text ?? "",
                Button2URL = status.Button2URL ?? "",
                ProcessTriggerEnabled = status.ProcessTriggerEnabled,
                IdleTriggerEnabled = status.IdleTriggerEnabled,
                Fallback = status.Fallback,
                Priority = status.Priority <= 0 ? 100 : status.Priority,
                Processes = status.Processes == null ? new List<string>() : new List<string>(status.Processes)
            };
            return clone;
        }

        static int TimestampEnumFromIndex(int index)
        {
            switch (index)
            {
                case 1: return (int)TimestampType.SinceStartup;
                case 2: return (int)TimestampType.LocalTime;
                case 3: return (int)TimestampType.Custom;
                case 4: return (int)TimestampType.SincePresenceUpdate;
                default: return (int)TimestampType.SinceLastConnection;
            }
        }

        static int TimestampIndexFromEnum(int value)
        {
            switch ((TimestampType)value)
            {
                case TimestampType.SinceStartup: return 1;
                case TimestampType.LocalTime: return 2;
                case TimestampType.Custom: return 3;
                case TimestampType.SincePresenceUpdate: return 4;
                default: return 0;
            }
        }

        static DateTime ClampDate(DateTime value)
        {
            DateTime min = DateTimePicker.MinimumDateTime;
            DateTime max = DateTimePicker.MaximumDateTime;
            return value < min || value > max ? DateTime.Now : value;
        }

        void ApplyTheme()
        {
            BackColor = CurrentColors.BgColor;
            ForeColor = CurrentColors.TextColor;

            foreach (Control control in GetAllControls(this))
            {
                if (control is TextBox ||
                    control is ComboBox ||
                    control is NumericUpDown ||
                    control is DateTimePicker ||
                    control is ListBox)
                {
                    control.BackColor = CurrentColors.BgTextFields;
                    control.ForeColor = CurrentColors.TextColor;
                }
                else if (control is Button)
                {
                    control.ForeColor = CurrentColors.TextColor;
                    control.BackColor = CurrentColors.BgColor;
                }
                else
                {
                    control.BackColor = CurrentColors.BgColor;
                    control.ForeColor = CurrentColors.TextColor;
                }
            }

            if (processHint != null)
            {
                processHint.Enabled = true;
                processHint.BackColor = CurrentColors.BgColor;
                processHint.ForeColor = CurrentColors.TextColor;
            }

            if (Properties.Settings.Default.darkMode)
            {
                foreach (Control control in GetAllControls(this))
                {
                    if (control is Button button)
                        button.FlatStyle = FlatStyle.Flat;
                }
            }
        }

        static IEnumerable<Control> GetAllControls(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                yield return control;
                foreach (Control child in GetAllControls(control))
                    yield return child;
            }
        }
    }
}

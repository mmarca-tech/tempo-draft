namespace TempoDraft.Desktop;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private TabControl settingsTabs;
    private TabPage appSettingsTab;
    private TabPage mappingsTab;
    private TabPage runTab;
    private TableLayoutPanel rootLayout;
    private FlowLayoutPanel controlsLayout;
    private DateTimePicker datePicker;
    private CheckBox dryRunCheckBox;
    private CheckBox forceUploadCheckBox;
    private Button processButton;
    private Button uploadButton;
    private Button appSettingsSaveButton;
    private Button mappingsSaveButton;
    private DataGridView outputGrid;
    private TextBox logTextBox;
    private Label dateLabel;

    private TableLayoutPanel appSettingsLayout;
    private TextBox timeZoneIdTextBox;
    private TextBox workdayStartTextBox;
    private TextBox workdayEndTextBox;
    private NumericUpDown roundingMinutesUpDown;
    private NumericUpDown minBlockMinutesUpDown;
    private NumericUpDown mergeGapMinutesUpDown;
    private NumericUpDown defaultCodingMinutesUpDown;
    private NumericUpDown commitClusterGapUpDown;
    private TextBox outputFolderTextBox;
    private TextBox gitRepoBasePathTextBox;
    private TextBox gitRepoPathsTextBox;
    private TextBox ignoreSubjectsTextBox;
    private TextBox uploadTimeZoneTextBox;
    private NumericUpDown meetingRoundUpMinutesUpDown;
    private TextBox jiraBaseUrlTextBox;
    private TextBox jiraEmailTextBox;
    private TextBox jiraApiTokenTextBox;

    private DataGridView keywordGrid;
    private DataGridView subjectGrid;
    private DataGridView repoDefaultGrid;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        settingsTabs = new TabControl();
        appSettingsTab = new TabPage();
        mappingsTab = new TabPage();
        runTab = new TabPage();
        rootLayout = new TableLayoutPanel();
        controlsLayout = new FlowLayoutPanel();
        datePicker = new DateTimePicker();
        dryRunCheckBox = new CheckBox();
        forceUploadCheckBox = new CheckBox();
        processButton = new Button();
        uploadButton = new Button();
        appSettingsSaveButton = new Button();
        mappingsSaveButton = new Button();
        outputGrid = new DataGridView();
        logTextBox = new TextBox();
        dateLabel = new Label();

        appSettingsLayout = new TableLayoutPanel();
        timeZoneIdTextBox = new TextBox();
        workdayStartTextBox = new TextBox();
        workdayEndTextBox = new TextBox();
        roundingMinutesUpDown = new NumericUpDown();
        minBlockMinutesUpDown = new NumericUpDown();
        mergeGapMinutesUpDown = new NumericUpDown();
        defaultCodingMinutesUpDown = new NumericUpDown();
        commitClusterGapUpDown = new NumericUpDown();
        outputFolderTextBox = new TextBox();
        gitRepoBasePathTextBox = new TextBox();
        gitRepoPathsTextBox = new TextBox();
        ignoreSubjectsTextBox = new TextBox();
        uploadTimeZoneTextBox = new TextBox();
        meetingRoundUpMinutesUpDown = new NumericUpDown();
        jiraBaseUrlTextBox = new TextBox();
        jiraEmailTextBox = new TextBox();
        jiraApiTokenTextBox = new TextBox();

        keywordGrid = new DataGridView();
        subjectGrid = new DataGridView();
        repoDefaultGrid = new DataGridView();

        SuspendLayout();

        rootLayout.ColumnCount = 1;
        rootLayout.RowCount = 1;
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.Padding = new Padding(12);

        settingsTabs.Dock = DockStyle.Fill;
        settingsTabs.TabPages.Add(appSettingsTab);
        settingsTabs.TabPages.Add(mappingsTab);
        settingsTabs.TabPages.Add(runTab);
        settingsTabs.SelectedTab = runTab;

        appSettingsTab.Text = "App Settings";
        appSettingsTab.Controls.Add(appSettingsLayout);

        mappingsTab.Text = "Mappings";
        var mappingsLayout = new TableLayoutPanel();
        mappingsLayout.Dock = DockStyle.Fill;
        mappingsLayout.RowCount = 3;
        mappingsLayout.ColumnCount = 1;
        mappingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        mappingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        mappingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));
        mappingsLayout.Padding = new Padding(8);

        var keywordGroup = new GroupBox { Text = "KeywordToIssueKey", Dock = DockStyle.Fill };
        var subjectGroup = new GroupBox { Text = "SubjectContainsToIssueKey", Dock = DockStyle.Fill };
        var repoGroup = new GroupBox { Text = "DefaultIssueKeyByRepo", Dock = DockStyle.Fill };

        SetupGrid(keywordGrid);
        SetupGrid(subjectGrid);
        SetupGrid(repoDefaultGrid);

        keywordGroup.Controls.Add(keywordGrid);
        subjectGroup.Controls.Add(subjectGrid);
        repoGroup.Controls.Add(repoDefaultGrid);

        mappingsLayout.Controls.Add(keywordGroup, 0, 0);
        mappingsLayout.Controls.Add(subjectGroup, 0, 1);
        mappingsLayout.Controls.Add(repoGroup, 0, 2);
        mappingsSaveButton.Text = "Save Mappings";
        mappingsSaveButton.Size = new Size(140, 30);
        mappingsSaveButton.Margin = new Padding(8);
        mappingsSaveButton.Anchor = AnchorStyles.Right;
        mappingsSaveButton.Click += mappingsSaveButton_Click;
        mappingsSaveButton.Dock = DockStyle.Bottom;

        mappingsTab.Controls.Add(mappingsLayout);
        mappingsTab.Controls.Add(mappingsSaveButton);

        runTab.Text = "Run";
        var runLayout = new TableLayoutPanel();
        runLayout.Dock = DockStyle.Fill;
        runLayout.RowCount = 3;
        runLayout.ColumnCount = 1;
        runLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        runLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
        runLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
        runLayout.Controls.Add(controlsLayout, 0, 0);
        runLayout.Controls.Add(outputGrid, 0, 1);
        runLayout.Controls.Add(logTextBox, 0, 2);
        runTab.Controls.Add(runLayout);

        appSettingsLayout.Dock = DockStyle.Fill;
        appSettingsLayout.ColumnCount = 2;
        appSettingsLayout.RowCount = 18;
        appSettingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
        appSettingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        appSettingsLayout.Padding = new Padding(8);
        for (int i = 0; i < 15; i++)
        {
            appSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        }
        appSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        appSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        appSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

        AddLabel(appSettingsLayout, "TimeZoneId", 0);
        AddControl(appSettingsLayout, timeZoneIdTextBox, 0);
        AddLabel(appSettingsLayout, "WorkdayStart (HH:mm)", 1);
        AddControl(appSettingsLayout, workdayStartTextBox, 1);
        AddLabel(appSettingsLayout, "WorkdayEnd (HH:mm)", 2);
        AddControl(appSettingsLayout, workdayEndTextBox, 2);
        AddLabel(appSettingsLayout, "RoundingMinutes", 3);
        AddControl(appSettingsLayout, roundingMinutesUpDown, 3);
        AddLabel(appSettingsLayout, "MinBlockMinutes", 4);
        AddControl(appSettingsLayout, minBlockMinutesUpDown, 4);
        AddLabel(appSettingsLayout, "MergeGapMinutes", 5);
        AddControl(appSettingsLayout, mergeGapMinutesUpDown, 5);
        AddLabel(appSettingsLayout, "DefaultCodingBlockMinutes", 6);
        AddControl(appSettingsLayout, defaultCodingMinutesUpDown, 6);
        AddLabel(appSettingsLayout, "CommitClusterGapMinutes", 7);
        AddControl(appSettingsLayout, commitClusterGapUpDown, 7);
        AddLabel(appSettingsLayout, "OutputFolder", 8);
        AddControl(appSettingsLayout, outputFolderTextBox, 8);
        AddLabel(appSettingsLayout, "GitRepoBasePath", 9);
        AddControl(appSettingsLayout, gitRepoBasePathTextBox, 9);
        AddLabel(appSettingsLayout, "UploadTimeZoneId", 10);
        AddControl(appSettingsLayout, uploadTimeZoneTextBox, 10);
        AddLabel(appSettingsLayout, "MeetingRoundUpToMinutes", 11);
        AddControl(appSettingsLayout, meetingRoundUpMinutesUpDown, 11);
        AddLabel(appSettingsLayout, "Jira BaseUrl", 12);
        AddControl(appSettingsLayout, jiraBaseUrlTextBox, 12);
        AddLabel(appSettingsLayout, "Jira Email", 13);
        AddControl(appSettingsLayout, jiraEmailTextBox, 13);
        AddLabel(appSettingsLayout, "Jira ApiToken", 14);
        AddControl(appSettingsLayout, jiraApiTokenTextBox, 14);
        jiraApiTokenTextBox.UseSystemPasswordChar = true;

        AddLabel(appSettingsLayout, "GitRepoPaths (one per line)", 15);
        gitRepoPathsTextBox.Multiline = true;
        gitRepoPathsTextBox.ScrollBars = ScrollBars.Both;
        gitRepoPathsTextBox.Dock = DockStyle.Fill;
        gitRepoPathsTextBox.Margin = new Padding(4, 4, 4, 8);
        appSettingsLayout.Controls.Add(gitRepoPathsTextBox, 1, 15);

        AddLabel(appSettingsLayout, "IgnoreCalendarSubjects (one per line)", 16);
        ignoreSubjectsTextBox.Multiline = true;
        ignoreSubjectsTextBox.ScrollBars = ScrollBars.Both;
        ignoreSubjectsTextBox.Dock = DockStyle.Fill;
        ignoreSubjectsTextBox.Margin = new Padding(4, 4, 4, 4);
        appSettingsLayout.Controls.Add(ignoreSubjectsTextBox, 1, 16);

        appSettingsSaveButton.Text = "Save App Settings";
        appSettingsSaveButton.Size = new Size(160, 30);
        appSettingsSaveButton.Margin = new Padding(4, 6, 4, 6);
        appSettingsSaveButton.Anchor = AnchorStyles.Right;
        appSettingsSaveButton.Click += appSettingsSaveButton_Click;
        appSettingsLayout.Controls.Add(appSettingsSaveButton, 1, 17);

        controlsLayout.Dock = DockStyle.Fill;
        controlsLayout.Padding = new Padding(6, 6, 6, 6);
        controlsLayout.FlowDirection = FlowDirection.LeftToRight;
        controlsLayout.WrapContents = true;
        controlsLayout.AutoScroll = false;
        controlsLayout.AutoSize = true;
        controlsLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        dateLabel.Text = "Date";
        dateLabel.AutoSize = true;
        dateLabel.TextAlign = ContentAlignment.MiddleLeft;
        dateLabel.Margin = new Padding(6, 8, 6, 8);
        datePicker.Width = 220;
        datePicker.Margin = new Padding(6, 8, 18, 8);
        dryRunCheckBox.Text = "Dry Run";
        dryRunCheckBox.AutoSize = true;
        dryRunCheckBox.Margin = new Padding(6, 8, 22, 8);
        forceUploadCheckBox.Text = "Force Upload";
        forceUploadCheckBox.AutoSize = true;
        forceUploadCheckBox.Margin = new Padding(6, 8, 22, 8);
        processButton.Text = "Process";
        processButton.Size = new Size(90, 28);
        processButton.Margin = new Padding(6, 6, 6, 6);
        processButton.Click += processButton_Click;
        uploadButton.Text = "Upload";
        uploadButton.Size = new Size(90, 28);
        uploadButton.Margin = new Padding(6, 6, 6, 6);
        uploadButton.Click += uploadButton_Click;

        controlsLayout.Controls.Add(dateLabel);
        controlsLayout.Controls.Add(datePicker);
        // removed no-outlook/no-git controls
        controlsLayout.Controls.Add(dryRunCheckBox);
        controlsLayout.Controls.Add(forceUploadCheckBox);
        controlsLayout.Controls.Add(processButton);
        controlsLayout.Controls.Add(uploadButton);

        SetupOutputGrid(outputGrid);

        logTextBox.Multiline = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);

        rootLayout.Controls.Add(settingsTabs, 0, 0);

        ClientSize = new Size(920, 720);
        Controls.Add(rootLayout);
        Text = "TempoDraft";
        ResumeLayout(false);
    }

    private static void AddLabel(TableLayoutPanel panel, string text, int row)
    {
        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, row);
    }

    private static void AddControl(TableLayoutPanel panel, Control control, int row)
    {
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 1, row);
        if (control is NumericUpDown nud)
        {
            nud.Maximum = 10000;
            nud.Minimum = 0;
            nud.Increment = 5;
        }
    }

    private static void SetupGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = true;
        grid.AllowUserToDeleteRows = true;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Key" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Issue Key" });
    }

    private static void SetupOutputGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;

        grid.Columns.Clear();
        grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Upload", ReadOnly = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Issue", ReadOnly = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Minutes", ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Description", ReadOnly = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Start", ReadOnly = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "End", ReadOnly = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Evidence", ReadOnly = true });
    }
}

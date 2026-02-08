using System.Windows.Forms;

internal sealed class MainForm : Form
{
	private const int DwmwaUseImmersiveDarkMode = 20;
	private const int DwmwaUseImmersiveDarkModeLegacy = 19;
	private const uint SwpNosize = 0x0001;
	private const uint SwpNomove = 0x0002;
	private const uint SwpNozorder = 0x0004;
	private const uint SwpNoownerzorder = 0x0200;
	private const uint SwpNoactivate = 0x0010;
	private const uint SwpFramechanged = 0x0020;

	private static readonly Color WindowBackColor = Color.FromArgb( 27, 30, 34 );
	private static readonly Color PanelBackColor = Color.FromArgb( 33, 37, 42 );
	private static readonly Color InputBackColor = Color.FromArgb( 24, 27, 31 );
	private static readonly Color LogBackColor = Color.FromArgb( 20, 23, 27 );
	private static readonly Color ButtonBackColor = Color.FromArgb( 45, 50, 56 );
	private static readonly Color ButtonHoverBackColor = Color.FromArgb( 58, 64, 71 );
	private static readonly Color ButtonDownBackColor = Color.FromArgb( 66, 73, 81 );
	private static readonly Color BorderColor = Color.FromArgb( 72, 79, 88 );
	private static readonly Color TextColor = Color.FromArgb( 233, 236, 240 );
	private static readonly Color MutedTextColor = Color.FromArgb( 166, 174, 186 );
	private static readonly Color LightWindowBackColor = Color.FromArgb( 245, 247, 250 );
	private static readonly Color LightPanelBackColor = Color.FromArgb( 251, 252, 254 );
	private static readonly Color LightInputBackColor = Color.White;
	private static readonly Color LightLogBackColor = Color.White;
	private static readonly Color LightButtonBackColor = Color.FromArgb( 233, 236, 240 );
	private static readonly Color LightButtonHoverBackColor = Color.FromArgb( 223, 228, 234 );
	private static readonly Color LightButtonDownBackColor = Color.FromArgb( 213, 219, 226 );
	private static readonly Color LightBorderColor = Color.FromArgb( 180, 188, 200 );
	private static readonly Color LightTextColor = Color.FromArgb( 25, 28, 32 );
	private static readonly Color LightMutedTextColor = Color.FromArgb( 95, 105, 118 );

	private readonly TextBox _mdlPathTextBox = new();
	private readonly TextBox _batchRootTextBox = new();
	private readonly TextBox _gmodRootTextBox = new();
	private readonly TextBox _outputRootTextBox = new();
	private readonly TextBox _vmdlNameTextBox = new();
	private readonly ComboBox _profileComboBox = new();
	private readonly CheckBox _profileOverrideCheckBox = new();
	private readonly CheckBox _batchModeCheckBox = new();
	private readonly CheckBox _recursiveBatchCheckBox = new();
	private readonly NumericUpDown _threadsUpDown = new();
	private readonly CheckBox _preservePathCheckBox = new();
	private readonly CheckBox _materialsCheckBox = new();
	private readonly CheckBox _animationsCheckBox = new();
	private readonly CheckBox _copyShadersCheckBox = new();
	private readonly ComboBox _roughSourceComboBox = new();
	private readonly ComboBox _roughChannelComboBox = new();
	private readonly CheckBox _roughInvertCheckBox = new();
	private readonly ComboBox _metalSourceComboBox = new();
	private readonly ComboBox _metalChannelComboBox = new();
	private readonly CheckBox _metalInvertCheckBox = new();
	private readonly CheckBox _overrideLevelsCheckBox = new();
	private readonly NumericUpDown _levelsInMinUpDown = new();
	private readonly NumericUpDown _levelsInMaxUpDown = new();
	private readonly NumericUpDown _levelsGammaUpDown = new();
	private readonly NumericUpDown _levelsOutMinUpDown = new();
	private readonly NumericUpDown _levelsOutMaxUpDown = new();
	private readonly Label _roughSourcePreviewLabel = new();
	private readonly Label _metalSourcePreviewLabel = new();
	private readonly CheckBox _verboseCheckBox = new();
	private readonly Button _convertButton = new();
	private readonly Button _themeToggleButton = new();
	private readonly RichTextBox _logTextBox = new();
	private bool _isDarkTheme = true;

	public MainForm()
	{
		Text = "MDL to VMDL Converter";
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size( 980, 720 );
		Width = 1080;
		Height = 760;

		BuildUi();
		Shown += (_, _) => ApplyNativeTheme();
	}

	private void BuildUi()
	{
		var root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 3,
			Padding = new Padding( 12 )
		};
		root.RowStyles.Add( new RowStyle( SizeType.AutoSize ) );
		root.RowStyles.Add( new RowStyle( SizeType.AutoSize ) );
		root.RowStyles.Add( new RowStyle( SizeType.Percent, 100f ) );
		Controls.Add( root );

		var pathsGroup = new GroupBox
		{
			Text = "Paths",
			Dock = DockStyle.Top,
			AutoSize = true,
			Padding = new Padding( 10 )
		};

		var inputPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			ColumnCount = 3,
			AutoSize = true
		};
		inputPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 170f ) );
		inputPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 100f ) );
		inputPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 120f ) );

		AddPathRow( inputPanel, 0, "Source MDL", _mdlPathTextBox, OnBrowseMdl );
		AddPathRow( inputPanel, 1, "Batch Root", _batchRootTextBox, OnBrowseBatchRoot );
		AddPathRow( inputPanel, 2, "GMod Root", _gmodRootTextBox, OnBrowseGmodRoot );
		AddPathRow( inputPanel, 3, "Output Root", _outputRootTextBox, OnBrowseOutputRoot );
		AddTextRow( inputPanel, 4, "VMDL Name (optional)", _vmdlNameTextBox );

		pathsGroup.Controls.Add( inputPanel );
		root.Controls.Add( pathsGroup, 0, 0 );

		var settingsPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			ColumnCount = 2,
			AutoSize = true,
			Padding = new Padding( 0, 10, 0, 10 )
		};
		settingsPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 62f ) );
		settingsPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 38f ) );

		var materialGroup = new GroupBox
		{
			Text = "Material Overrides",
			Dock = DockStyle.Fill,
			AutoSize = true,
			Padding = new Padding( 10 )
		};

		var materialPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			ColumnCount = 5,
			AutoSize = true
		};
		materialPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 130f ) );
		materialPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 180f ) );
		materialPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 80f ) );
		materialPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 120f ) );
		materialPanel.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 140f ) );

		materialPanel.Controls.Add( new Label
		{
			Text = "Material Profile",
			AutoSize = true,
			Margin = new Padding( 0, 8, 8, 0 )
		}, 0, 0 );

		_profileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		_profileComboBox.Width = 180;
		_profileComboBox.Items.AddRange( Enum.GetNames<MaterialProfileOverride>() );
		_profileComboBox.SelectedItem = MaterialProfileOverride.Auto.ToString();
		_profileComboBox.Margin = new Padding( 0, 4, 0, 4 );
		materialPanel.Controls.Add( _profileComboBox, 1, 0 );
		materialPanel.SetColumnSpan( _profileComboBox, 2 );

		_profileOverrideCheckBox.Appearance = Appearance.Button;
		_profileOverrideCheckBox.Text = "Override Profile";
		_profileOverrideCheckBox.AutoSize = false;
		_profileOverrideCheckBox.Width = 130;
		_profileOverrideCheckBox.Height = 26;
		_profileOverrideCheckBox.TextAlign = ContentAlignment.MiddleCenter;
		_profileOverrideCheckBox.Checked = false;
		_profileOverrideCheckBox.Margin = new Padding( 0, 3, 0, 3 );
		materialPanel.Controls.Add( _profileOverrideCheckBox, 3, 0 );
		materialPanel.SetColumnSpan( _profileOverrideCheckBox, 2 );

		materialPanel.Controls.Add( new Label
		{
			Text = "Roughness Map",
			AutoSize = true,
			Margin = new Padding( 0, 8, 8, 0 )
		}, 0, 1 );
		_roughSourceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		_roughSourceComboBox.Width = 170;
		_roughSourceComboBox.Items.AddRange( Enum.GetNames<MaterialOverrideTextureSource>() );
		_roughSourceComboBox.SelectedItem = MaterialOverrideTextureSource.Auto.ToString();
		_roughSourceComboBox.Margin = new Padding( 0, 4, 0, 4 );
		materialPanel.Controls.Add( _roughSourceComboBox, 1, 1 );

		materialPanel.Controls.Add( new Label
		{
			Text = "Channel",
			AutoSize = true,
			Margin = new Padding( 8, 8, 8, 0 )
		}, 2, 1 );
		_roughChannelComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		_roughChannelComboBox.Width = 100;
		_roughChannelComboBox.Items.AddRange( Enum.GetNames<MaterialOverrideChannel>() );
		_roughChannelComboBox.SelectedItem = MaterialOverrideChannel.Alpha.ToString();
		_roughChannelComboBox.Margin = new Padding( 0, 4, 0, 4 );
		materialPanel.Controls.Add( _roughChannelComboBox, 3, 1 );

		_roughInvertCheckBox.Text = "Invert";
		_roughInvertCheckBox.AutoSize = true;
		_roughInvertCheckBox.Margin = new Padding( 8, 6, 0, 0 );
		materialPanel.Controls.Add( _roughInvertCheckBox, 4, 1 );

		_roughSourcePreviewLabel.AutoSize = true;
		_roughSourcePreviewLabel.ForeColor = Color.DimGray;
		_roughSourcePreviewLabel.Margin = new Padding( 0, 0, 0, 6 );
		materialPanel.Controls.Add( _roughSourcePreviewLabel, 1, 2 );
		materialPanel.SetColumnSpan( _roughSourcePreviewLabel, 4 );

		materialPanel.Controls.Add( new Label
		{
			Text = "Metalness Map",
			AutoSize = true,
			Margin = new Padding( 0, 8, 8, 0 )
		}, 0, 3 );
		_metalSourceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		_metalSourceComboBox.Width = 170;
		_metalSourceComboBox.Items.AddRange( Enum.GetNames<MaterialOverrideTextureSource>() );
		_metalSourceComboBox.SelectedItem = MaterialOverrideTextureSource.Auto.ToString();
		_metalSourceComboBox.Margin = new Padding( 0, 4, 0, 4 );
		materialPanel.Controls.Add( _metalSourceComboBox, 1, 3 );

		materialPanel.Controls.Add( new Label
		{
			Text = "Channel",
			AutoSize = true,
			Margin = new Padding( 8, 8, 8, 0 )
		}, 2, 3 );
		_metalChannelComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		_metalChannelComboBox.Width = 100;
		_metalChannelComboBox.Items.AddRange( Enum.GetNames<MaterialOverrideChannel>() );
		_metalChannelComboBox.SelectedItem = MaterialOverrideChannel.Alpha.ToString();
		_metalChannelComboBox.Margin = new Padding( 0, 4, 0, 4 );
		materialPanel.Controls.Add( _metalChannelComboBox, 3, 3 );

		_metalInvertCheckBox.Text = "Invert";
		_metalInvertCheckBox.AutoSize = true;
		_metalInvertCheckBox.Margin = new Padding( 8, 6, 0, 0 );
		materialPanel.Controls.Add( _metalInvertCheckBox, 4, 3 );

		_metalSourcePreviewLabel.AutoSize = true;
		_metalSourcePreviewLabel.ForeColor = Color.DimGray;
		_metalSourcePreviewLabel.Margin = new Padding( 0, 0, 0, 6 );
		materialPanel.Controls.Add( _metalSourcePreviewLabel, 1, 4 );
		materialPanel.SetColumnSpan( _metalSourcePreviewLabel, 4 );

		_overrideLevelsCheckBox.Text = "Override levels/curves";
		_overrideLevelsCheckBox.AutoSize = true;
		_overrideLevelsCheckBox.Checked = false;
		_overrideLevelsCheckBox.Margin = new Padding( 0, 8, 0, 2 );
		materialPanel.Controls.Add( _overrideLevelsCheckBox, 0, 5 );
		materialPanel.SetColumnSpan( _overrideLevelsCheckBox, 5 );

		var levelsPanel = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			AutoSize = true,
			Dock = DockStyle.Top,
			Margin = new Padding( 0, 0, 0, 0 )
		};

		levelsPanel.Controls.Add( new Label
		{
			Text = "In Min",
			AutoSize = true,
			Margin = new Padding( 0, 8, 6, 0 )
		} );
		ConfigureUnitIntervalUpDown( _levelsInMinUpDown, 0m );
		levelsPanel.Controls.Add( _levelsInMinUpDown );

		levelsPanel.Controls.Add( new Label
		{
			Text = "In Max",
			AutoSize = true,
			Margin = new Padding( 14, 8, 6, 0 )
		} );
		ConfigureUnitIntervalUpDown( _levelsInMaxUpDown, 1m );
		levelsPanel.Controls.Add( _levelsInMaxUpDown );

		levelsPanel.Controls.Add( new Label
		{
			Text = "Gamma",
			AutoSize = true,
			Margin = new Padding( 14, 8, 6, 0 )
		} );
		_levelsGammaUpDown.Minimum = 0.01m;
		_levelsGammaUpDown.Maximum = 8.00m;
		_levelsGammaUpDown.DecimalPlaces = 2;
		_levelsGammaUpDown.Increment = 0.05m;
		_levelsGammaUpDown.Value = 1.00m;
		_levelsGammaUpDown.Width = 70;
		_levelsGammaUpDown.Margin = new Padding( 0, 4, 0, 0 );
		levelsPanel.Controls.Add( _levelsGammaUpDown );

		levelsPanel.Controls.Add( new Label
		{
			Text = "Out Min",
			AutoSize = true,
			Margin = new Padding( 14, 8, 6, 0 )
		} );
		ConfigureUnitIntervalUpDown( _levelsOutMinUpDown, 0m );
		levelsPanel.Controls.Add( _levelsOutMinUpDown );

		levelsPanel.Controls.Add( new Label
		{
			Text = "Out Max",
			AutoSize = true,
			Margin = new Padding( 14, 8, 6, 0 )
		} );
		ConfigureUnitIntervalUpDown( _levelsOutMaxUpDown, 1m );
		levelsPanel.Controls.Add( _levelsOutMaxUpDown );

		materialPanel.Controls.Add( levelsPanel, 0, 6 );
		materialPanel.SetColumnSpan( levelsPanel, 5 );

		_overrideLevelsCheckBox.CheckedChanged += (_, _) => SetLevelsControlsEnabled( _overrideLevelsCheckBox.Checked );
		_profileOverrideCheckBox.CheckedChanged += (_, _) => UpdateProfileOverrideUi();
		_profileComboBox.SelectedIndexChanged += (_, _) => UpdateMaterialSourcePreview();
		_roughSourceComboBox.SelectedIndexChanged += (_, _) => UpdateMaterialSourcePreview();
		_roughChannelComboBox.SelectedIndexChanged += (_, _) => UpdateMaterialSourcePreview();
		_roughInvertCheckBox.CheckedChanged += (_, _) => UpdateMaterialSourcePreview();
		_metalSourceComboBox.SelectedIndexChanged += (_, _) => UpdateMaterialSourcePreview();
		_metalChannelComboBox.SelectedIndexChanged += (_, _) => UpdateMaterialSourcePreview();
		_metalInvertCheckBox.CheckedChanged += (_, _) => UpdateMaterialSourcePreview();

		UpdateProfileOverrideUi();
		UpdateMaterialSourcePreview();
		SetLevelsControlsEnabled( false );

		materialGroup.Controls.Add( materialPanel );
		settingsPanel.Controls.Add( materialGroup, 0, 0 );

		var conversionGroup = new GroupBox
		{
			Text = "Conversion",
			Dock = DockStyle.Fill,
			AutoSize = true,
			Padding = new Padding( 10 )
		};

		var conversionPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			ColumnCount = 2,
			AutoSize = true
		};
		conversionPanel.ColumnStyles.Add( new ColumnStyle( SizeType.AutoSize ) );
		conversionPanel.ColumnStyles.Add( new ColumnStyle( SizeType.AutoSize ) );

		_batchModeCheckBox.Text = "Batch mode";
		_batchModeCheckBox.AutoSize = true;
		_batchModeCheckBox.Margin = new Padding( 0, 0, 14, 0 );

		_recursiveBatchCheckBox.Text = "Recursive";
		_recursiveBatchCheckBox.AutoSize = true;
		_recursiveBatchCheckBox.Checked = true;
		_recursiveBatchCheckBox.Margin = new Padding( 0, 0, 0, 0 );

		var batchFlagsPanel = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			AutoSize = true,
			Margin = new Padding( 0, 0, 0, 6 )
		};
		batchFlagsPanel.Controls.Add( _batchModeCheckBox );
		batchFlagsPanel.Controls.Add( _recursiveBatchCheckBox );
		conversionPanel.Controls.Add( batchFlagsPanel, 0, 0 );
		conversionPanel.SetColumnSpan( batchFlagsPanel, 2 );

		conversionPanel.Controls.Add( new Label
		{
			Text = "Threads:",
			AutoSize = true,
			Margin = new Padding( 0, 8, 8, 0 )
		}, 0, 1 );

		_threadsUpDown.Minimum = 1;
		_threadsUpDown.Maximum = 128;
		_threadsUpDown.Value = Math.Max( 1, Environment.ProcessorCount );
		_threadsUpDown.Width = 90;
		_threadsUpDown.Margin = new Padding( 0, 4, 0, 4 );
		conversionPanel.Controls.Add( _threadsUpDown, 1, 1 );

		_preservePathCheckBox.Text = "Preserve models/... output path";
		_preservePathCheckBox.AutoSize = true;
		_preservePathCheckBox.Checked = true;
		_preservePathCheckBox.Margin = new Padding( 0, 2, 0, 2 );
		conversionPanel.Controls.Add( _preservePathCheckBox, 0, 2 );
		conversionPanel.SetColumnSpan( _preservePathCheckBox, 2 );

		_materialsCheckBox.Text = "Convert Materials";
		_materialsCheckBox.AutoSize = true;
		_materialsCheckBox.Checked = true;
		_materialsCheckBox.Margin = new Padding( 0, 2, 0, 2 );
		conversionPanel.Controls.Add( _materialsCheckBox, 0, 3 );
		conversionPanel.SetColumnSpan( _materialsCheckBox, 2 );

		_animationsCheckBox.Text = "Export Animations";
		_animationsCheckBox.AutoSize = true;
		_animationsCheckBox.Checked = false;
		_animationsCheckBox.Margin = new Padding( 0, 2, 0, 2 );
		conversionPanel.Controls.Add( _animationsCheckBox, 0, 4 );
		conversionPanel.SetColumnSpan( _animationsCheckBox, 2 );

		_copyShadersCheckBox.Text = "Copy Custom Shaders";
		_copyShadersCheckBox.AutoSize = true;
		_copyShadersCheckBox.Checked = true;
		_copyShadersCheckBox.Margin = new Padding( 0, 2, 0, 2 );
		conversionPanel.Controls.Add( _copyShadersCheckBox, 0, 5 );
		conversionPanel.SetColumnSpan( _copyShadersCheckBox, 2 );

		_verboseCheckBox.Text = "Verbose";
		_verboseCheckBox.AutoSize = true;
		_verboseCheckBox.Margin = new Padding( 0, 2, 0, 2 );
		conversionPanel.Controls.Add( _verboseCheckBox, 0, 6 );
		conversionPanel.SetColumnSpan( _verboseCheckBox, 2 );

		_convertButton.Text = "Convert";
		_convertButton.AutoSize = false;
		_convertButton.Width = 140;
		_convertButton.Height = 32;
		_convertButton.Margin = new Padding( 0, 10, 0, 0 );
		_convertButton.Click += async ( _, _ ) => await ConvertAsync();

		_themeToggleButton.AutoSize = false;
		_themeToggleButton.Width = 140;
		_themeToggleButton.Height = 32;
		_themeToggleButton.Margin = new Padding( 8, 10, 0, 0 );
		_themeToggleButton.Click += (_, _) =>
		{
			_isDarkTheme = !_isDarkTheme;
			ApplyCurrentTheme();
		};
		UpdateThemeToggleButtonText();

		var actionButtonsPanel = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			AutoSize = true,
			Margin = new Padding( 0, 0, 0, 0 )
		};
		actionButtonsPanel.Controls.Add( _convertButton );
		actionButtonsPanel.Controls.Add( _themeToggleButton );

		conversionPanel.Controls.Add( actionButtonsPanel, 0, 7 );
		conversionPanel.SetColumnSpan( actionButtonsPanel, 2 );

		conversionGroup.Controls.Add( conversionPanel );
		settingsPanel.Controls.Add( conversionGroup, 1, 0 );

		root.Controls.Add( settingsPanel, 0, 1 );

		var logGroup = new GroupBox
		{
			Text = "Log",
			Dock = DockStyle.Fill,
			Padding = new Padding( 10 )
		};

		_logTextBox.Multiline = true;
		_logTextBox.ScrollBars = RichTextBoxScrollBars.Both;
		_logTextBox.WordWrap = false;
		_logTextBox.Font = new Font( "Consolas", 10f, FontStyle.Regular, GraphicsUnit.Point );
		_logTextBox.Dock = DockStyle.Fill;
		_logTextBox.ReadOnly = true;
		logGroup.Controls.Add( _logTextBox );
		root.Controls.Add( logGroup, 0, 2 );

		string defaultOut = Path.Combine( Directory.GetCurrentDirectory(), "converted_output" );
		_outputRootTextBox.Text = defaultOut;

		ApplyCurrentTheme();
	}

	private static void AddPathRow( TableLayoutPanel panel, int row, string label, TextBox targetTextBox, EventHandler onBrowse )
	{
		EnsureRowCount( panel, row + 1 );
		panel.Controls.Add( new Label { Text = label, AutoSize = true, Margin = new Padding( 0, 9, 8, 0 ) }, 0, row );

		targetTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
		targetTextBox.Margin = new Padding( 0, 4, 8, 4 );
		panel.Controls.Add( targetTextBox, 1, row );

		var browseButton = new Button
		{
			Text = "Browse...",
			AutoSize = true,
			Margin = new Padding( 0, 3, 0, 3 )
		};
		browseButton.Click += onBrowse;
		panel.Controls.Add( browseButton, 2, row );
	}

	private static void AddTextRow( TableLayoutPanel panel, int row, string label, TextBox targetTextBox )
	{
		EnsureRowCount( panel, row + 1 );
		panel.Controls.Add( new Label { Text = label, AutoSize = true, Margin = new Padding( 0, 9, 8, 0 ) }, 0, row );

		targetTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
		targetTextBox.Margin = new Padding( 0, 4, 8, 4 );
		panel.Controls.Add( targetTextBox, 1, row );
		panel.Controls.Add( new Label { Text = string.Empty, AutoSize = true }, 2, row );
	}

	private static void EnsureRowCount( TableLayoutPanel panel, int rowCount )
	{
		while ( panel.RowCount < rowCount )
		{
			panel.RowStyles.Add( new RowStyle( SizeType.AutoSize ) );
			panel.RowCount++;
		}
	}

	private static void ConfigureUnitIntervalUpDown( NumericUpDown upDown, decimal defaultValue )
	{
		upDown.Minimum = 0.00m;
		upDown.Maximum = 1.00m;
		upDown.DecimalPlaces = 2;
		upDown.Increment = 0.01m;
		upDown.Value = defaultValue;
		upDown.Width = 64;
		upDown.Margin = new Padding( 0, 4, 0, 0 );
	}

	private void SetLevelsControlsEnabled( bool enabled )
	{
		_levelsInMinUpDown.Enabled = enabled;
		_levelsInMaxUpDown.Enabled = enabled;
		_levelsGammaUpDown.Enabled = enabled;
		_levelsOutMinUpDown.Enabled = enabled;
		_levelsOutMaxUpDown.Enabled = enabled;
	}

	private void UpdateProfileOverrideUi()
	{
		_profileComboBox.Enabled = !_profileOverrideCheckBox.Checked;
		UpdateMaterialSourcePreview();
	}

	private void UpdateMaterialSourcePreview()
	{
		MaterialProfileOverride selectedProfile = MaterialProfileOverride.Auto;
		if ( _profileComboBox.SelectedItem is string profileText
			&& Enum.TryParse( profileText, ignoreCase: true, out MaterialProfileOverride parsedProfile ) )
		{
			selectedProfile = parsedProfile;
		}

		MaterialOverrideTextureSource roughSource = MaterialOverrideTextureSource.Auto;
		if ( _roughSourceComboBox.SelectedItem is string roughSourceText
			&& Enum.TryParse( roughSourceText, ignoreCase: true, out MaterialOverrideTextureSource parsedRoughSource ) )
		{
			roughSource = parsedRoughSource;
		}

		MaterialOverrideChannel roughChannel = MaterialOverrideChannel.Alpha;
		if ( _roughChannelComboBox.SelectedItem is string roughChannelText
			&& Enum.TryParse( roughChannelText, ignoreCase: true, out MaterialOverrideChannel parsedRoughChannel ) )
		{
			roughChannel = parsedRoughChannel;
		}
		bool roughInvert = _roughInvertCheckBox.Checked;

		MaterialOverrideTextureSource metalSource = MaterialOverrideTextureSource.Auto;
		if ( _metalSourceComboBox.SelectedItem is string metalSourceText
			&& Enum.TryParse( metalSourceText, ignoreCase: true, out MaterialOverrideTextureSource parsedMetalSource ) )
		{
			metalSource = parsedMetalSource;
		}

		MaterialOverrideChannel metalChannel = MaterialOverrideChannel.Alpha;
		if ( _metalChannelComboBox.SelectedItem is string metalChannelText
			&& Enum.TryParse( metalChannelText, ignoreCase: true, out MaterialOverrideChannel parsedMetalChannel ) )
		{
			metalChannel = parsedMetalChannel;
		}
		bool metalInvert = _metalInvertCheckBox.Checked;

		_roughSourcePreviewLabel.Text = "Source: " + BuildMapSourcePreview(
			isRoughness: true,
			selectedProfile,
			roughSource,
			roughChannel,
			roughInvert
		);

		_metalSourcePreviewLabel.Text = "Source: " + BuildMapSourcePreview(
			isRoughness: false,
			selectedProfile,
			metalSource,
			metalChannel,
			metalInvert
		);
	}

	private string BuildMapSourcePreview(
		bool isRoughness,
		MaterialProfileOverride selectedProfile,
		MaterialOverrideTextureSource overrideSource,
		MaterialOverrideChannel overrideChannel,
		bool invert )
	{
		if ( overrideSource != MaterialOverrideTextureSource.Auto )
		{
			return $"Manual override -> {overrideSource} ({overrideChannel}{(invert ? ", inverted" : string.Empty)})";
		}

		if ( _profileOverrideCheckBox.Checked )
		{
			return isRoughness
				? "Profile override ON -> Base/Bump baseline, SourceEngine roughness auto (phong/envmask fallback)"
				: "Profile override ON -> Base/Bump baseline, SourceEngine metalness default";
		}

		if ( selectedProfile == MaterialProfileOverride.Auto )
		{
			return "Auto detect from VMT profile per material";
		}

		return selectedProfile switch
		{
			MaterialProfileOverride.ExoPbr => isRoughness
				? "ExoPBR auto -> ARM texture (roughness channel)"
				: "ExoPBR auto -> ARM texture (metalness channel)",
			MaterialProfileOverride.Gpbr => isRoughness
				? "GPBR auto -> MRAO texture (roughness channel)"
				: "GPBR auto -> MRAO texture (metalness channel)",
			MaterialProfileOverride.MwbPbr => isRoughness
				? "MWBPBR auto -> Normal map (Alpha, converted)"
				: "MWBPBR auto -> Base texture (Alpha)",
			MaterialProfileOverride.BftPseudoPbr => isRoughness
				? "BFT auto -> Phong exponent texture (converted)"
				: "BFT auto -> Base texture (Alpha)",
			MaterialProfileOverride.MadIvan18 => isRoughness
				? "MadIvan18 auto -> Normal map (Alpha, inverted)"
				: "MadIvan18 auto -> Phong exponent texture (Red)",
			MaterialProfileOverride.SourceEngine => isRoughness
				? "SourceEngine auto -> Phong exponent or envmask fallback"
				: "SourceEngine auto -> default metallic value",
			_ => "Auto detect from VMT profile per material"
		};
	}

	private void OnBrowseMdl( object? sender, EventArgs e )
	{
		using var dialog = new OpenFileDialog
		{
			Filter = "MDL Files (*.mdl)|*.mdl|All Files (*.*)|*.*",
			CheckFileExists = true,
			Multiselect = false
		};

		if ( dialog.ShowDialog( this ) != DialogResult.OK )
		{
			return;
		}

		_mdlPathTextBox.Text = dialog.FileName;

		if ( string.IsNullOrWhiteSpace( _gmodRootTextBox.Text ) )
		{
			string? gmodRoot = ConversionRunner.ResolveGmodRoot( null, dialog.FileName );
			if ( !string.IsNullOrWhiteSpace( gmodRoot ) )
			{
				_gmodRootTextBox.Text = gmodRoot;
			}
		}
	}

	private void OnBrowseGmodRoot( object? sender, EventArgs e )
	{
		using var dialog = new FolderBrowserDialog
		{
			Description = "Select Garry's Mod root or garrysmod folder"
		};

		if ( dialog.ShowDialog( this ) == DialogResult.OK )
		{
			_gmodRootTextBox.Text = dialog.SelectedPath;
		}
	}

	private void OnBrowseBatchRoot( object? sender, EventArgs e )
	{
		using var dialog = new FolderBrowserDialog
		{
			Description = "Select a folder that contains .mdl files"
		};

		if ( dialog.ShowDialog( this ) == DialogResult.OK )
		{
			_batchRootTextBox.Text = dialog.SelectedPath;
			_batchModeCheckBox.Checked = true;

			if ( string.IsNullOrWhiteSpace( _gmodRootTextBox.Text ) )
			{
				string? gmodRoot = ConversionRunner.ResolveGmodRoot( null, dialog.SelectedPath );
				if ( !string.IsNullOrWhiteSpace( gmodRoot ) )
				{
					_gmodRootTextBox.Text = gmodRoot;
				}
			}
		}
	}

	private void OnBrowseOutputRoot( object? sender, EventArgs e )
	{
		using var dialog = new FolderBrowserDialog
		{
			Description = "Select output root folder"
		};

		if ( dialog.ShowDialog( this ) == DialogResult.OK )
		{
			_outputRootTextBox.Text = dialog.SelectedPath;
		}
	}

	private async Task ConvertAsync()
	{
		string mdlPath = _mdlPathTextBox.Text.Trim();
		string batchRoot = _batchRootTextBox.Text.Trim();
		bool batchMode = _batchModeCheckBox.Checked || !string.IsNullOrWhiteSpace( batchRoot );

		if ( batchMode && string.IsNullOrWhiteSpace( batchRoot ) && Directory.Exists( mdlPath ) )
		{
			batchRoot = mdlPath;
		}

		if ( !batchMode && string.IsNullOrWhiteSpace( mdlPath ) )
		{
			MessageBox.Show( this, "Please choose an MDL file.", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		if ( !batchMode && !File.Exists( mdlPath ) )
		{
			MessageBox.Show( this, $"MDL file not found:\n{mdlPath}", "Missing file", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		if ( batchMode && (string.IsNullOrWhiteSpace( batchRoot ) || !Directory.Exists( batchRoot )) )
		{
			MessageBox.Show( this, "Please choose a valid batch root folder.", "Missing batch folder", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		string outputRoot = _outputRootTextBox.Text.Trim();
		if ( string.IsNullOrWhiteSpace( outputRoot ) )
		{
			MessageBox.Show( this, "Please choose an output root folder.", "Missing output", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		MaterialProfileOverride profileOverride = MaterialProfileOverride.Auto;
		if ( _profileComboBox.SelectedItem is string selected
			&& Enum.TryParse( selected, ignoreCase: true, out MaterialProfileOverride parsed ) )
		{
			profileOverride = parsed;
		}

		if ( _profileOverrideCheckBox.Checked )
		{
			profileOverride = MaterialProfileOverride.SourceEngine;
		}

		MaterialOverrideTextureSource roughnessOverrideSource = MaterialOverrideTextureSource.Auto;
		if ( _roughSourceComboBox.SelectedItem is string roughSourceSelected
			&& Enum.TryParse( roughSourceSelected, ignoreCase: true, out MaterialOverrideTextureSource parsedRoughSource ) )
		{
			roughnessOverrideSource = parsedRoughSource;
		}

		MaterialOverrideChannel roughnessOverrideChannel = MaterialOverrideChannel.Alpha;
		if ( _roughChannelComboBox.SelectedItem is string roughChannelSelected
			&& Enum.TryParse( roughChannelSelected, ignoreCase: true, out MaterialOverrideChannel parsedRoughChannel ) )
		{
			roughnessOverrideChannel = parsedRoughChannel;
		}
		bool roughnessOverrideInvert = _roughInvertCheckBox.Checked;

		MaterialOverrideTextureSource metalnessOverrideSource = MaterialOverrideTextureSource.Auto;
		if ( _metalSourceComboBox.SelectedItem is string metalSourceSelected
			&& Enum.TryParse( metalSourceSelected, ignoreCase: true, out MaterialOverrideTextureSource parsedMetalSource ) )
		{
			metalnessOverrideSource = parsedMetalSource;
		}

		MaterialOverrideChannel metalnessOverrideChannel = MaterialOverrideChannel.Alpha;
		if ( _metalChannelComboBox.SelectedItem is string metalChannelSelected
			&& Enum.TryParse( metalChannelSelected, ignoreCase: true, out MaterialOverrideChannel parsedMetalChannel ) )
		{
			metalnessOverrideChannel = parsedMetalChannel;
		}
		bool metalnessOverrideInvert = _metalInvertCheckBox.Checked;

		if ( _levelsInMinUpDown.Value > _levelsInMaxUpDown.Value )
		{
			MessageBox.Show( this, "Levels input min cannot be greater than input max.", "Invalid levels", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		if ( _levelsOutMinUpDown.Value > _levelsOutMaxUpDown.Value )
		{
			MessageBox.Show( this, "Levels output min cannot be greater than output max.", "Invalid levels", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		var options = new ConverterOptions
		{
			MdlPath = batchMode ? null : Path.GetFullPath( mdlPath ),
			BatchRootDirectory = batchMode ? Path.GetFullPath( batchRoot ) : null,
			RecursiveSearch = _recursiveBatchCheckBox.Checked,
			MaxParallelism = (int)_threadsUpDown.Value,
			VvdPath = null,
			VtxPath = null,
			PhyPath = null,
			OutputDirectory = Path.GetFullPath( outputRoot ),
			VmdlFileName = _vmdlNameTextBox.Text.Trim(),
			GmodRootDirectory = string.IsNullOrWhiteSpace( _gmodRootTextBox.Text ) ? null : Path.GetFullPath( _gmodRootTextBox.Text.Trim() ),
			ShaderSourceDirectory = null,
			PreserveModelRelativePath = _preservePathCheckBox.Checked,
			ConvertMaterials = _materialsCheckBox.Checked,
			ExportAnimations = _animationsCheckBox.Checked,
			CopyShaders = _copyShadersCheckBox.Checked,
			Verbose = _verboseCheckBox.Checked,
			MaterialProfileOverride = profileOverride,
			RoughnessOverrideSource = roughnessOverrideSource,
			RoughnessOverrideChannel = roughnessOverrideChannel,
			RoughnessOverrideInvert = roughnessOverrideInvert,
			MetalnessOverrideSource = metalnessOverrideSource,
			MetalnessOverrideChannel = metalnessOverrideChannel,
			MetalnessOverrideInvert = metalnessOverrideInvert,
			MaterialOverrideLevelsEnabled = _overrideLevelsCheckBox.Checked,
			MaterialOverrideInputMin = (float)_levelsInMinUpDown.Value,
			MaterialOverrideInputMax = (float)_levelsInMaxUpDown.Value,
			MaterialOverrideGamma = (float)_levelsGammaUpDown.Value,
			MaterialOverrideOutputMin = (float)_levelsOutMinUpDown.Value,
			MaterialOverrideOutputMax = (float)_levelsOutMaxUpDown.Value
		};

		_logTextBox.Clear();
		_convertButton.Enabled = false;
		AppendLog( batchMode ? "Starting batch conversion..." : "Starting conversion..." );

		try
		{
			if ( options.IsBatchMode )
			{
				BatchConversionSummary summary = await Task.Run( () =>
					ConversionRunner.RunBatch(
						options,
						message => AppendLog( message ),
						warning => AppendLog( warning )
					)
				);

				AppendLog( "Batch conversion complete." );
				AppendLog( $"Output root: {summary.OutputRoot}" );
				AppendLog( $"Models discovered: {summary.TotalModels}" );
				AppendLog( $"Succeeded: {summary.Succeeded}" );
				AppendLog( $"Failed: {summary.Failed}" );
				AppendLog( $"Total SMD files: {summary.TotalSmdCount}" );
				AppendLog( $"Total DMX files: {summary.TotalDmxCount}" );
				AppendLog( $"Total animation files: {summary.TotalAnimationCount}" );
				AppendLog( $"Total material remaps: {summary.TotalMaterialRemapCount}" );
				AppendLog( $"Total morph channels: {summary.TotalMorphChannelCount}" );

				if ( summary.Failures.Count > 0 )
				{
					AppendLog( "Failed models:" );
					foreach ( BatchConversionFailure failure in summary.Failures )
					{
						AppendLog( $"  - {failure.MdlPath}" );
						AppendLog( $"    {failure.Error}" );
					}
				}
			}
			else
			{
				ConversionSummary summary = await Task.Run( () =>
					ConversionRunner.Run(
						options,
						message => AppendLog( message ),
						warning => AppendLog( warning )
					)
				);

				AppendLog( "Conversion complete." );
				AppendLog( $"Model output: {summary.ModelOutputDirectory}" );
				AppendLog( $"VMDL: {summary.VmdlPath}" );
				AppendLog( $"SMD files: {summary.SmdCount}" );
				AppendLog( $"DMX files: {summary.DmxCount}" );
				AppendLog( $"Anim files: {summary.AnimationCount}" );
				AppendLog( $"Material remaps: {summary.MaterialRemapCount}" );
				AppendLog( $"Morph channels: {summary.MorphChannelCount}" );
			}
		}
		catch ( Exception ex )
		{
			AppendLog( "Conversion failed." );
			AppendLog( ex.ToString() );
			MessageBox.Show( this, ex.Message, "Conversion failed", MessageBoxButtons.OK, MessageBoxIcon.Error );
		}
		finally
		{
			_convertButton.Enabled = true;
		}
	}

	private void AppendLog( string message )
	{
		if ( _logTextBox.InvokeRequired )
		{
			_logTextBox.BeginInvoke( () => AppendLog( message ) );
			return;
		}

		_logTextBox.AppendText( message + Environment.NewLine );
	}

	private Color ActiveWindowBackColor => _isDarkTheme ? WindowBackColor : LightWindowBackColor;
	private Color ActivePanelBackColor => _isDarkTheme ? PanelBackColor : LightPanelBackColor;
	private Color ActiveInputBackColor => _isDarkTheme ? InputBackColor : LightInputBackColor;
	private Color ActiveLogBackColor => _isDarkTheme ? LogBackColor : LightLogBackColor;
	private Color ActiveButtonBackColor => _isDarkTheme ? ButtonBackColor : LightButtonBackColor;
	private Color ActiveButtonHoverBackColor => _isDarkTheme ? ButtonHoverBackColor : LightButtonHoverBackColor;
	private Color ActiveButtonDownBackColor => _isDarkTheme ? ButtonDownBackColor : LightButtonDownBackColor;
	private Color ActiveBorderColor => _isDarkTheme ? BorderColor : LightBorderColor;
	private Color ActiveTextColor => _isDarkTheme ? TextColor : LightTextColor;
	private Color ActiveMutedTextColor => _isDarkTheme ? MutedTextColor : LightMutedTextColor;

	private void ApplyCurrentTheme()
	{
		BackColor = ActiveWindowBackColor;
		ForeColor = ActiveTextColor;
		ApplyDarkThemeRecursive( this );
		UpdateThemeToggleButtonText();
		ApplyNativeTheme();
		Invalidate( true );
		Update();
	}

	private void ApplyDarkThemeRecursive( Control control )
	{
		switch ( control )
		{
			case Form:
				control.BackColor = ActiveWindowBackColor;
				control.ForeColor = ActiveTextColor;
				break;
			case GroupBox:
				control.BackColor = ActivePanelBackColor;
				control.ForeColor = ActiveTextColor;
				break;
			case TableLayoutPanel:
			case FlowLayoutPanel:
			case Panel:
				control.BackColor = IsInsideGroupBox( control ) ? ActivePanelBackColor : ActiveWindowBackColor;
				control.ForeColor = ActiveTextColor;
				break;
			case Label label:
				label.ForeColor = (ReferenceEquals( label, _roughSourcePreviewLabel ) || ReferenceEquals( label, _metalSourcePreviewLabel ))
					? ActiveMutedTextColor
					: ActiveTextColor;
				label.BackColor = label.Parent?.BackColor ?? Color.Transparent;
				break;
			case TextBox textBox:
				textBox.BorderStyle = BorderStyle.FixedSingle;
				textBox.BackColor = ReferenceEquals( textBox, _logTextBox ) ? ActiveLogBackColor : ActiveInputBackColor;
				textBox.ForeColor = ActiveTextColor;
				break;
			case RichTextBox richTextBox:
				richTextBox.BorderStyle = BorderStyle.FixedSingle;
				richTextBox.BackColor = ActiveLogBackColor;
				richTextBox.ForeColor = ActiveTextColor;
				break;
			case ComboBox comboBox:
				comboBox.FlatStyle = FlatStyle.Flat;
				comboBox.BackColor = ActiveInputBackColor;
				comboBox.ForeColor = ActiveTextColor;
				break;
			case NumericUpDown numericUpDown:
				numericUpDown.BorderStyle = BorderStyle.FixedSingle;
				numericUpDown.BackColor = ActiveInputBackColor;
				numericUpDown.ForeColor = ActiveTextColor;
				break;
			case CheckBox checkBox:
				checkBox.ForeColor = ActiveTextColor;
				if ( checkBox.Appearance == Appearance.Button )
				{
					checkBox.FlatStyle = FlatStyle.Flat;
					checkBox.BackColor = ActiveButtonBackColor;
					checkBox.FlatAppearance.BorderColor = ActiveBorderColor;
					checkBox.FlatAppearance.CheckedBackColor = ActiveButtonHoverBackColor;
					checkBox.FlatAppearance.MouseOverBackColor = ActiveButtonHoverBackColor;
					checkBox.FlatAppearance.MouseDownBackColor = ActiveButtonDownBackColor;
				}
				break;
			case Button button:
				button.FlatStyle = FlatStyle.Flat;
				button.BackColor = ActiveButtonBackColor;
				button.ForeColor = ActiveTextColor;
				button.FlatAppearance.BorderColor = ActiveBorderColor;
				button.FlatAppearance.MouseOverBackColor = ActiveButtonHoverBackColor;
				button.FlatAppearance.MouseDownBackColor = ActiveButtonDownBackColor;
				break;
			default:
				control.ForeColor = ActiveTextColor;
				break;
		}

		foreach ( Control child in control.Controls )
		{
			ApplyDarkThemeRecursive( child );
		}
	}

	private static bool IsInsideGroupBox( Control control )
	{
		Control? current = control.Parent;
		while ( current is not null )
		{
			if ( current is GroupBox )
			{
				return true;
			}

			current = current.Parent;
		}

		return false;
	}

	private void UpdateThemeToggleButtonText()
	{
		_themeToggleButton.Text = _isDarkTheme ? "Light Theme" : "Dark Theme";
	}

	private void ApplyNativeTheme()
	{
		TrySetTitleBarTheme( _isDarkTheme );
		TryApplyThemeClass( _logTextBox, _isDarkTheme ? "DarkMode_Explorer" : "Explorer" );
	}

	private void TrySetTitleBarTheme( bool dark )
	{
		if ( !IsHandleCreated )
		{
			return;
		}

		int enabled = dark ? 1 : 0;
		try
		{
			int hr = DwmSetWindowAttribute( Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof( int ) );
			if ( hr != 0 )
			{
				_ = DwmSetWindowAttribute( Handle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof( int ) );
			}

			_ = SetWindowPos(
				Handle,
				IntPtr.Zero,
				0,
				0,
				0,
				0,
				SwpNomove | SwpNosize | SwpNozorder | SwpNoownerzorder | SwpNoactivate | SwpFramechanged
			);
		}
		catch ( DllNotFoundException )
		{
			// Ignore where DWM API is unavailable.
		}
		catch ( EntryPointNotFoundException )
		{
			// Ignore on older Windows builds.
		}
	}

	private static void TryApplyThemeClass( Control control, string className )
	{
		if ( !control.IsHandleCreated )
		{
			control.CreateControl();
		}

		if ( !control.IsHandleCreated )
		{
			return;
		}

		try
		{
			_ = SetWindowTheme( control.Handle, className, null );
		}
		catch ( DllNotFoundException )
		{
			// Ignore where UXTheme API is unavailable.
		}
		catch ( EntryPointNotFoundException )
		{
			// Ignore where UXTheme API is unavailable.
		}
	}

	[DllImport( "dwmapi.dll" )]
	private static extern int DwmSetWindowAttribute( IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute );

	[DllImport( "uxtheme.dll", CharSet = CharSet.Unicode )]
	private static extern int SetWindowTheme( IntPtr hWnd, string? pszSubAppName, string? pszSubIdList );

	[DllImport( "user32.dll", SetLastError = true )]
	private static extern bool SetWindowPos(
		IntPtr hWnd,
		IntPtr hWndInsertAfter,
		int x,
		int y,
		int cx,
		int cy,
		uint uFlags
	);
}

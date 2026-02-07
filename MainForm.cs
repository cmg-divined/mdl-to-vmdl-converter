using System.Windows.Forms;

internal sealed class MainForm : Form
{
	private readonly TextBox _mdlPathTextBox = new();
	private readonly TextBox _gmodRootTextBox = new();
	private readonly TextBox _outputRootTextBox = new();
	private readonly TextBox _vmdlNameTextBox = new();
	private readonly ComboBox _profileComboBox = new();
	private readonly CheckBox _preservePathCheckBox = new();
	private readonly CheckBox _materialsCheckBox = new();
	private readonly CheckBox _copyShadersCheckBox = new();
	private readonly CheckBox _verboseCheckBox = new();
	private readonly Button _convertButton = new();
	private readonly TextBox _logTextBox = new();

	public MainForm()
	{
		Text = "MDL to VMDL Converter";
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size( 980, 720 );
		Width = 1080;
		Height = 760;

		BuildUi();
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
		AddPathRow( inputPanel, 1, "GMod Root", _gmodRootTextBox, OnBrowseGmodRoot );
		AddPathRow( inputPanel, 2, "Output Root", _outputRootTextBox, OnBrowseOutputRoot );
		AddTextRow( inputPanel, 3, "VMDL Name (optional)", _vmdlNameTextBox );

		root.Controls.Add( inputPanel, 0, 0 );

		var optionsPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			FlowDirection = FlowDirection.LeftToRight,
			AutoSize = true,
			WrapContents = true,
			Padding = new Padding( 0, 10, 0, 10 )
		};

		optionsPanel.Controls.Add( new Label
		{
			Text = "Material Profile:",
			AutoSize = true,
			Margin = new Padding( 0, 8, 8, 0 )
		} );

		_profileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		_profileComboBox.Width = 180;
		_profileComboBox.Items.AddRange( Enum.GetNames<MaterialProfileOverride>() );
		_profileComboBox.SelectedItem = MaterialProfileOverride.Auto.ToString();
		optionsPanel.Controls.Add( _profileComboBox );

		_preservePathCheckBox.Text = "Preserve models/... output path";
		_preservePathCheckBox.AutoSize = true;
		_preservePathCheckBox.Checked = true;
		_preservePathCheckBox.Margin = new Padding( 18, 6, 0, 0 );
		optionsPanel.Controls.Add( _preservePathCheckBox );

		_materialsCheckBox.Text = "Convert Materials";
		_materialsCheckBox.AutoSize = true;
		_materialsCheckBox.Checked = true;
		_materialsCheckBox.Margin = new Padding( 18, 6, 0, 0 );
		optionsPanel.Controls.Add( _materialsCheckBox );

		_copyShadersCheckBox.Text = "Copy Custom Shaders";
		_copyShadersCheckBox.AutoSize = true;
		_copyShadersCheckBox.Checked = true;
		_copyShadersCheckBox.Margin = new Padding( 18, 6, 0, 0 );
		optionsPanel.Controls.Add( _copyShadersCheckBox );

		_verboseCheckBox.Text = "Verbose";
		_verboseCheckBox.AutoSize = true;
		_verboseCheckBox.Margin = new Padding( 18, 6, 0, 0 );
		optionsPanel.Controls.Add( _verboseCheckBox );

		_convertButton.Text = "Convert";
		_convertButton.AutoSize = true;
		_convertButton.Margin = new Padding( 24, 2, 0, 0 );
		_convertButton.Click += async ( _, _ ) => await ConvertAsync();
		optionsPanel.Controls.Add( _convertButton );

		root.Controls.Add( optionsPanel, 0, 1 );

		_logTextBox.Multiline = true;
		_logTextBox.ScrollBars = ScrollBars.Both;
		_logTextBox.WordWrap = false;
		_logTextBox.Font = new Font( "Consolas", 10f, FontStyle.Regular, GraphicsUnit.Point );
		_logTextBox.Dock = DockStyle.Fill;
		root.Controls.Add( _logTextBox, 0, 2 );

		string defaultOut = Path.Combine( Directory.GetCurrentDirectory(), "converted_output" );
		_outputRootTextBox.Text = defaultOut;
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
		if ( string.IsNullOrWhiteSpace( mdlPath ) )
		{
			MessageBox.Show( this, "Please choose an MDL file.", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		if ( !File.Exists( mdlPath ) )
		{
			MessageBox.Show( this, $"MDL file not found:\n{mdlPath}", "Missing file", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		string outputRoot = _outputRootTextBox.Text.Trim();
		if ( string.IsNullOrWhiteSpace( outputRoot ) )
		{
			MessageBox.Show( this, "Please choose an output root folder.", "Missing output", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		MaterialProfileOverride profileOverride = MaterialProfileOverride.Auto;
		if ( _profileComboBox.SelectedItem is string selected && Enum.TryParse( selected, ignoreCase: true, out MaterialProfileOverride parsed ) )
		{
			profileOverride = parsed;
		}

		var options = new ConverterOptions
		{
			MdlPath = Path.GetFullPath( mdlPath ),
			VvdPath = null,
			VtxPath = null,
			PhyPath = null,
			OutputDirectory = Path.GetFullPath( outputRoot ),
			VmdlFileName = _vmdlNameTextBox.Text.Trim(),
			GmodRootDirectory = string.IsNullOrWhiteSpace( _gmodRootTextBox.Text ) ? null : Path.GetFullPath( _gmodRootTextBox.Text.Trim() ),
			ShaderSourceDirectory = null,
			PreserveModelRelativePath = _preservePathCheckBox.Checked,
			ConvertMaterials = _materialsCheckBox.Checked,
			CopyShaders = _copyShadersCheckBox.Checked,
			Verbose = _verboseCheckBox.Checked,
			MaterialProfileOverride = profileOverride
		};

		_logTextBox.Clear();
		_convertButton.Enabled = false;
		AppendLog( "Starting conversion..." );

		try
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
			AppendLog( $"Material remaps: {summary.MaterialRemapCount}" );
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
}

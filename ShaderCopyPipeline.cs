internal static class ShaderCopyPipeline
{
	public static void Copy( ConverterOptions options, string outputRoot, Action<string>? info = null, Action<string>? warn = null )
	{
		info ??= _ => { };
		warn ??= _ => { };

		string? sourceDirectory = ResolveShaderSourceDirectory( options.ShaderSourceDirectory );
		if ( string.IsNullOrWhiteSpace( sourceDirectory ) )
		{
			warn( "[warn] Shader source directory not found. Skipping shader copy." );
			return;
		}

		string targetDirectory = Path.Combine( outputRoot, "shaders" );
		Directory.CreateDirectory( targetDirectory );

		int copied = 0;
		foreach ( string file in Directory.EnumerateFiles( sourceDirectory, "*", SearchOption.TopDirectoryOnly ) )
		{
			string extension = Path.GetExtension( file );
			if ( !string.Equals( extension, ".shader", StringComparison.OrdinalIgnoreCase )
				&& !string.Equals( extension, ".shader_c", StringComparison.OrdinalIgnoreCase )
				&& !string.Equals( extension, ".shdrgrph", StringComparison.OrdinalIgnoreCase ) )
			{
				continue;
			}

			string destination = Path.Combine( targetDirectory, Path.GetFileName( file ) );
			File.Copy( file, destination, overwrite: true );
			copied++;
		}

		if ( copied > 0 )
		{
			info( $"Copied {copied} shader files to {targetDirectory}" );
		}
	}

	private static string? ResolveShaderSourceDirectory( string? explicitSourceDirectory )
	{
		if ( !string.IsNullOrWhiteSpace( explicitSourceDirectory ) )
		{
			string full = Path.GetFullPath( explicitSourceDirectory );
			if ( Directory.Exists( full ) )
			{
				return full;
			}
		}

		string? fromCurrentLocal = FindConverterShaderFolderUpTree( Directory.GetCurrentDirectory() );
		if ( !string.IsNullOrWhiteSpace( fromCurrentLocal ) )
		{
			return fromCurrentLocal;
		}

		string? fromAppBaseLocal = FindConverterShaderFolderUpTree( AppContext.BaseDirectory );
		if ( !string.IsNullOrWhiteSpace( fromAppBaseLocal ) )
		{
			return fromAppBaseLocal;
		}

		string? fromCurrent = FindGmodShaderFolderUpTree( Directory.GetCurrentDirectory() );
		if ( !string.IsNullOrWhiteSpace( fromCurrent ) )
		{
			return fromCurrent;
		}

		string? fromAppBase = FindGmodShaderFolderUpTree( AppContext.BaseDirectory );
		if ( !string.IsNullOrWhiteSpace( fromAppBase ) )
		{
			return fromAppBase;
		}

		return null;
	}

	private static string? FindConverterShaderFolderUpTree( string startDirectory )
	{
		DirectoryInfo? dir = new DirectoryInfo( startDirectory );
		while ( dir is not null )
		{
			string direct = Path.Combine( dir.FullName, "shaders" );
			if ( Directory.Exists( direct ) )
			{
				return direct;
			}

			string nested = Path.Combine( dir.FullName, "MdlToVmdlConverter", "shaders" );
			if ( Directory.Exists( nested ) )
			{
				return nested;
			}

			dir = dir.Parent;
		}

		return null;
	}

	private static string? FindGmodShaderFolderUpTree( string startDirectory )
	{
		DirectoryInfo? dir = new DirectoryInfo( startDirectory );
		while ( dir is not null )
		{
			string candidate = Path.Combine( dir.FullName, "gmod_mount", "assets", "shaders" );
			if ( Directory.Exists( candidate ) )
			{
				return candidate;
			}

			dir = dir.Parent;
		}

		return null;
	}
}

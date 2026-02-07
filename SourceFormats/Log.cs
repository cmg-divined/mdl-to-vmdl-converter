namespace GModMount.Source;

public static class Log
{
	public static bool Verbose { get; set; }

	public static void Info( string message )
	{
		if ( Verbose )
		{
			Console.WriteLine( "[info] " + message );
		}
	}

	public static void Warning( string message )
	{
		Console.Error.WriteLine( "[warn] " + message );
	}
}
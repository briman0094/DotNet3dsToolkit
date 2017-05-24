using System.IO;
using DotNet3dsToolkit.Properties;

namespace DotNet3dsToolkit
{
	public class MissingFileException : IOException
	{
		public MissingFileException( string path ) : base( string.Format( Language.ErrorMissingFile, path ) )
		{
			this.Path = path;
		}

		public string Path { get; set; }
	}
}
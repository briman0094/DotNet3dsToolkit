using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DotNet3dsToolkit.Properties;
using SkyEditor.Core.IO;

namespace DotNet3dsToolkit
{
	/// <summary>
	/// Reads metadata from packed or unpacked ROMs.
	/// </summary>
	public class MetadataReader
	{
		#region "CIA"

		/// <remarks>From CTRTool
		/// https://github.com/profi200/Project_CTR/blob/d32f096e3ea8d6cacc2f8e8f43d4eec51394eca2/ctrtool/utils.c </remarks>
		private static int Align( int offset, int alignment )
		{
			int mask = ~( alignment - 1 );
			return offset + ( alignment - 1 ) & mask;
		}

		/// <summary>
		/// Gets the offset of the content section of the given CIA file.
		/// </summary>
		internal static int GetCIAContentOffset( GenericFile cia )
		{
			int offsetCerts = MetadataReader.Align( cia.ReadInt32( 0 ), 64 );
			int offsetTik = MetadataReader.Align( cia.ReadInt32( 0x8 ) + offsetCerts, 64 );
			int offsetTmd = MetadataReader.Align( cia.ReadInt32( 0xc ) + offsetTik, 64 );
			int offsetContent = MetadataReader.Align( cia.ReadInt32( 0x10 ) + offsetTmd, 64 );
			return offsetContent;
		}

		#endregion

		/// <summary>
		/// Gets the system corresponding to the given directory.
		/// </summary>
		/// <param name="path">The directory containing the unpacked ROM to check.</param>
		/// <returns>A <see cref="SystemType"/> corresponding to the extracted files located in the directory <paramref name="path"/>.</returns>
		public static SystemType GetDirectorySystem( string path )
		{
			if ( File.Exists( Path.Combine( path, "arm9.bin" ) ) && File.Exists( Path.Combine( path, "arm7.bin" ) ) && File.Exists( Path.Combine( path, "header.bin" ) ) && Directory.Exists( Path.Combine( path, "data" ) ) )
			{
				return SystemType.NDS;
			}
			if ( File.Exists( Path.Combine( path, "exheader.bin" ) ) && Directory.Exists( Path.Combine( path, "exefs" ) ) && Directory.Exists( Path.Combine( path, "romfs" ) ) )
			{
				return SystemType.ThreeDS;
			}
			return SystemType.Unknown;
		}

		/// <summary>
		/// Gets the game ID from the unpacked ROM in the given directory.
		/// </summary>
		/// <param name="path">The directory containing the unpacked ROM to check.</param>
		/// <param name="system">The type of system the unpacked ROM is for.</param>
		/// <returns>The unpacked ROM's game code.</returns>
		public static string GetDirectoryGameID( string path, SystemType system )
		{
			switch ( system )
			{
				case SystemType.NDS:
					byte[] header = File.ReadAllBytes( Path.Combine( path, "header.bin" ) );
					ASCIIEncoding e = new ASCIIEncoding();
					return e.GetString( header, 0xc, 4 );
				case SystemType.ThreeDS:
					byte[] exheader = File.ReadAllBytes( Path.Combine( path, "exheader.bin" ) );
					return BitConverter.ToUInt64( exheader, 0x200 ).ToString( "X" ).PadLeft( 16, '0' );
				default:
					throw new NotSupportedException( string.Format( Language.ErrorSystemNotSupported, system ) );
			}
		}

		/// <summary>
		/// Gets the game ID from the unpacked ROM in the given directory.
		/// </summary>
		/// <param name="path">The directory containing the unpacked ROM to check.</param>
		/// <returns>The unpacked ROM's game code.</returns>
		public static string GetDirectoryGameID( string path )
		{
			return MetadataReader.GetDirectoryGameID( path, MetadataReader.GetDirectorySystem( path ) );
		}

		/// <summary>
		/// Gets the system corresponding to the given ROM.
		/// </summary>
		/// <param name="path">The filename of the ROM to check.</param>
		/// <returns>A <see cref="SystemType"/> corresponding to ROM located at <paramref name="path"/>.</returns>
		public static async Task<SystemType> GetROMSystem( string path )
		{
			var e = new ASCIIEncoding();
			var n = new GenericNDSRom();

			using ( var file = new GenericFile() )
			{
				file.EnableInMemoryLoad = false;
				file.IsReadOnly = true;

				await file.OpenFile( path, new PhysicalIOProvider() );

				if ( await n.IsFileOfType( file ) )
					return SystemType.NDS;

				if ( file.Length > 104 && e.GetString( await file.ReadAsync( 0x100, 4 ) ) == "NCSD" ||
					 file.Length > 104 && e.GetString( await file.ReadAsync( 0x100, 4 ) ) == "NCCH" ||
					 file.Length > await file.ReadInt32Async( 0 ) && e.GetString( await file.ReadAsync( 0x100 + MetadataReader.GetCIAContentOffset( file ), 4 ) ) == "NCCH" )
					return SystemType.ThreeDS;

				return SystemType.Unknown;
			}
		}
	}
}
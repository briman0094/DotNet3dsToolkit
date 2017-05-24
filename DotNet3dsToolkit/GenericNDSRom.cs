using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNet3dsToolkit.Properties;
using SkyEditor.Core.IO;
using SkyEditor.Core.Utilities;

//Implements IDetectableFileType
namespace DotNet3dsToolkit
{
	public class GenericNDSRom : GenericFile, IReportProgress, IIOProvider
	{
		public override string GetDefaultExtension()
		{
			return "*.nds";
		}

		public GenericNDSRom()
		{
			this.EnableInMemoryLoad = true;
			this.ResetWorkingDirectory();
		}

		public override async Task OpenFile( string filename, IIOProvider provider )
		{
			await base.OpenFile( filename, provider );
			await Task.Run( () => this.CurrentFilenameTable == this.GetFnt() );
			await Task.Run( async () => { this.CurrentFileAllocationTable = await this.GetFat(); } );
			this.CurrentArm9OverlayTable = this.ParseArm9OverlayTable();
			this.CurrentArm9OverlayTable = this.ParseArm7OverlayTable();
		}

		#region "Events"

		public event EventHandler<ProgressReportedEventArgs> UnpackProgress;
		public event EventHandler Completed;

		#endregion

		#region "Properties"

		//Credit to http://nocash.emubase.de/gbatek.htm#dscartridgesencryptionfirmware (As of Jan 1 2014) for research
		//Later moved to http://problemkaputt.de/gbatek.htm#dscartridgeheader
		public string GameTitle
		{
			get
			{
				ASCIIEncoding e = new ASCIIEncoding();
				return e.GetString( this.Read( 0, 12 ) ).Trim();
			}
			set
			{
				ASCIIEncoding e = new ASCIIEncoding();
				var buffer = e.GetBytes( value );
				for ( var count = 0; count <= 11; count++ )
				{
					this.Write( count, (byte) ( buffer.Length > count
													? buffer[ count ]
													: 0 ) );
				}
			}
		}

		public string GameCode
		{
			get
			{
				ASCIIEncoding e = new ASCIIEncoding();
				return e.GetString( this.Read( 12, 4 ) ).Trim();
			}
			set
			{
				ASCIIEncoding e = new ASCIIEncoding();
				var buffer = e.GetBytes( value );
				for ( var count = 0; count <= 3; count++ )
				{
					this.Write( 12 + count, (byte) ( buffer.Length > count
														 ? buffer[ count ]
														 : 0 ) );
				}
			}
		}

		private string MakerCode
		{
			get
			{
				ASCIIEncoding e = new ASCIIEncoding();
				return e.GetString( this.Read( 16, 2 ) ).Trim();
			}
			set
			{
				ASCIIEncoding e = new ASCIIEncoding();
				var buffer = e.GetBytes( value );
				for ( var count = 0; count <= 1; count++ )
				{
					this.Write( 16 + count, (byte) ( buffer.Length > count
														 ? buffer[ count ]
														 : 0 ) );
				}
			}
		}

		private byte UnitCode
		{
			get => this.Read( 0x12 );
			set => this.Write( 0x12, value );
		}

		private byte EncryptionSeedSelect
		{
			get => this.Read( 0x13 );
			set => this.Write( 0x13, value );
		}

		/// <summary>
		/// Gets or sets the capacity of the cartridge.  Cartridge size = 128KB * 2 ^ (DeviceCapacity)
		/// </summary>
		public byte DeviceCapacity
		{
			get => this.Read( 0x14 );
			set => this.Write( 0x14, value );
		}

		//Reserved: 8 bytes of 0

		//Region: 1 byte

		public byte RomVersion
		{
			get => this.Read( 0x1e );
			set => this.Write( 0x1e, value );
		}

		//Autostart: bit 2 skips menu

		private int Arm9RomOffset
		{
			get => BitConverter.ToInt32( this.Read( 0x20, 4 ), 0 );
			set => this.Write( 0x20, 4, BitConverter.GetBytes( value ) );
		}

		private int Arm9REntryAddress
		{
			get => BitConverter.ToInt32( this.Read( 0x24, 4 ), 0 );
			set => this.Write( 0x24, 4, BitConverter.GetBytes( value ) );
		}

		private int Arm9RamAddress
		{
			get => BitConverter.ToInt32( this.Read( 0x28, 4 ), 0 );
			set => this.Write( 0x28, 4, BitConverter.GetBytes( value ) );
		}

		private int Arm9Size
		{
			get => BitConverter.ToInt32( this.Read( 0x2c, 4 ), 0 );
			set => this.Write( 0x2c, 4, BitConverter.GetBytes( value ) );
		}

		private int Arm7RomOffset
		{
			get => BitConverter.ToInt32( this.Read( 0x30, 4 ), 0 );
			set => this.Write( 0x30, 4, BitConverter.GetBytes( value ) );
		}

		private int Arm7REntryAddress
		{
			get => BitConverter.ToInt32( this.Read( 0x34, 4 ), 0 );
			set => this.Write( 0x34, 4, BitConverter.GetBytes( value ) );
		}

		private int Arm7RamAddress
		{
			get => BitConverter.ToInt32( this.Read( 0x38, 4 ), 0 );
			set => this.Write( 0x38, 4, BitConverter.GetBytes( value ) );
		}

		private int Arm7Size
		{
			get => BitConverter.ToInt32( this.Read( 0x3c, 4 ), 0 );
			set => this.Write( 0x3c, 4, BitConverter.GetBytes( value ) );
		}

		private int FilenameTableOffset
		{
			get => BitConverter.ToInt32( this.Read( 0x40, 4 ), 0 );
			set => this.Write( 0x40, 4, BitConverter.GetBytes( value ) );
		}

		private int FilenameTableSize
		{
			get => BitConverter.ToInt32( this.Read( 0x44, 4 ), 0 );
			set => this.Write( 0x44, 4, BitConverter.GetBytes( value ) );
		}

		private int FileAllocationTableOffset
		{
			get => BitConverter.ToInt32( this.Read( 0x48, 4 ), 0 );
			set => this.Write( 0x48, 4, BitConverter.GetBytes( value ) );
		}

		private int FileAllocationTableSize
		{
			get => BitConverter.ToInt32( this.Read( 0x4c, 4 ), 0 );
			set => this.Write( 0x4c, 4, BitConverter.GetBytes( value ) );
		}

		private int FileArm9OverlayOffset
		{
			get => BitConverter.ToInt32( this.Read( 0x50, 4 ), 0 );
			set => this.Write( 0x50, 4, BitConverter.GetBytes( value ) );
		}

		private int FileArm9OverlaySize
		{
			get => BitConverter.ToInt32( this.Read( 0x54, 4 ), 0 );
			set => this.Write( 0x54, 4, BitConverter.GetBytes( value ) );
		}

		private int FileArm7OverlayOffset
		{
			get => BitConverter.ToInt32( this.Read( 0x58, 4 ), 0 );
			set => this.Write( 0x58, 4, BitConverter.GetBytes( value ) );
		}

		private int FileArm7OverlaySize
		{
			get => BitConverter.ToInt32( this.Read( 0x5c, 4 ), 0 );
			set => this.Write( 0x5c, 4, BitConverter.GetBytes( value ) );
		}

		//060h    4     Port 40001A4h setting for normal commands (usually 00586000h)
		//064h    4     Port 40001A4h setting for KEY1 commands   (usually 001808F8h)
		private int IconTitleOffset
		{
			get => BitConverter.ToInt32( this.Read( 0x68, 4 ), 0 );
			set => this.Write( 0x68, 4, BitConverter.GetBytes( value ) );
		}

		private int IconTitleLength => 0x840;
		//06Ch    2     Secure Area Checksum, CRC-16 of [ [20h]..7FFFh]
		//06Eh    2     Secure Area Loading Timeout (usually 051Eh)
		//070h    4     ARM9 Auto Load List RAM Address (?)
		//074h    4     ARM7 Auto Load List RAM Address (?)
		//078h    8     Secure Area Disable (by encrypted "NmMdOnly") (usually zero)
		//080h    4     Total Used ROM size (remaining/unused bytes usually FFh-padded)
		//084h    4     ROM Header Size (4000h)
		//088h    38h   Reserved (zero filled)
		//0C0h    9Ch   Nintendo Logo (compressed bitmap, same as in GBA Headers)
		//15Ch    2     Nintendo Logo Checksum, CRC-16 of [0C0h-15Bh], fixed CF56h
		//15Eh    2     Header Checksum, CRC-16 of [000h-15Dh]
		//160h    4     Debug rom_offset   (0=none) (8000h and up)       ;only if debug
		//164h    4     Debug size         (0=none) (max 3BFE00h)        ;version with
		//168h    4     Debug ram_address  (0=none) (2400000h..27BFE00h) ;SIO and 8MB
		//16Ch    4     Reserved (zero filled) (transferred, and stored, but not used)
		//170h    90h   Reserved (zero filled) (transferred, but not stored in RAM)

		/// <summary>
		/// The ROM's filename table
		/// </summary>
		private FilenameTable CurrentFilenameTable { get; set; }

		private List<FileAllocationEntry> CurrentFileAllocationTable { get; set; }

		private List<OverlayTableEntry> CurrentArm9OverlayTable { get; set; }

		private List<OverlayTableEntry> CurrentArm7OverlayTable { get; set; }

		#endregion

		#region "NitroRom Stuff"

		#region "Private Classes"

		private class OverlayTableEntry
		{
			private int OverlayID { get; }
			private int RamAddress { get; }
			private int RamSize { get; }
			private int BssSize { get; }
			private int StaticInitStart { get; }
			private int StaticInitEnd { get; }
			public int FileID { get; }

			public OverlayTableEntry( byte[] rawData )
			{
				this.OverlayID = BitConverter.ToInt32( rawData, 0 );
				this.RamAddress = BitConverter.ToInt32( rawData, 4 );
				this.RamSize = BitConverter.ToInt32( rawData, 8 );
				this.BssSize = BitConverter.ToInt32( rawData, 0xc );
				this.StaticInitStart = BitConverter.ToInt32( rawData, 0x10 );
				this.StaticInitEnd = BitConverter.ToInt32( rawData, 0x14 );
				this.FileID = BitConverter.ToInt32( rawData, 0x18 );
			}
		}

		private class FileAllocationEntry
		{
			public int Offset { get; }
			public int EndAddress { get; }

			public FileAllocationEntry( int offset, int endAddress )
			{
				this.Offset = offset;
				this.EndAddress = endAddress;
			}
		}

		private class DirectoryMainTable
		{
			public uint SubTableOffset { get; }
			public ushort FirstSubTableFileID { get; }

			/// <summary>
			/// If this is the root directory, will contain the number of child directories.
			/// Otherwise, the ID of the parent directory.
			/// </summary>
			/// <returns></returns>
			public ushort ParentDir { get; }

			public DirectoryMainTable( byte[] rawData )
			{
				this.SubTableOffset = BitConverter.ToUInt32( rawData, 0 );
				this.FirstSubTableFileID = BitConverter.ToUInt16( rawData, 4 );
				this.ParentDir = BitConverter.ToUInt16( rawData, 6 );
			}
		}

		private class FntSubTable
		{
			public byte Length { get; set; }
			public string Name { get; set; }

			public ushort SubDirectoryID { get; set; }

			//Only for directories
			public ushort ParentFileID { get; set; }
		}

		private class FilenameTable
		{
			public string Name { get; set; }
			public int FileIndex { get; set; }

			public bool IsDirectory => this.FileIndex < 0;

			public List<FilenameTable> Children { get; }

			public override string ToString()
			{
				return this.Name;
			}

			public FilenameTable()
			{
				this.FileIndex = -1;
				this.Children = new List<FilenameTable>();
			}
		}

		#endregion

		private List<OverlayTableEntry> ParseArm9OverlayTable()
		{
			List<OverlayTableEntry> @out = new List<OverlayTableEntry>();
			for ( var count = this.FileArm9OverlayOffset; count <= this.FileArm9OverlayOffset + this.FileArm9OverlaySize - 1; count += 32 )
			{
				@out.Add( new OverlayTableEntry( this.Read( count, 32 ) ) );
			}
			return @out;
		}

		private List<OverlayTableEntry> ParseArm7OverlayTable()
		{
			List<OverlayTableEntry> @out = new List<OverlayTableEntry>();
			for ( var count = this.FileArm7OverlayOffset; count <= this.FileArm7OverlayOffset + this.FileArm7OverlaySize - 1; count += 32 )
			{
				@out.Add( new OverlayTableEntry( this.Read( count, 32 ) ) );
			}
			return @out;
		}

		private async Task<List<FileAllocationEntry>> GetFat()
		{
			ConcurrentDictionary<int, FileAllocationEntry> @out = new ConcurrentDictionary<int, FileAllocationEntry>();
			AsyncFor f = new AsyncFor();
			await f.RunFor( async count => {
				var offset = this.FileAllocationTableOffset + count * 8;
				FileAllocationEntry entry = new FileAllocationEntry( BitConverter.ToInt32( await this.ReadAsync( offset, 4 ), 0 ), BitConverter.ToInt32( await this.ReadAsync( offset + 4, 4 ), 0 ) );
				if ( entry.Offset != 0 )
				{
					@out[ count ] = entry;
				}
			}, 0, this.FileAllocationTableSize / 8 - 1 );
			return @out.Keys.OrderBy( x => x ).Select( x => @out[ x ] ).ToList();
		}

		private FilenameTable GetFnt()
		{
			DirectoryMainTable root = new DirectoryMainTable( this.Read( this.FilenameTableOffset, 8 ) );
			List<DirectoryMainTable> rootDirectories = new List<DirectoryMainTable>();
			//In the root entry, ParentDir means number of directories
			for ( var count = 8; count <= root.SubTableOffset - 1; count += 8 )
			{
				rootDirectories.Add( new DirectoryMainTable( this.Read( this.FilenameTableOffset + count, 8 ) ) );
			}
			//Todo: read the relationship between directories and files
			FilenameTable @out = new FilenameTable { Name = "data" };
			this.BuildFnt( @out, root, rootDirectories );
			return @out;
		}

		private void BuildFnt( FilenameTable parentFnt, DirectoryMainTable root, List<DirectoryMainTable> directories )
		{
			foreach ( var item in this.ReadFntSubTable( (int) root.SubTableOffset, root.FirstSubTableFileID ) )
			{
				FilenameTable child = new FilenameTable { Name = item.Name };
				parentFnt.Children.Add( child );
				if ( item.Length > 128 )
				{
					this.BuildFnt( child, directories[ ( item.SubDirectoryID & 0xfff ) - 1 ], directories );
				}
				else
				{
					child.FileIndex = item.ParentFileID;
				}
			}
		}

		private List<FntSubTable> ReadFntSubTable( int rootSubTableOffset, int parentFileID )
		{
			List<FntSubTable> subTables = new List<FntSubTable>();
			var offset = rootSubTableOffset + this.FilenameTableOffset;
			int length = this.Read( offset );
			while ( length > 0 )
			{
				if ( length > 128 )
				{
					//Then it's a sub directory
					//Read the string
					byte[] buffer = this.Read( offset + 1, length - 128 );
					var s = ( new ASCIIEncoding() ).GetString( buffer );
					//Read sub directory ID
					ushort subDirID = this.ReadUInt16( offset + 1 + length - 128 );
					//Add the result to the list
					subTables.Add( new FntSubTable {
						Length = (byte) length,
						Name = s,
						SubDirectoryID = subDirID
					} );
					//Increment the offset
					offset += length - 128 + 1 + 2;
				}
				else if ( length < 128 )
				{
					//Then it's a file
					//Read the string
					byte[] buffer = this.Read( offset + 1, length );
					var s = ( new ASCIIEncoding() ).GetString( buffer );
					//Add the result to the list
					subTables.Add( new FntSubTable {
						Length = (byte) length,
						Name = s,
						ParentFileID = (ushort) parentFileID
					} );
					parentFileID += 1;
					//Increment the offset
					offset += length + 1;
				}
				else
				{
					//Reserved.  I'm not sure what to do here.
					throw new NotSupportedException( "Subtable length of 0x80 not supported." );
				}

				length = this.Read( offset );
			}
			return subTables;
		}

		/// <summary>
		/// Extracts the files contained within the ROMs.
		/// Extractions either run synchronously or asynchrounously, depending on the value of IsThreadSafe.
		/// </summary>
		/// <param name="targetDir">Directory to store the extracted files.</param>
		/// <param name="provider"></param>
		public async Task Unpack( string targetDir, IIOProvider provider )
		{
			var fat = this.CurrentFileAllocationTable;

			//Set up extraction dependencies
			this.CurrentExtractProgress = 0;
			this.CurrentExtractMax = fat.Count;
			this.ExtractionTasks = new ConcurrentBag<Task>();

			//Ensure directory exists
			if ( !provider.DirectoryExists( targetDir ) )
			{
				provider.CreateDirectory( targetDir );
			}

			//Start extracting
			//-Header
			var headerTask = Task.Run( () => provider.WriteAllBytes( Path.Combine( targetDir, "header.bin" ), this.Read( 0, 0x200 ) ) );
			if ( this.IsThreadSafe )
			{
				this.ExtractionTasks.Add( headerTask );
			}
			else
			{
				await headerTask;
			}

			//-Arm9
			var arm9Task = Task.Run( () => {
				List<byte> arm9Buffer = new List<byte>();
				arm9Buffer.AddRange( this.Read( this.Arm9RomOffset, this.Arm9Size ) );

				//Write an additional 0xC bytes if the next 4 equal: 21 06 C0 DE
				uint footer = this.ReadUInt32( this.Arm9RomOffset + this.Arm9Size );
				if ( footer == 0xdec00621 )
				{
					arm9Buffer.AddRange( this.Read( this.Arm9RomOffset + this.Arm9Size, 0xc ) );
				}

				provider.WriteAllBytes( Path.Combine( targetDir, "arm9.bin" ), arm9Buffer.ToArray() );
			} );
			if ( this.IsThreadSafe )
			{
				this.ExtractionTasks.Add( arm9Task );
			}
			else
			{
				await arm9Task;
			}

			//-Arm7
			var arm7Task = Task.Run( () => provider.WriteAllBytes( Path.Combine( targetDir, "arm7.bin" ), this.Read( this.Arm7RomOffset, this.Arm7Size ) ) );
			if ( this.IsThreadSafe )
			{
				this.ExtractionTasks.Add( arm7Task );
			}
			else
			{
				await arm7Task;
			}

			//-Arm9 overlay table (y9.bin)
			var y9Task = Task.Run( () => provider.WriteAllBytes( Path.Combine( targetDir, "y9.bin" ), this.Read( this.FileArm9OverlayOffset, this.FileArm9OverlaySize ) ) );

			if ( this.IsThreadSafe )
			{
				this.ExtractionTasks.Add( y9Task );
			}
			else
			{
				await y9Task;
			}

			//-Extract arm7 overlay table (y7.bin)
			var y7Task = Task.Run( () => provider.WriteAllBytes( Path.Combine( targetDir, "y7.bin" ), this.Read( this.FileArm7OverlayOffset, this.FileArm7OverlaySize ) ) );

			if ( this.IsThreadSafe )
			{
				this.ExtractionTasks.Add( y7Task );
			}
			else
			{
				await y7Task;
			}
			//-Extract overlays
			var overlay9 = this.ExtractOverlay( fat, this.ParseArm9OverlayTable(), Path.Combine( targetDir, "overlay" ), provider );

			if ( this.IsThreadSafe )
			{
				this.ExtractionTasks.Add( overlay9 );
			}
			else
			{
				await overlay9;
			}

			var overlay7 = this.ExtractOverlay( fat, this.ParseArm7OverlayTable(), Path.Combine( targetDir, "overlay7" ), provider );

			if ( this.IsThreadSafe )
			{
				this.ExtractionTasks.Add( overlay7 );
			}
			else
			{
				await overlay7;
			}
			//-Extract icon (banner.bin)
			var iconTask = Task.Run( () => {
				//0 means none
				if ( this.IconTitleOffset > 0 )
				{
					provider.WriteAllBytes( Path.Combine( targetDir, "banner.bin" ), this.Read( this.IconTitleOffset, this.IconTitleLength ) );
				}
			} );

			if ( this.IsThreadSafe )
			{
				this.ExtractionTasks.Add( iconTask );
			}
			else
			{
				await iconTask;
			}

			//- Extract files
			var filesExtraction = this.ExtractFiles( fat, this.CurrentFilenameTable, targetDir, provider );
			if ( this.IsThreadSafe )
			{
				this.ExtractionTasks.Add( filesExtraction );
			}
			else
			{
				await filesExtraction;
			}

			//Wait for everything to finish
			await Task.WhenAll( this.ExtractionTasks );
		}

		/// <summary>
		/// Extracts contained files if the file is thread safe, otherwise, extracts files one at a time.
		/// </summary>
		/// <param name="fat"></param>
		/// <param name="root"></param>
		/// <param name="targetDir"></param>
		/// <param name="provider"></param>
		/// <returns></returns>
		private async Task ExtractFiles( IReadOnlyList<FileAllocationEntry> fat, FilenameTable root, string targetDir, IIOProvider provider )
		{
			string dest = Path.Combine( targetDir, root.Name );
			AsyncFor f = new AsyncFor {
				RunSynchronously = !this.IsThreadSafe,
				BatchSize = root.Children.Count
			};

			async Task ExtractFile( FilenameTable item )
			{
				if ( item.IsDirectory )
				{
					await this.ExtractFiles( fat, item, dest, provider );
				}
				else
				{
					var entry = fat[ item.FileIndex ];
					var parentDir = Path.GetDirectoryName( Path.Combine( dest, item.Name ) );
					if ( !provider.DirectoryExists( parentDir ) )
					{
						provider.CreateDirectory( parentDir );
					}
					provider.WriteAllBytes( Path.Combine( dest, item.Name ), await this.ReadAsync( entry.Offset, entry.EndAddress - entry.Offset ) );
					Interlocked.Increment( ref this.extractProgress );
				}
			}

			await f.RunForEach( root.Children, ExtractFile );
		}

		private async Task ExtractOverlay( IReadOnlyList<FileAllocationEntry> fat, IReadOnlyCollection<OverlayTableEntry> overlayTable, string targetDir, IIOProvider provider )
		{
			if ( overlayTable.Count > 0 && !provider.DirectoryExists( targetDir ) )
			{
				provider.CreateDirectory( targetDir );
			}
			AsyncFor f = new AsyncFor {
				RunSynchronously = !this.IsThreadSafe,
				BatchSize = overlayTable.Count
			};
			await f.RunForEach( overlayTable, item => {
				var dest = Path.Combine( targetDir, "overlay_" + item.FileID.ToString().PadLeft( 4, '0' ) + ".bin" );
				var entry = fat[ item.FileID ];
				provider.WriteAllBytes( dest, this.Read( entry.Offset, entry.EndAddress - entry.Offset ) );
			} );
		}

		/// <summary>
		/// Gets or sets the total number of files extracted in the current unpacking process.
		/// </summary>
		/// <returns></returns>
		private int CurrentExtractProgress
		{
			get => this.extractProgress;
			set
			{
				this.extractProgress = value;
				this.UnpackProgress?.Invoke( this, new ProgressReportedEventArgs {
					IsIndeterminate = false,
					Progress = this.ExtractionProgress,
					Message = this.Message
				} );
			}
		}

		private int extractProgress;

		/// <summary>
		/// Gets or sets the total number of files in the current unpacking process.
		/// </summary>
		/// <returns></returns>
		private int CurrentExtractMax { get; set; }

		/// <summary>
		/// Currently running tasks that are part of the unpacking process.
		/// </summary>
		/// <returns></returns>
		private ConcurrentBag<Task> ExtractionTasks { get; set; }

		/// <summary>
		/// The progress of the current unpacking process.
		/// </summary>
		/// <returns></returns>
		public float ExtractionProgress => this.CurrentExtractProgress / (float) this.CurrentExtractMax;

		float IReportProgress.Progress => this.ExtractionProgress;

		private bool IsExtractionIndeterminate => false;

		bool IReportProgress.IsIndeterminate => this.IsExtractionIndeterminate;

		public bool IsCompleted => this.CurrentExtractProgress == 1;

		public event EventHandler<ProgressReportedEventArgs> ProgressChanged;

		public string Message => this.IsCompleted
									 ? Language.Complete
									 : Language.LoadingUnpacking;

		#endregion

		public virtual async Task<bool> IsFileOfType( GenericFile file )
		{
			// Implements IDetectableFileType.IsOfType
			return file.Length > 0x15d && await file.ReadAsync( 0x15c ) == 0x56 && await file.ReadAsync( 0x15d ) == 0xcf;
		}

		#region "IO Provider Implementation"

		public Stream OpenFileWriteOnly( string filename )
		{
			throw new NotSupportedException();
		}

		public string WorkingDirectory
		{
			get => this.workingDirectory;
			set
			{
				if ( Path.IsPathRooted( value ) )
				{
					this.workingDirectory = value;
				}
				else
				{
					foreach ( var part in value.Replace( '\\', '/' ).Split( '/' ) )
					{
						if ( part == "." )
						{
							// Do nothing
						}
						else if ( part == ".." )
						{
							this.workingDirectory = Path.GetDirectoryName( this.workingDirectory );
						}
						else
						{
							this.workingDirectory = Path.Combine( this.workingDirectory, part );
						}
					}
				}
			}
		}

		private string workingDirectory;

		public void ResetWorkingDirectory()
		{
			this.WorkingDirectory = "/";
		}

		public long GetFileLength( string filename )
		{
			var entry = this.GetFatEntry( filename );
			return entry.EndAddress - entry.Offset;
		}

		public bool FileExists( string filename )
		{
			return this.GetFatEntry( filename, false ) != null;
		}

		public bool DirectoryExists( string path )
		{
			throw new NotSupportedException();
		}

		public void CreateDirectory( string path )
		{
			throw new NotSupportedException();
		}

		public string[] GetFiles( string path, string searchPattern, bool topDirectoryOnly )
		{
			throw new NotSupportedException();
		}

		public string[] GetDirectories( string path, bool topDirectoryOnly )
		{
			throw new NotSupportedException();
		}

		public byte[] ReadAllBytes( string filename )
		{
			var entry = this.GetFatEntry( filename );
			return this.Read( entry.Offset, entry.EndAddress - entry.Offset );
		}

		public string ReadAllText( string filename )
		{
			throw new NotSupportedException();
		}

		public void WriteAllBytes( string filename, byte[] data )
		{
			throw new NotSupportedException();
		}

		public void WriteAllText( string filename, string data )
		{
			throw new NotSupportedException();
		}

		public void CopyFile( string sourceFilename, string destinationFilename )
		{
			throw new NotSupportedException();
		}

		public void DeleteFile( string filename )
		{
			throw new NotSupportedException();
		}

		public void DeleteDirectory( string path )
		{
			throw new NotSupportedException();
		}

		public string GetTempFilename()
		{
			throw new NotSupportedException();
		}

		public string GetTempDirectory()
		{
			throw new NotSupportedException();
		}

		public Stream OpenFile( string filename )
		{
			throw new NotSupportedException();
		}

		public Stream OpenFileReadOnly( string filename )
		{
			throw new NotSupportedException();
		}

		protected string FixPath( string pathToFix )
		{
			var @fixed = pathToFix.Replace( "\\", "/" );

			//Apply working directory
			return Path.IsPathRooted( pathToFix )
					   ? @fixed
					   : Path.Combine( this.WorkingDirectory, @fixed );
		}

		private FileAllocationEntry GetFatEntry( string path, bool throwIfNotFound = true )
		{
			var fixedPath = this.FixPath( path );
			var parts = fixedPath.Split( '/' );
			FilenameTable currentEntry = null;

			if ( parts.Length < 2 )
			{
				throw new ArgumentException( string.Format( Language.ErrorInvalidPathFormat, path ), nameof(path) );
			}

			int index;
			switch ( parts[ 1 ].ToLower() )
			{
				case "data":
					currentEntry = this.CurrentFilenameTable;
					break;
				case "overlay":
					if ( int.TryParse( parts[ 2 ].ToLower().Substring( 8, 4 ), out index ) )
					{
						return this.CurrentFileAllocationTable[ this.CurrentArm9OverlayTable[ index ].FileID ];
					}
					break;
				case "overlay7":
					if ( int.TryParse( parts[ 2 ].ToLower().Substring( 8, 4 ), out index ) )
					{
						return this.CurrentFileAllocationTable[ this.CurrentArm7OverlayTable[ index ].FileID ];
					}
					break;
				case "arm7.bin":
					return new FileAllocationEntry( this.Arm7RomOffset, this.Arm7RomOffset + this.Arm7Size );
				case "arm9.bin":
					//Write an additional 0xC bytes if the next 4 equal: 21 06 C0 DE
					uint footer = this.ReadUInt32( this.Arm9RomOffset + this.Arm9Size );
					return footer == 0xdec00621
							   ? new FileAllocationEntry( this.Arm9RomOffset, this.Arm9RomOffset + this.Arm9Size + 0xc )
							   : new FileAllocationEntry( this.Arm9RomOffset, this.Arm9RomOffset + this.Arm9Size );
				case "header.bin":
					return new FileAllocationEntry( 0, 0x200 );
				case "icon.bin":
					return new FileAllocationEntry( this.IconTitleOffset, this.IconTitleOffset + this.IconTitleLength );
				case "y7.bin":
					return new FileAllocationEntry( this.FileArm7OverlayOffset, this.FileArm7OverlayOffset + this.FileArm7OverlaySize );
				case "y9.bin":
					return new FileAllocationEntry( this.FileArm9OverlayOffset, this.FileArm9OverlayOffset + this.FileArm9OverlaySize );
			}

			if ( currentEntry != null )
			{
				for ( var count = 2; count <= parts.Length - 1; count++ )
				{
					var currentCount = count;
					currentEntry = currentEntry?.Children.FirstOrDefault( x => x.Name.ToLower() == parts[ currentCount ] );
				}
			}

			if ( currentEntry == null )
			{
				if ( throwIfNotFound )
					throw new FileNotFoundException( Language.ErrorROMFileNotFound, path );

				return null;
			}

			return this.CurrentFileAllocationTable[ currentEntry.FileIndex ];
		}

		#endregion
	}
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotNet3dsToolkit.Properties;
using SkyEditor.Core.IO;
using SkyEditor.Core.Utilities;

namespace DotNet3dsToolkit
{
	public class Converter : IDisposable, IReportProgress
	{
		private static readonly int[] PartitionNumbers = {
			0,
			1,
			2,
			6,
			7
		};

		public event ConsoleOutputReceivedEventHandler ConsoleOutputReceived;

		public delegate void ConsoleOutputReceivedEventHandler( object sender, DataReceivedEventArgs e );

		/// <summary>
		/// Whether or not to forward console output of child processes to the current process.
		/// </summary>
		/// <returns></returns>
		public bool OutputConsoleOutput { get; set; }

		private async Task RunProgram( string program, string arguments )
		{
			bool handlersRegistered = false;

			Process p = new Process {
				StartInfo = {
					FileName = program,
					WorkingDirectory = Path.GetDirectoryName( program ),
					Arguments = arguments,
					WindowStyle = ProcessWindowStyle.Hidden,
					CreateNoWindow = true,
					RedirectStandardOutput = this.OutputConsoleOutput
				}
			};
			p.StartInfo.RedirectStandardError = p.StartInfo.RedirectStandardOutput;
			p.StartInfo.UseShellExecute = false;

			if ( p.StartInfo.RedirectStandardOutput )
			{
				p.OutputDataReceived += this.OnInputRecieved;
				p.ErrorDataReceived += this.OnInputRecieved;
				handlersRegistered = true;
			}

			p.Start();

			if ( this.OutputConsoleOutput )
			{
				p.BeginOutputReadLine();
				p.BeginErrorReadLine();
			}

			await Task.Run( () => p.WaitForExit() );

			if ( handlersRegistered )
			{
				p.OutputDataReceived -= this.OnInputRecieved;
				p.ErrorDataReceived -= this.OnInputRecieved;
			}
		}

		private void OnInputRecieved( object sender, DataReceivedEventArgs e )
		{
			if ( sender is Process && !string.IsNullOrEmpty( e.Data ) )
			{
				Console.Write( $"[{Path.GetFileNameWithoutExtension( ( (Process) sender ).StartInfo.FileName )}] $" );
				Console.WriteLine( e.Data );
				this.ConsoleOutputReceived?.Invoke( this, e );
			}
		}

		public event EventHandler<ProgressReportedEventArgs> ProgressChanged;
		public event EventHandler Completed;

		#region $"Tool Management"

		private string ToolDirectory { get; set; }
		private string Path3Dstool { get; set; }
		private string Path3DSBuilder { get; set; }
		private string PathMakeROM { get; set; }
		private string PathCtrtool { get; set; }
		private string PathNDSTool { get; set; }

		public float Progress
		{
			get => this.progress;
			private set
			{
				if ( Math.Abs( value - this.progress ) > float.Epsilon )
				{
					this.progress = value;
					this.ProgressChanged?.Invoke( this, new ProgressReportedEventArgs {
						Progress = this.Progress,
						IsIndeterminate = this.IsIndeterminate,
						Message = this.Message
					} );
				}
			}
		}

		private float progress;

		public string Message
		{
			get => this.message;
			protected set
			{
				if ( value != this.message )
				{
					this.message = value;
					this.ProgressChanged?.Invoke( this, new ProgressReportedEventArgs {
						Progress = this.Progress,
						IsIndeterminate = this.IsIndeterminate,
						Message = this.Message
					} );
				}
			}
		}

		private string message;

		public bool IsIndeterminate
		{
			get => this.isIndeterminate;
			protected set
			{
				if ( value != this.isIndeterminate )
				{
					this.isIndeterminate = value;
					this.ProgressChanged?.Invoke( this, new ProgressReportedEventArgs {
						Progress = this.Progress,
						IsIndeterminate = this.IsIndeterminate,
						Message = this.Message
					} );
				}
			}
		}

		private bool isIndeterminate;

		public bool IsCompleted
		{
			get => this.isCompleted;
			protected set
			{
				if ( value != this.isCompleted )
				{
					this.isCompleted = value;

					if ( value )
					{
						this.Progress = 1;
						this.IsIndeterminate = false;
						this.Completed?.Invoke( this, new EventArgs() );
					}
				}
			}
		}

		private bool isCompleted;

		private void ResetToolDirectory()
		{
			this.ToolDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "DotNet3DSToolkit-" + Guid.NewGuid() );
			if ( Directory.Exists( this.ToolDirectory ) )
			{
				this.ResetToolDirectory();
			}
			else
			{
				Directory.CreateDirectory( this.ToolDirectory );
			}
		}

		/// <summary>
		/// Copies 3dstool.exe to the the tools directory if it's not already there.
		/// </summary>
		private void Copy3DSTool()
		{
			if ( string.IsNullOrEmpty( this.ToolDirectory ) )
			{
				this.ResetToolDirectory();
			}

			string exePath = Path.Combine( this.ToolDirectory, "3dstool.exe" );
			string txtPath = Path.Combine( this.ToolDirectory, "ignore_3dstool.txt" );

			if ( !File.Exists( exePath ) )
			{
				File.WriteAllBytes( exePath, Resources._3dstool );
				this.Path3Dstool = exePath;
			}

			if ( !File.Exists( txtPath ) )
			{
				File.WriteAllText( Path.Combine( this.ToolDirectory, "ignore_3dstool.txt" ), Resources.ignore_3dstool );
			}
		}

		private void Copy3DSBuilder()
		{
			if ( string.IsNullOrEmpty( this.ToolDirectory ) )
			{
				this.ResetToolDirectory();
			}

			string exePath = Path.Combine( this.ToolDirectory, "3DS Builder.exe" );
			if ( !File.Exists( exePath ) )
			{
				File.WriteAllBytes( exePath, Resources._3DS_Builder );
				this.Path3DSBuilder = exePath;
			}
		}

		private void CopyCtrTool()
		{
			if ( string.IsNullOrEmpty( this.ToolDirectory ) )
			{
				this.ResetToolDirectory();
			}

			string exePath = Path.Combine( this.ToolDirectory, "ctrtool.exe" );
			if ( !File.Exists( exePath ) )
			{
				File.WriteAllBytes( exePath, Resources.ctrtool );
				this.PathCtrtool = exePath;
			}
		}

		private void CopyMakeRom()
		{
			if ( string.IsNullOrEmpty( this.ToolDirectory ) )
			{
				this.ResetToolDirectory();
			}

			string exePath = Path.Combine( this.ToolDirectory, "makerom.exe" );
			if ( !File.Exists( exePath ) )
			{
				File.WriteAllBytes( exePath, Resources.makerom );
				this.PathMakeROM = exePath;
			}
		}

		private void CopyNDSTool()
		{
			if ( string.IsNullOrEmpty( this.ToolDirectory ) )
			{
				this.ResetToolDirectory();
			}

			string exePath = Path.Combine( this.ToolDirectory, "ndstool.exe" );
			if ( !File.Exists( exePath ) )
			{
				File.WriteAllBytes( exePath, Resources.ndstool );
				this.PathNDSTool = exePath;
			}
		}

		private void DeleteTools()
		{
			if ( Directory.Exists( this.ToolDirectory ) )
			{
				Directory.Delete( this.ToolDirectory, true );
			}
		}

		#endregion

		#region $"Extraction"

		public void ExtractPrivateHeader( string sourceCci, string outputFile )
		{
			string onlineHeaderBinPath = outputFile;
			using ( FileStream f = new FileStream( sourceCci, FileMode.Open, FileAccess.Read ) )
			{
				byte[] buffer = new byte[ 0x2e00 + 1 ];
				f.Seek( 0x1200, SeekOrigin.Begin );
				f.Read( buffer, 0, 0x2e00 );
				File.WriteAllBytes( onlineHeaderBinPath, buffer );
			}
		}

		private async Task ExtractCciPartitions( ExtractionOptions options )
		{
			string headerNcchPath = Path.Combine( options.DestinationDirectory, options.RootHeaderName );
			await this.RunProgram( this.Path3Dstool, $"-xtf 3ds \"{options.SourceRom}\" --header \"{headerNcchPath}\" -0 DecryptedPartition0.bin -1 DecryptedPartition1.bin -2 DecryptedPartition2.bin -6 DecryptedPartition6.bin -7 DecryptedPartition7.bin" );
		}

		private async Task ExtractCiaPartitions( ExtractionOptions options )
		{
			await this.RunProgram( this.PathCtrtool, $"--content=Partition \"{options.SourceRom}\"" );

			Regex partitionRegex = new Regex( $"Partition\\.000([0-9])\\.[0-9]{8}" );
			const string replace = "DecryptedPartition$1.bin";
			foreach ( string item in Directory.GetFiles( this.ToolDirectory )
											  .Where( item => partitionRegex.IsMatch( item ) ) )
			{
				File.Move( item, partitionRegex.Replace( item, replace ) );
			}
		}

		private async Task ExtractPartition0( ExtractionOptions options, string partitionFilename, bool ctrTool )
		{
			//Extract partitions
			string exheaderPath = Path.Combine( options.DestinationDirectory, options.ExheaderName );
			string headerPath = Path.Combine( options.DestinationDirectory, options.Partition0HeaderName );
			string logoPath = Path.Combine( options.DestinationDirectory, options.LogoLZName );
			string plainPath = Path.Combine( options.DestinationDirectory, options.PlainRGNName );
			await this.RunProgram( this.Path3Dstool, $"-xtf cxi \"{partitionFilename}\" --header \"{headerPath}\" --exh \"{exheaderPath}\" --exefs DecryptedExeFS.bin --romfs DecryptedRomFS.bin --logo \"{logoPath}\" --plain \"{plainPath}\"" );

			//Extract romfs and exefs
			string romfsDir = Path.Combine( options.DestinationDirectory, options.RomFSDirName );
			string exefsDir = Path.Combine( options.DestinationDirectory, options.ExeFSDirName );
			string exefsHeaderPath = Path.Combine( options.DestinationDirectory, options.ExeFSHeaderName );
			List<Task> tasks = new List<Task>();

			//- romfs
			if ( ctrTool )
			{
				await this.RunProgram( this.PathCtrtool, $"-t romfs --romfsdir \"{romfsDir}\" DecryptedRomFS.bin" );
			}
			else
			{
				tasks.Add( this.RunProgram( this.Path3Dstool, $"-xtf romfs DecryptedRomFS.bin --romfs-dir \"{romfsDir}\"" ) );
			}

			//- exefs
			string exefsExtractionOptions;
			//If options.DecompressCodeBin Then
			exefsExtractionOptions = "-xutf";
			//Else
			//    exefsExtractionOptions = $"-xtf"
			//End If

			if ( ctrTool )
			{
				await this.RunProgram( this.PathCtrtool, $"-t exefs --exefsdir=\"{exefsDir}\" DecryptedExeFS.bin --decompresscode" );
			}
			else
			{
				tasks.Add( Task.Run( async () => {
					//- exefs
					await this.RunProgram( this.Path3Dstool, $"{exefsExtractionOptions} exefs DecryptedExeFS.bin --exefs-dir \"{exefsDir}\" --header \"{exefsHeaderPath}\"" );

					File.Move( Path.Combine( options.DestinationDirectory, options.ExeFSDirName, "banner.bnr" ), Path.Combine( options.DestinationDirectory, options.ExeFSDirName, "banner.bin" ) );
					File.Move( Path.Combine( options.DestinationDirectory, options.ExeFSDirName, "icon.icn" ), Path.Combine( options.DestinationDirectory, options.ExeFSDirName, "icon.bin" ) );

					//- banner
					await this.RunProgram( this.Path3Dstool, $"-x -t banner -f \"{Path.Combine( options.DestinationDirectory, options.ExeFSDirName, "banner.bin" )}\" --banner-dir \"{Path.Combine( options.DestinationDirectory, "ExtractedBanner" )}\"" );

					File.Move( Path.Combine( options.DestinationDirectory, "ExtractedBanner", "banner0.bcmdl" ), Path.Combine( options.DestinationDirectory, "ExtractedBanner", "banner.cgfx" ) );
				} ) );
			}

			//Cleanup while we're waiting
			File.Delete( Path.Combine( this.ToolDirectory, "DecryptedPartition0.bin" ) );

			//Wait for all extractions
			await Task.WhenAll( tasks );

			//Cleanup the rest
			File.Delete( Path.Combine( this.ToolDirectory, "DecryptedRomFS.bin" ) );
			File.Delete( Path.Combine( this.ToolDirectory, "DecryptedExeFS.bin" ) );
		}

		private async Task ExtractPartition1( ExtractionOptions options )
		{
			if ( File.Exists( Path.Combine( this.ToolDirectory, "DecryptedPartition1.bin" ) ) )
			{
				//Extract
				string headerPath = Path.Combine( options.DestinationDirectory, options.Partition1HeaderName );
				string extractedPath = Path.Combine( options.DestinationDirectory, options.ExtractedManualDirName );
				await this.RunProgram( this.Path3Dstool, $"-xtf cfa DecryptedPartition1.bin --header \"{headerPath}\" --romfs DecryptedManual.bin" );
				await this.RunProgram( this.Path3Dstool, $"-xtf romfs DecryptedManual.bin --romfs-dir \"{extractedPath}\"" );

				//Cleanup
				File.Delete( Path.Combine( this.ToolDirectory, "DecryptedPartition1.bin" ) );
				File.Delete( Path.Combine( this.ToolDirectory, "DecryptedManual.bin" ) );
			}
		}

		private async Task ExtractPartition2( ExtractionOptions options )
		{
			if ( File.Exists( Path.Combine( this.ToolDirectory, "DecryptedPartition2.bin" ) ) )
			{
				//Extract
				string headerPath = Path.Combine( options.DestinationDirectory, options.Partition2HeaderName );
				string extractedPath = Path.Combine( options.DestinationDirectory, options.ExtractedDownloadPlayDirName );
				await this.RunProgram( this.Path3Dstool, $"-xtf cfa DecryptedPartition2.bin --header \"{headerPath}\" --romfs DecryptedDownloadPlay.bin" );
				await this.RunProgram( this.Path3Dstool, $"-xtf romfs DecryptedDownloadPlay.bin --romfs-dir \"{extractedPath}\"" );

				//Cleanup
				File.Delete( Path.Combine( this.ToolDirectory, "DecryptedPartition2.bin" ) );
				File.Delete( Path.Combine( this.ToolDirectory, "DecryptedDownloadPlay.bin" ) );
			}
		}

		private async Task ExtractPartition6( ExtractionOptions options )
		{
			if ( File.Exists( Path.Combine( this.ToolDirectory, "DecryptedPartition6.bin" ) ) )
			{
				//Extract
				string headerPath = Path.Combine( options.DestinationDirectory, options.Partition6HeaderName );
				string extractedPath = Path.Combine( options.DestinationDirectory, options.N3DSUpdateDirName );
				await this.RunProgram( this.Path3Dstool, $"-xtf cfa DecryptedPartition6.bin --header \"{headerPath}\" --romfs DecryptedN3DSUpdate.bin" );
				await this.RunProgram( this.Path3Dstool, $"-xtf romfs DecryptedN3DSUpdate.bin --romfs-dir \"{extractedPath}\"" );

				//Cleanup
				File.Delete( Path.Combine( this.ToolDirectory, "DecryptedPartition6.bin" ) );
				File.Delete( Path.Combine( this.ToolDirectory, "DecryptedN3DSUpdate.bin" ) );
			}
		}

		private async Task ExtractPartition7( ExtractionOptions options )
		{
			if ( File.Exists( Path.Combine( this.ToolDirectory, "DecryptedPartition7.bin" ) ) )
			{
				//Extract
				string headerPath = Path.Combine( options.DestinationDirectory, options.Partition7HeaderName );
				string extractedPath = Path.Combine( options.DestinationDirectory, options.O3DSUpdateDirName );
				await this.RunProgram( this.Path3Dstool, $"-xtf cfa DecryptedPartition7.bin --header \"{headerPath}\" --romfs DecryptedO3DSUpdate.bin" );
				await this.RunProgram( this.Path3Dstool, $"-xtf romfs DecryptedO3DSUpdate.bin --romfs-dir \"{extractedPath}\"" );

				//Cleanup
				File.Delete( Path.Combine( this.ToolDirectory, "DecryptedPartition7.bin" ) );
				File.Delete( Path.Combine( this.ToolDirectory, "DecryptedO3DSUpdate.bin" ) );
			}
		}

		#endregion

		#region $"Building Parts"

		private void UpdateExheader( BuildOptions options, bool isCia )
		{
			using ( FileStream f = new FileStream( Path.Combine( options.SourceDirectory, options.ExheaderName ), FileMode.Open, FileAccess.ReadWrite ) )
			{
				f.Seek( 0xd, SeekOrigin.Begin );
				int sciD = f.ReadByte();

				if ( options.CompressCodeBin )
				{
					sciD = sciD | 1;
					//We want to set bit 1 to 1 to force using a compressed code.bin
				}
				else
				{
					sciD = sciD & 0xfe;
					//We want to set bit 1 to 0 to avoid using a compressed code.bin
				}

				if ( isCia )
				{
					sciD = sciD | 2;
					//Set bit 2 to 1
				}
				else
				{
					sciD = sciD & 0xfd;
					//Set bit 2 to 0
				}

				f.Seek( 0xd, SeekOrigin.Begin );
				f.WriteByte( (byte) sciD );
				f.Flush();
			}
		}

		private async Task BuildRomFs( BuildOptions options )
		{
			string romfsDir = Path.Combine( options.SourceDirectory, options.RomFSDirName );
			await this.BuildRomFs( romfsDir, "CustomRomFS.bin" );
		}

		public async Task BuildRomFs( string sourceDirectory, string outputFile )
		{
			await this.RunProgram( this.Path3Dstool, $"-ctf romfs \"{outputFile}\" --romfs-dir \"{sourceDirectory}\"" );
		}

		private async Task BuildExeFs( BuildOptions options )
		{
			string bannerBin = Path.Combine( options.SourceDirectory, options.ExeFSDirName, "banner.bin" );
			string bannerBnr = Path.Combine( options.SourceDirectory, options.ExeFSDirName, "banner.bnr" );
			string iconBin = Path.Combine( options.SourceDirectory, options.ExeFSDirName, "icon.bin" );
			string iconIco = Path.Combine( options.SourceDirectory, options.ExeFSDirName, "icon.icn" );

			//Rename banner
			if ( File.Exists( bannerBin ) && !File.Exists( bannerBnr ) )
			{
				File.Move( bannerBin, bannerBnr );
			}
			else if ( File.Exists( bannerBin ) && File.Exists( bannerBnr ) )
			{
				File.Delete( bannerBnr );
				File.Move( bannerBin, bannerBnr );
			}
			else if ( !File.Exists( bannerBin ) && File.Exists( bannerBnr ) )
			{
				//Do nothing
				//Both files don't exist
			}
			else
			{
				throw new MissingFileException( bannerBin );
			}

			//Rename icon
			if ( File.Exists( iconBin ) && !File.Exists( iconIco ) )
			{
				File.Move( iconBin, iconIco );
			}
			else if ( File.Exists( iconBin ) && File.Exists( iconIco ) )
			{
				File.Delete( iconIco );
				File.Move( iconBin, iconIco );
			}
			else if ( !File.Exists( iconBin ) && File.Exists( iconIco ) )
			{
				//Do nothing
				//Both files don't exist
			}
			else
			{
				throw new MissingFileException( iconBin );
			}

			//Compress code.bin if applicable
			if ( options.CompressCodeBin )
			{
				throw new NotSupportedException();
				//"3dstool -zvf code-patched.bin --compress-type blz --compress-out exefs/code.bin"
			}

			string headerPath = Path.Combine( options.SourceDirectory, options.ExeFSHeaderName );
			string exefsPath = Path.Combine( options.SourceDirectory, options.ExeFSDirName );
			await this.RunProgram( this.Path3Dstool, $"-ctf exefs CustomExeFS.bin --exefs-dir \"{exefsPath}\" --header \"{headerPath}\"" );

			//Rename files back
			File.Move( bannerBnr, bannerBin );
			File.Move( iconIco, iconBin );
		}

		private async Task BuildPartition0( BuildOptions options )
		{
			//Build romfs and exefs
			await this.BuildRomFs( options );
			await this.BuildExeFs( options );

			//Build cxi
			string exheaderPath = Path.Combine( options.SourceDirectory, options.ExheaderName );
			string headerPath = Path.Combine( options.SourceDirectory, options.Partition0HeaderName );
			string logoPath = Path.Combine( options.SourceDirectory, options.LogoLZName );
			string plainPath = Path.Combine( options.SourceDirectory, options.PlainRGNName );
			await this.RunProgram( this.Path3Dstool, $"-ctf cxi CustomPartition0.bin --header \"{headerPath}\" --exh \"{exheaderPath}\" --exefs CustomExeFS.bin --romfs CustomRomFS.bin --logo \"{logoPath}\" --plain \"{plainPath}\"" );

			//Cleanup
			File.Delete( Path.Combine( this.ToolDirectory, "CustomExeFS.bin" ) );
			File.Delete( Path.Combine( this.ToolDirectory, "CustomRomFS.bin" ) );
		}

		private async Task BuildPartition1( BuildOptions options )
		{
			string headerPath = Path.Combine( options.SourceDirectory, options.Partition1HeaderName );
			string extractedPath = Path.Combine( options.SourceDirectory, options.ExtractedManualDirName );
			await this.RunProgram( this.Path3Dstool, $"-ctf romfs CustomManual.bin --romfs-dir \"{extractedPath}\"" );
			await this.RunProgram( this.Path3Dstool, $"-ctf cfa CustomPartition1.bin --header \"{headerPath}\" --romfs CustomManual.bin" );
			File.Delete( Path.Combine( this.ToolDirectory, "CustomManual.bin" ) );
		}

		private async Task BuildPartition2( BuildOptions options )
		{
			string headerPath = Path.Combine( options.SourceDirectory, options.Partition2HeaderName );
			string extractedPath = Path.Combine( options.SourceDirectory, options.ExtractedDownloadPlayDirName );
			await this.RunProgram( this.Path3Dstool, $"-ctf romfs CustomDownloadPlay.bin --romfs-dir \"{extractedPath}\"" );
			await this.RunProgram( this.Path3Dstool, $"-ctf cfa CustomPartition2.bin --header \"{headerPath}\" --romfs CustomDownloadPlay.bin" );
			File.Delete( Path.Combine( this.ToolDirectory, "CustomDownloadPlay.bin" ) );
		}

		private async Task BuildPartition6( BuildOptions options )
		{
			string headerPath = Path.Combine( options.SourceDirectory, options.Partition6HeaderName );
			string extractedPath = Path.Combine( options.SourceDirectory, options.N3DSUpdateDirName );
			await this.RunProgram( this.Path3Dstool, $"-ctf romfs CustomN3DSUpdate.bin --romfs-dir \"{extractedPath}\"" );
			await this.RunProgram( this.Path3Dstool, $"-ctf cfa CustomPartition6.bin --header \"{headerPath}\" --romfs CustomN3DSUpdate.bin" );
			File.Delete( Path.Combine( this.ToolDirectory, "CustomN3DSUpdate.bin" ) );
		}

		private async Task BuildPartition7( BuildOptions options )
		{
			string headerPath = Path.Combine( options.SourceDirectory, options.Partition7HeaderName );
			string extractedPath = Path.Combine( options.SourceDirectory, options.O3DSUpdateDirName );
			await this.RunProgram( this.Path3Dstool, $"-ctf romfs CustomO3DSUpdate.bin --romfs-dir \"{extractedPath}\"" );
			await this.RunProgram( this.Path3Dstool, $"-ctf cfa CustomPartition7.bin --header \"{headerPath}\" --romfs CustomO3DSUpdate.bin" );
			File.Delete( Path.Combine( this.ToolDirectory, "CustomO3DSUpdate.bin" ) );
		}

		private async Task BuildPartitions( BuildOptions options )
		{
			this.Copy3DSTool();

			List<Task> partitionTasks = new List<Task> {
				this.BuildPartition0( options ),
				this.BuildPartition1( options ),
				this.BuildPartition2( options ),
				this.BuildPartition6( options ),
				this.BuildPartition7( options )
			};
			await Task.WhenAll( partitionTasks );

			if ( !File.Exists( Path.Combine( this.ToolDirectory, "CustomPartition0.bin" ) ) )
			{
				throw new MissingFileException( Path.Combine( this.ToolDirectory, "CustomPartition0.bin" ) );
			}
		}

		#endregion

		#region Top Level Public Methods

		/// <summary>
		/// Extracts a decrypted CCI ROM.
		/// </summary>
		/// <param name="filename">Full path of the ROM to extract.</param>
		/// <param name="outputDirectory">Directory into which to extract the files.</param>
		public async Task ExtractCci( string filename, string outputDirectory )
		{
			ExtractionOptions options = new ExtractionOptions {
				SourceRom = filename,
				DestinationDirectory = outputDirectory
			};
			await this.ExtractCci( options );
		}

		/// <summary>
		/// Extracts a CCI ROM.
		/// </summary>
		public async Task ExtractCci( ExtractionOptions options )
		{
			this.IsIndeterminate = true;
			this.IsCompleted = false;

			this.Copy3DSTool();

			if ( !Directory.Exists( options.DestinationDirectory ) )
			{
				Directory.CreateDirectory( options.DestinationDirectory );
			}

			await this.ExtractCciPartitions( options );

			List<Task> partitionExtractions = new List<Task> {
				this.ExtractPartition0( options, "DecryptedPartition0.bin", false ),
				this.ExtractPartition1( options ),
				this.ExtractPartition2( options ),
				this.ExtractPartition6( options ),
				this.ExtractPartition7( options )
			};
			await Task.WhenAll( partitionExtractions );

			this.IsCompleted = true;
		}

		/// <summary>
		/// Extracts a decrypted CXI ROM.
		/// </summary>
		/// <param name="filename">Full path of the ROM to extract.</param>
		/// <param name="outputDirectory">Directory into which to extract the files.</param>
		public async Task ExtractCxi( string filename, string outputDirectory )
		{
			ExtractionOptions options = new ExtractionOptions {
				SourceRom = filename,
				DestinationDirectory = outputDirectory
			};
			await this.ExtractCxi( options );
		}

		/// <summary>
		/// Extracts a CXI partition.
		/// </summary>
		public async Task ExtractCxi( ExtractionOptions options )
		{
			this.IsIndeterminate = true;
			this.IsCompleted = false;

			this.Copy3DSTool();
			this.CopyCtrTool();

			if ( !Directory.Exists( options.DestinationDirectory ) )
			{
				Directory.CreateDirectory( options.DestinationDirectory );
			}

			//Extract partition 0, which is the only partition we have
			await this.ExtractPartition0( options, options.SourceRom, true );

			this.IsCompleted = true;
		}

		/// <summary>
		/// Extracts a decrypted CIA.
		/// </summary>
		/// <param name="filename">Full path of the ROM to extract.</param>
		/// <param name="outputDirectory">Directory into which to extract the files.</param>
		public async Task ExtractCia( string filename, string outputDirectory )
		{
			ExtractionOptions options = new ExtractionOptions {
				SourceRom = filename,
				DestinationDirectory = outputDirectory
			};
			await this.ExtractCia( options );
		}

		/// <summary>
		/// Extracts a CIA.
		/// </summary>
		public async Task ExtractCia( ExtractionOptions options )
		{
			this.IsIndeterminate = true;
			this.IsCompleted = false;

			this.Copy3DSTool();
			this.CopyCtrTool();

			if ( !Directory.Exists( options.DestinationDirectory ) )
			{
				Directory.CreateDirectory( options.DestinationDirectory );
			}

			await this.ExtractCiaPartitions( options );

			List<Task> partitionExtractions = new List<Task> {
				this.ExtractPartition0( options, "DecryptedPartition0.bin", false ),
				this.ExtractPartition1( options ),
				this.ExtractPartition2( options ),
				this.ExtractPartition6( options ),
				this.ExtractPartition7( options )
			};
			await Task.WhenAll( partitionExtractions );

			this.IsCompleted = true;
		}

		/// <summary>
		/// Extracts an NDS ROM.
		/// </summary>
		/// <param name="filename">Full path of the ROM to extract.</param>
		/// <param name="outputDirectory">Directory into which to extract the files.</param>
		public async Task ExtractNds( string filename, string outputDirectory )
		{
			this.Progress = 0;
			this.IsIndeterminate = false;
			this.IsCompleted = false;

			void ReportProgress( object sender, ProgressReportedEventArgs e ) => this.ProgressChanged?.Invoke( this, e );

			if ( !Directory.Exists( outputDirectory ) )
			{
				Directory.CreateDirectory( outputDirectory );
			}

			GenericNDSRom r = new GenericNDSRom();
			PhysicalIOProvider p = new PhysicalIOProvider();

			r.UnpackProgress += ReportProgress;

			await r.OpenFile( filename, p );
			await r.Unpack( outputDirectory, p );

			r.UnpackProgress -= ReportProgress;
			this.IsCompleted = true;
		}

		/// <summary>
		/// Extracts a decrypted CCI or CXI ROM.
		/// </summary>
		/// <param name="filename">Full path of the ROM to extract.</param>
		/// <param name="outputDirectory">Directory into which to extract the files.</param>
		/// <remarks>Extraction type is determined by file extension.  Extensions of $".cxi" are extracted as CXI, all others are extracted as CCI.  To override this behavior, use a more specific extraction function.</remarks>
		/// <exception cref="NotSupportedException">Thrown when the input file is not a supported file.</exception>
		public async Task ExtractAuto( string filename, string outputDirectory )
		{
			switch ( await MetadataReader.GetROMSystem( filename ) )
			{
				case SystemType.NDS:
					await this.ExtractNds( filename, outputDirectory );
					break;
				case SystemType.ThreeDS:
					ASCIIEncoding e = new ASCIIEncoding();
					using ( GenericFile file = new GenericFile() )
					{
						file.EnableInMemoryLoad = false;
						file.IsReadOnly = true;
						await file.OpenFile( filename, new PhysicalIOProvider() );

						if ( file.Length > 104 && e.GetString( await file.ReadAsync( 0x100, 4 ) ) == "NCSD" )
						{
							await this.ExtractCci( filename, outputDirectory );
						}
						else if ( file.Length > 104 && e.GetString( await file.ReadAsync( 0x100, 4 ) ) == "NCCH" )
						{
							await this.ExtractCxi( filename, outputDirectory );
						}
						else if ( file.Length > await file.ReadInt32Async( 0 ) && e.GetString( await file.ReadAsync( 0x100 + MetadataReader.GetCIAContentOffset( file ), 4 ) ) == "NCCH" )
						{
							await this.ExtractCia( filename, outputDirectory );
						}
						else
						{
							throw new NotSupportedException( Language.ErrorInvalidFileFormat );
						}
					}
					break;
				case SystemType.Unknown:
					throw new NotSupportedException( Language.ErrorInvalidFileFormat );
			}
		}

		/// <summary>
		/// Builds a decrypted CCI/3DS file, for use with Citra or Decrypt9
		/// </summary>
		/// <param name="options"></param>
		public async Task Build3DSDecrypted( BuildOptions options )
		{
			this.IsIndeterminate = true;
			this.IsCompleted = false;

			this.UpdateExheader( options, false );

			string headerPath = Path.Combine( options.SourceDirectory, options.RootHeaderName );
			string outputPath = Path.Combine( options.SourceDirectory, options.Destination );
			string partitionArgs = "";

			if ( !File.Exists( headerPath ) )
			{
				throw new IOException( $"NCCH header not found.  This can happen if you extracted a CXI and are trying to rebuild a decrypted CCI.  Try building as a key-0 encrypted CCI instead.  Path of missing header: \"{headerPath}\"." );
			}

			await this.BuildPartitions( options );

			//Delete partitions that are too small
			foreach ( var item in PartitionNumbers )
			{
				FileInfo info = new FileInfo( Path.Combine( this.ToolDirectory, "CustomPartition" + item + ".bin" ) );
				if ( info.Length <= 20000 )
				{
					File.Delete( info.FullName );
				}
				else
				{
					if ( info.Exists )
					{
						var num = item.ToString();
						partitionArgs += $" -{num} CustomPartition{num}.bin";
					}
				}
			}

			await this.RunProgram( this.Path3Dstool, $"-ctf 3ds \"{outputPath}\" --header \"{headerPath}\"{partitionArgs}" );

			//Cleanup
			foreach ( string partition in PartitionNumbers.Select( item => Path.Combine( this.ToolDirectory, "CustomPartition" + item.ToString() + ".bin" ) )
														  .Where( File.Exists ) )
			{
				File.Delete( partition );
			}

			this.IsCompleted = true;
		}

		/// <summary>
		/// Builds a decrypted CCI from the given files.
		/// </summary>
		/// <param name="sourceDirectory">Path of the files to build.  Must have been created with <see cref="ExtractAuto(String, String)"/> or equivalent function using default settings.</param>
		/// <param name="outputROM">Destination of the output ROM.</param>
		public async Task Build3DSDecrypted( string sourceDirectory, string outputROM )
		{
			BuildOptions options = new BuildOptions {
				SourceDirectory = sourceDirectory,
				Destination = outputROM
			};

			await this.Build3DSDecrypted( options );
		}

		/// <summary>
		/// Builds a CCI/3DS file encrypted with a 0-key, for use with Gateway.  Excludes update partitions, download play, and manual.
		/// </summary>
		/// <param name="options"></param>
		public async Task Build3DS0Key( BuildOptions options )
		{
			this.IsIndeterminate = true;
			this.IsCompleted = false;

			this.Copy3DSBuilder();

			this.UpdateExheader( options, false );

			string exHeader = Path.Combine( options.SourceDirectory, options.ExheaderName );
			string exeFS = Path.Combine( options.SourceDirectory, options.ExeFSDirName );
			string romFS = Path.Combine( options.SourceDirectory, options.RomFSDirName );

			if ( options.CompressCodeBin )
			{
				await this.RunProgram( this.Path3DSBuilder, $"\"{exeFS}\" \"{romFS}\" \"{exHeader}\" \"{options.Destination}\"-compressCode" );
				Console.WriteLine( "WARNING: .code.bin is still compressed, and other operations may be affected." );
			}
			else
			{
				await this.RunProgram( this.Path3DSBuilder, $"\"{exeFS}\" \"{romFS}\" \"{exHeader}\" \"{options.Destination}\"" );
				var dotCodeBin = Path.Combine( options.SourceDirectory, options.ExeFSDirName, ".code.bin" );
				var codeBin = Path.Combine( options.SourceDirectory, options.ExeFSDirName, "code.bin" );
				if ( File.Exists( dotCodeBin ) )
				{
					File.Move( dotCodeBin, codeBin );
				}
			}

			this.IsCompleted = true;
		}

		/// <summary>
		/// Builds a 0-key encrypted CCI from the given files.
		/// </summary>
		/// <param name="sourceDirectory">Path of the files to build.  Must have been created with <see cref="ExtractAuto(String, String)"/> or equivalent function using default settings.</param>
		/// <param name="outputROM">Destination of the output ROM.</param>
		public async Task Build3DS0Key( string sourceDirectory, string outputROM )
		{
			BuildOptions options = new BuildOptions {
				SourceDirectory = sourceDirectory,
				Destination = outputROM
			};

			await this.Build3DS0Key( options );
		}

		/// <summary>
		/// Builds a CCI/3DS file encrypted with a 0-key, for use with Gateway.  Excludes update partitions, download play, and manual.
		/// </summary>
		/// <param name="options"></param>
		public async Task BuildCia( BuildOptions options )
		{
			this.IsIndeterminate = true;
			this.IsCompleted = false;

			this.UpdateExheader( options, true );
			this.CopyMakeRom();
			await this.BuildPartitions( options );

			string partitionArgs = "";

			//Delete partitions that are too small
			foreach ( var item in PartitionNumbers )
			{
				FileInfo info = new FileInfo( Path.Combine( this.ToolDirectory, "CustomPartition" + item + ".bin" ) );
				if ( info.Length <= 20000 )
				{
					File.Delete( info.FullName );
				}
				else
				{
					var num = item.ToString();
					partitionArgs += $" -content CustomPartition{num}.bin:{num}";
				}
			}

			//string headerPath = Path.Combine( options.SourceDirectory, options.RootHeaderName );
			string outputPath = Path.Combine( options.SourceDirectory, options.Destination );

			await this.RunProgram( this.PathMakeROM, $"-f cia -o \"{outputPath}\"{partitionArgs}" );

			//Cleanup
			foreach ( var partition in PartitionNumbers.Select( item => Path.Combine( this.ToolDirectory, "CustomPartition" + item + ".bin" ) )
													   .Where( File.Exists ) )
			{
				File.Delete( partition );
			}

			this.IsCompleted = true;
		}

		/// <summary>
		/// Builds a CIA from the given files.
		/// </summary>
		/// <param name="sourceDirectory">Path of the files to build.  Must have been created with <see cref="ExtractAuto(String, String)"/> or equivalent function using default settings.</param>
		/// <param name="outputROM">Destination of the output ROM.</param>
		public async Task BuildCia( string sourceDirectory, string outputROM )
		{
			BuildOptions options = new BuildOptions {
				SourceDirectory = sourceDirectory,
				Destination = outputROM
			};

			await this.BuildCia( options );
		}

		/// <summary>
		/// Builds an NDS ROM from the given files.
		/// </summary>
		/// <param name="sourceDirectory">Path of the files to build.  Must have been created with <see cref="ExtractAuto(String, String)"/> or equivalent function using default settings.</param>
		/// <param name="outputROM">Destination of the output ROM.</param>
		public async Task BuildNDS( string sourceDirectory, string outputROM )
		{
			this.IsIndeterminate = true;
			this.IsCompleted = false;

			this.CopyNDSTool();

			await this.RunProgram( this.PathNDSTool, $"-c \"{outputROM}\" -9 \"{sourceDirectory}/arm9.bin\" -7 \"{sourceDirectory}/arm7.bin\" -y9 \"{sourceDirectory}/y9.bin\" -y7 \"{sourceDirectory}/y7.bin\" -d \"{sourceDirectory}/data\" -y \"{sourceDirectory}/overlay\" -t \"{sourceDirectory}/banner.bin\" -h \"{sourceDirectory}/header.bin\"" );

			this.IsCompleted = true;
		}

		/// <summary>
		/// Builds files for use with HANS.
		/// </summary>
		/// <param name="options">Options to use for the build.  <see cref="BuildOptions.Destination"/> should be the SD card root.</param>
		/// <param name="shortcutName">Name of the shortcut.  Should not contain spaces nor special characters.</param>
		/// <param name="rawName">Raw name for the destination RomFS and Code files.  Should be short, but the exact requirements are unknown.</param>
		public async Task BuildHans( BuildOptions options, string shortcutName, string rawName )
		{
			this.IsIndeterminate = true;
			this.IsCompleted = false;

			//Validate input.  Never trust the user.
			shortcutName = shortcutName.Replace( " ", "" ).Replace( "é", "e" );

			this.Copy3DSTool();

			//Create variables
			var romfsDir = Path.Combine( options.SourceDirectory, options.RomFSDirName );
			var romfsFile = Path.Combine( this.ToolDirectory, "romfsRepacked.bin" );
			var codeFile = Path.Combine( options.SourceDirectory, options.ExeFSDirName, "code.bin" );
			var smdhSourceFile = Path.Combine( options.SourceDirectory, options.ExeFSDirName, "icon.bin" );
			var exheaderFile = Path.Combine( options.SourceDirectory, options.ExheaderName );
			string titleID;

			if ( File.Exists( exheaderFile ) )
			{
				var exheader = File.ReadAllBytes( exheaderFile );
				titleID = BitConverter.ToUInt64( exheader, 0x200 ).ToString( "X" ).PadLeft( 16, '0' );
			}
			else
			{
				throw new IOException( $"Could not find exheader at the path \"{exheaderFile}\"." );
			}

			//Repack romfs
			await this.BuildRomFs( romfsDir, romfsFile );

			//Copy the files
			//- Create non-existant directories
			if ( !Directory.Exists( options.Destination ) )
			{
				Directory.CreateDirectory( options.Destination );
			}
			if ( !Directory.Exists( Path.Combine( options.Destination, "hans" ) ) )
			{
				Directory.CreateDirectory( Path.Combine( options.Destination, "hans" ) );
			}

			//- Copy files if they exist
			if ( File.Exists( romfsFile ) )
			{
				File.Copy( romfsFile, Path.Combine( options.Destination, "hans", rawName + ".romfs" ), true );
			}
			if ( File.Exists( codeFile ) )
			{
				File.Copy( codeFile, Path.Combine( options.Destination, "hans", rawName + ".code" ), true );
			}

			//Create the homebrew launcher shortcut
			if ( !Directory.Exists( Path.Combine( options.Destination, "3ds" ) ) )
			{
				Directory.CreateDirectory( Path.Combine( options.Destination, "3ds" ) );
			}

			//- Copy smdh
			bool iconExists = false;
			if ( File.Exists( smdhSourceFile ) )
			{
				iconExists = true;
				File.Copy( smdhSourceFile, Path.Combine( options.Destination, "3ds", shortcutName + ".smdh" ), true );
			}

			//- Write hans shortcut
			StringBuilder shortcut = new StringBuilder();
			shortcut.AppendLine( "<shortcut>" );
			shortcut.AppendLine( "\t<executable>/3ds/hans/hans.3dsx</executable>" );
			if ( iconExists )
			{
				shortcut.AppendLine( $"\t<icon>/3ds/{shortcutName}.smdh</icon>" );
			}
			shortcut.AppendLine( $"\t<arg>-f/3ds/hans/titles/{rawName}.txt</arg>" );
			shortcut.AppendLine( "</shortcut>" );
			shortcut.AppendLine( "<targets selectable=\"false\">" );
			shortcut.AppendLine( $"\t<title mediatype=\"2\">{titleID}</title>" );
			shortcut.AppendLine( $"\t<title mediatype=\"1\">{titleID}</title>" );
			shortcut.AppendLine( "</targets>" );
			File.WriteAllText( Path.Combine( options.Destination, "3ds", shortcutName + ".xml" ), shortcut.ToString() );

			//- Write hans title settings
			StringBuilder preset = new StringBuilder();
			preset.Append( "region : -1" );
			preset.Append( "\n" );
			preset.Append( "language : -1" );
			preset.Append( "\n" );
			preset.Append( "clock : 0" );
			preset.Append( "\n" );
			preset.Append( "romfs : 0" );
			preset.Append( "\n" );
			preset.Append( "code : 0" );
			preset.Append( "\n" );
			preset.Append( "nim_checkupdate : 1" );
			preset.Append( "\n" );
			if ( !Directory.Exists( Path.Combine( options.Destination, "3ds", "hans", "titles" ) ) )
			{
				Directory.CreateDirectory( Path.Combine( options.Destination, "3ds", "hans", "titles" ) );
			}
			File.WriteAllText( Path.Combine( options.Destination, "3ds", "hans", "titles", rawName + ".txt" ), preset.ToString() );

			this.IsCompleted = true;
		}

		/// <summary>
		/// Builds files for use with HANS.
		/// </summary>
		/// <param name="sourceDirectory">Path of the files to build.  Must have been created with <see cref="ExtractAuto(String, String)"/> or equivalent function using default settings.</param>
		/// <param name="sdRoot">Root of the SD card</param>
		/// <param name="rawName">Raw name for the destination RomFS, Code, and shortcut files.  Should be short, but the exact requirements are unknown.  To use a different name for shortcut files, use <see cref="BuildHans(BuildOptions, String, String)"/>.</param>
		public async Task BuildHans( string sourceDirectory, string sdRoot, string rawName )
		{
			BuildOptions options = new BuildOptions {
				SourceDirectory = sourceDirectory,
				Destination = sdRoot
			};
			await this.BuildHans( options, rawName, rawName );
		}

		/// <summary>
		/// Builds a ROM from the given files.
		/// </summary>
		/// <param name="sourceDirectory">Path of the files to build.  Must have been created with <see cref="ExtractAuto(String, String)"/> or equivalent function using default settings.</param>
		/// <param name="outputROM">Destination of the output ROM.</param>
		/// <remarks>Output format is determined by file extension.  Extensions of ".cia" build a CIA, extensions of ".3dz" build a 0-key encrypted CCI, and all others build a decrypted CCI.  To force a different behavior, use a more specific Build function.</remarks>
		public async Task BuildAuto( string sourceDirectory, string outputROM )
		{
			var ext = Path.GetExtension( outputROM )?.ToLower();
			switch ( ext )
			{
				case ".cia":
					await this.BuildCia( sourceDirectory, outputROM );
					break;
				case ".3dz":
					await this.Build3DS0Key( sourceDirectory, outputROM );
					break;
				case ".nds":
				case ".srl":
					await this.BuildNDS( sourceDirectory, outputROM );
					break;
				default:
					await this.Build3DSDecrypted( sourceDirectory, outputROM );
					break;
			}
		}

		#endregion

		#region IDisposable 

		private bool disposed;

		protected void Dispose( bool disposing )
		{
			if ( !this.disposed )
			{
				if ( disposing )
				{
					this.DeleteTools();
				}
			}

			this.disposed = true;
		}

		public void Dispose() => this.Dispose( true );

		#endregion
	}
}
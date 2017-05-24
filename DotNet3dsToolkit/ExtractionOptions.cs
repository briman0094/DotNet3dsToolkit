namespace DotNet3dsToolkit
{
	public class ExtractionOptions
	{
		public string SourceRom { get; set; }

		public string DestinationDirectory { get; set; }

		public string RomFSDirName { get; set; } = "RomFS";
		public string ExeFSDirName { get; set; } = "ExeFS";
		public string ExeFSHeaderName { get; set; } = "HeaderExeFS.bin";
		public string ExheaderName { get; set; } = "ExHeader.bin";
		public string LogoLZName { get; set; } = "LogoLZ.bin";
		public string PlainRGNName { get; set; } = "PlainRGN.bin";
		public string Partition0HeaderName { get; set; } = "HeaderNCCH0.bin";
		public string Partition1HeaderName { get; set; } = "HeaderNCCH1.bin";
		public string Partition2HeaderName { get; set; } = "HeaderNCCH2.bin";
		public string Partition6HeaderName { get; set; } = "HeaderNCCH6.bin";
		public string Partition7HeaderName { get; set; } = "HeaderNCCH7.bin";
		public string ExtractedManualDirName { get; set; } = "Manual";
		public string ExtractedDownloadPlayDirName { get; set; } = "DownloadPlay";
		public string N3DSUpdateDirName { get; set; } = "N3DSUpdate";
		public string O3DSUpdateDirName { get; set; } = "O3DSUpdate";
		public string RootHeaderName { get; set; } = "HeaderNCCH.bin";
	}
}
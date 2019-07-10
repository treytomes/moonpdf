namespace MoonPdfLib.MuPdf
{
	public class FileSource : IPdfSource
	{
		public FileSource(string filename)
		{
			Filename = filename;
		}

		public string Filename { get; private set; }
	}
}

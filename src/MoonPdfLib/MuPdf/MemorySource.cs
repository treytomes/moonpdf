using System.IO;

namespace MoonPdfLib.MuPdf
{
	public class MemorySource : IPdfSource
	{
		public MemorySource(byte[] bytes)
		{
			Bytes = bytes;
		}

		public MemorySource(MemoryStream stream)
		{
			Bytes = stream.GetBuffer();
		}

		public byte[] Bytes { get; private set; }
	}
}

using System;

namespace MoonPdfLib.MuPdf
{
	public class MissingOrInvalidPdfPasswordException : Exception
	{
		public MissingOrInvalidPdfPasswordException()
			: base(MoonPdfLib.Properties.Resources.MissingOrInvalidPdfPasswordException)
		{
		}
	}
}

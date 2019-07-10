using System;

namespace MoonPdfLib.MuPdf
{
	public class SecuredSource : IPdfSource
	{
		public SecuredSource(IPdfSource source, string password)
		{
			if (source is SecuredSource)
			{
				throw new ArgumentException(MoonPdfLib.Properties.Resources.SecuredSourceArgumentException, nameof(source));
			}

			Password = password;
		}

		public IPdfSource Source { get; }

		public string Password { get; }
	}
}

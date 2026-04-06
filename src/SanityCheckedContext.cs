using System.Net;

namespace SatisfactorDotDev.AssetHttp;

class SanityCheckedContext
{
	public readonly HttpListenerContext Full;

	protected readonly Uri url;

	public bool IsSatisfactory { get; private set; }

	public SanityCheckedContext(
		HttpListenerContext context
	) {
		if (null == context.Request.Url) {
			throw new Exception("URL not specified on context request!");
		}

		url = context.Request.Url;

		IsSatisfactory = url.LocalPath.StartsWith("/satisfactory/");

		Full = context;
	}
}

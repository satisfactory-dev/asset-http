using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets;

using CUE4Parse_Conversion.Textures;

using SkiaSharp;

namespace SatisfactorDotDev.AssetHttp;

class SanityCheckedSatisfactoryContext : SanityCheckedContext
{
	private static readonly SHA512 SHA512 = SHA512.Create();

	public string Version {get; private set; }

	public string Path { get; private set; }

	protected Games.Satisfactory Satisfactory { get; private set; }

	protected CTexture? Asset { get; private set; }

	private bool asset_fetched = false;

	public bool IsMetadataRequest { get; private set; }

	public bool IsAssetRequest {
		get {
			return !IsMetadataRequest;
		}
	}

	public bool Exists {
		get
		{
			return TextureExists(Path);
		}
	}

	public FileProviderDictionary Files
	{
		get
		{
			return Satisfactory.Files;
		}
	}

	public SanityCheckedSatisfactoryContext(
		HttpListenerContext context,
		Dictionary<string, Games.Satisfactory> satisfactory_versions
	) : base(context) {
		if (!IsSatisfactory) {
			throw new UnsatisfactoryException($"Context was not Satisfactory! ({context.Request.Url?.LocalPath})");
		}

		string[] parts = url.LocalPath.Split("/", 4);

		if (parts.Length < 4)
		{
			throw new UnsatisfactoryException("Expecting at least 4 parts to URL path!");
		}

		if (!satisfactory_versions.ContainsKey(parts[2]))
		{
			throw new UnsatisfactoryException("Unsupported version specified!");
		}

		Version = parts[2];

		Path = parts[3];

		if (Path.EndsWith(".metadata.json")) {
			IsMetadataRequest = true;
			Path = Path[..^14];
		} else {
			IsMetadataRequest = false;
		}

		Satisfactory = satisfactory_versions[Version];

		if (!Satisfactory.Exists)
		{
			throw new UnsatisfactoryException($"Satisfactory {Version} does not exist!");
		}
	}

	private CTexture? Texture()
	{
		if (null == Asset && !asset_fetched)
		{
			Asset = Satisfactory.LoadTexture($"/{Path}");
			asset_fetched = true;

			return Asset;
		#pragma warning disable IDE0150 // Prefer 'null' check over type check
		} else if (Asset is CTexture) {
		#pragma warning restore IDE0150 // Prefer 'null' check over type check
			return Asset;
		};

		return null;
	}

	public SKData ToPng()
	{
		CTexture? texture = Texture();

		return null == texture
			? throw new UnsatisfactoryException("Texture does not exist!")
			: texture.ToSkBitmap().Encode(SKEncodedImageFormat.Png, 100);
	}

	public JsonObject ToMetadata()
	{
		CTexture? texture = Texture();

		return null == texture
			? throw new UnsatisfactoryException("Texture does not exist!")
			: new()
		{
			["Width"] = texture.Width,
			["Height"] = texture.Height,
			["SHA512"] = Convert.ToHexStringLower(SHA512.ComputeHash(texture.Data)),
			["Size"] = texture.Data.Length,
		};
	}

	public IPackage? LoadPackage(string path)
	{
		return Satisfactory.LoadPackage(path);
	}

	public bool TextureExists(string path)
	{
		return Satisfactory.TexureExists(path);
	}
}

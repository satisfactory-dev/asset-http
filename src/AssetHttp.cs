using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Json.Schema;
using SkiaSharp;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.FileProvider.Vfs;
using Org.BouncyCastle.Crypto.Digests;
using System.Security.Cryptography;
using Json.More;
using System.Text.Json;

namespace SatisfactorDotDev.AssetHttp;

class AssetHttpJsonItem(
	string version,
	string unreal_engine,
	bool usmap
) {
	public string version = version;

	public string unreal_engine = unreal_engine;

	public bool usmap = usmap;

	public static implicit operator AssetHttpJsonItem(JsonNode? maybe)
	{
		if (maybe is not JsonObject)
		{
			throw new Exception("Node must be an object!");
		}

		JsonObject obj = (JsonObject) maybe;

		JsonNode? Node_usmap = obj["usmap"];

		if (
			!obj.TryGetPropertyValue("unreal_engine", out JsonNode? Node_unreal_engine)
			|| null == Node_unreal_engine
		) {
			throw new Exception("Node must contain unreal_engine!");
		}

		if (
			!obj.TryGetPropertyValue("version", out JsonNode? Node_version)
			|| null == Node_version
		) {
			throw new Exception("Node must contain version!");
		}

		return new AssetHttpJsonItem(
			Node_version.GetValue<string>(),
			Node_unreal_engine.GetValue<string>(),
			null != Node_usmap && Node_usmap.GetValue<bool>()
		);
	}

	public static List<AssetHttpJsonItem> ListFromArray(JsonArray maybe)
	{
		List<AssetHttpJsonItem> result = [];

		foreach (JsonNode? item in maybe) {
			result.Add(item);
		}

		return result;
	}
}

class SanityCheckedContext
{
	public readonly HttpListenerContext full_context;

	protected readonly Uri Url;

	public bool IsSatisfactory { get; private set; }

	public SanityCheckedContext(
		HttpListenerContext context
	) {
		if (null == context.Request.Url) {
			throw new Exception("URL not specified on context request!");
		}

		Url = context.Request.Url;

		IsSatisfactory = Url.LocalPath.StartsWith("/satisfactory/");

		full_context = context;
	}
}

class UnsatisfactoryException(string message) : Exception(message) {}

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

	public SanityCheckedSatisfactoryContext(
		HttpListenerContext context,
		Dictionary<string, Games.Satisfactory> SatisfactoryVersions
	) : base(context) {
		if (!IsSatisfactory) {
			throw new UnsatisfactoryException($"Context was not Satisfactory! ({context.Request.Url?.LocalPath})");
		}

		string[] parts = Url.LocalPath.Split("/", 4);

		if (parts.Length < 4)
		{
			throw new UnsatisfactoryException("Expecting at least 4 parts to URL path!");
		}

		if (!SatisfactoryVersions.ContainsKey(parts[2]))
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

		Satisfactory = SatisfactoryVersions[Version];

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
		} else if (Asset is CTexture) {
			return Asset;
		};

		return null;
	}

	public SKData ToPng()
	{
		CTexture? texture = this.Texture();

		if (null == texture)
		{
			throw new UnsatisfactoryException("Texture does not exist!");
		}

		return texture.ToSkBitmap().Encode(SKEncodedImageFormat.Png, 100);
	}

	public JsonObject ToMetadata()
	{
		CTexture? texture = this.Texture();

		if (null == texture)
		{
			throw new UnsatisfactoryException("Texture does not exist!");
		}

		return new()
		{
			["Width"] = texture.Width,
			["Height"] = texture.Height,
			["SHA512"] = Convert.ToHexStringLower(SHA512.ComputeHash(texture.Data)),
			["Size"] = texture.Data.Length,
		};
	}
}

class AssetHttp
{
	private static HttpListener? listener = null;
	private static readonly Dictionary<string, Games.Satisfactory> Satisfactory = [];


	public static async Task Main()
	{
		Console.WriteLine("Starting");

		listener = new HttpListener();

		listener.Prefixes.Add($"http://127.0.0.1:5000/");

		string schema_string = """
			{
				"$schema": "https://json-schema.org/draft/2020-12/schema",
				"$defs": {
					"unreal_engine": {
						"type": "string",
						"enum": [
							"4.20",
							"4.26",
							"5.2",
							"5.3",
							"5.6",
							"5.7"
						]
					},
					"semver": {
						"type": "string",
						"pattern": "^\\d+\\.\\d+\\.\\d+\\.\\d+$"
					}
				},
				"type": "array",
				"minItems": 1,
				"items": {
					"type": "object",
					"required": [
						"version",
						"unreal_engine"
					],
					"properties": {
						"version": {
							"$ref": "#/$defs/semver"
						},
						"usmap": {
							"type": "boolean"
						},
						"unreal_engine": {
							"$ref": "#/$defs/unreal_engine"
						}
					}
				}
			}

		""";

		JsonSchema schema = JsonSchema.FromText(schema_string);

		string data_contents;

		using (StreamReader stream = new StreamReader("./satisfactory.json", Encoding.UTF8)) {
			data_contents = stream.ReadToEnd();
		}

		JsonElement maybe = JsonElement.Parse(data_contents);

		EvaluationResults data_items = schema.Evaluate(maybe);

		JsonNode? node = maybe.AsNode();

		if (node is not JsonArray)
		{
			throw new Exception("Expecting an array!");
		} else {
			Console.WriteLine($"{node.AsArray().Count} items in config");
		}

		if ( ! data_items.IsValid) {
			throw new Exception("data not valid!");
		}

		List<Games.Satisfactory> versions = [];

		List<AssetHttpJsonItem> items = AssetHttpJsonItem.ListFromArray(node.AsArray());

		Console.WriteLine($"{items.Count} items found in config");

		foreach (AssetHttpJsonItem entry in items) {
			EGame unreal_engine = entry.unreal_engine switch
			{
				"4.20" => EGame.GAME_UE4_20,
				"4.21" => EGame.GAME_UE4_21,
				"4.22" => EGame.GAME_UE4_22,
				"4.23" => EGame.GAME_UE4_23,
				"4.24" => EGame.GAME_UE4_24,
				"4.25" => EGame.GAME_UE4_25,
				"4.26" => EGame.GAME_UE4_26,
				"5.0" => EGame.GAME_UE5_0,
				"5.1" => EGame.GAME_UE5_1,
				"5.2" => EGame.GAME_UE5_2,
				"5.3" => EGame.GAME_UE5_3,
				"5.4" => EGame.GAME_UE5_4,
				"5.5" => EGame.GAME_UE5_5,
				"5.6" => EGame.GAME_UE5_6,
				"5.7" => EGame.GAME_UE5_7,
				_ => throw new Exception("Unsupported Unreal Engine version"),
			};

			versions.Add(new Games.Satisfactory(
				entry.version,
				unreal_engine,
				entry.usmap
			));
		}

		foreach (Games.Satisfactory version in versions)
		{
			Satisfactory[version.game_version] = version;
			Console.WriteLine($"Satisfactory {version.game_version} {(version.Exists ? "does" : "does not")} exist");
		}

		listener.Start();

		while (true)
		{
			HttpListenerContext full_context = listener.GetContext();

			if ("/favicon.ico" == full_context.Request.Url?.LocalPath)
			{
				full_context.Response.StatusCode = 404;
				full_context.Response.Close();
				continue;
			}

			try {
				SanityCheckedSatisfactoryContext context = new(
					full_context,
					Satisfactory
				);

				if (context.IsMetadataRequest)
				{
					await UriToAssetMetaDataAsync(context);
				} else {
					UriToAsset(context);
				}
			} catch (UnsatisfactoryException e) {
				Console.Error.Write(e);
			}
		}
	}

	protected static void UriToAsset(SanityCheckedSatisfactoryContext context)
	{
		SKData png = context.ToPng();

		context.full_context.Response.ContentLength64 = png.Size;
		png.AsStream().CopyTo(context.full_context.Response.OutputStream);
		context.full_context.Response.OutputStream.Close();
	}

	protected static async Task UriToAssetMetaDataAsync(SanityCheckedSatisfactoryContext context)
	{
		JsonObject metadata = context.ToMetadata();

		await JsonSerializer.SerializeAsync(
			context.full_context.Response.OutputStream,
			metadata
		);

		context.full_context.Response.OutputStream.Close();
	}
}

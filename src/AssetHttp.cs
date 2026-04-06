using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using CUE4Parse.UE4.Versions;

using Json.More;
using Json.Schema;

using SkiaSharp;

namespace SatisfactorDotDev.AssetHttp;

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

		using (StreamReader stream = new("./satisfactory.json", Encoding.UTF8)) {
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
			EGame unreal_engine = entry.Unreal_engine switch
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
				entry.Version,
				unreal_engine,
				entry.Usmap
			));
		}

		foreach (Games.Satisfactory version in versions)
		{
			Satisfactory[version.Game_Version] = version;
			Console.WriteLine($"Satisfactory {version.Game_Version} {(version.Exists ? "does" : "does not")} exist");
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

				try {
					if (!context.Exists) {
						Console.WriteLine($"Request for ${context.Path} failed, does not exist!");
						full_context.Response.StatusCode = 404;
						full_context.Response.Close();
						continue;
					}

					if (context.IsMetadataRequest)
					{
						await UriToAssetMetaDataAsync(context);
					} else {
						UriToAsset(context);
					}
				} catch (UnsatisfactoryException e) {
					Console.WriteLine($"Request for ${context.Path} failed, exception occurred!");
					Console.Error.Write(e);
				}
			} catch (UnsatisfactoryException e) {
				Console.Error.Write(e);
			}
		}
	}

	protected static void UriToAsset(SanityCheckedSatisfactoryContext context)
	{
		SKData png = context.ToPng();

		context.Full.Response.ContentLength64 = png.Size;
		png.AsStream().CopyTo(context.Full.Response.OutputStream);
		context.Full.Response.OutputStream.Close();
	}

	protected static async Task UriToAssetMetaDataAsync(SanityCheckedSatisfactoryContext context)
	{
		JsonObject metadata = context.ToMetadata();

		await JsonSerializer.SerializeAsync(
			context.Full.Response.OutputStream,
			metadata
		);

		context.Full.Response.OutputStream.Close();
	}
}

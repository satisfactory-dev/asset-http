using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Texture;
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

			JsonNode? json_response = null;
			SKData? png_response = null;

			int status_code = 200;

			try {
				SanityCheckedSatisfactoryContext context = new(
					full_context,
					Satisfactory
				);

				if ("textures.json" == context.Path) {
					json_response = UriToTextureList(context);
				}

				if (null == json_response && null == png_response) {
					try {
						if (!context.Exists) {
							Console.WriteLine($"Request for {context.Path} failed, does not exist!");
							status_code = 404;
						} else {
							if (context.IsMetadataRequest)
							{
								json_response = UriToAssetMetaDataAsync(context);
							} else {
								png_response = context.ToPng();
							}
						}
					} catch (UnsatisfactoryException e) {
						status_code = 500;
						Console.WriteLine($"Request for {context.Path} failed, exception occurred!");
						Console.Error.Write(e);
					}
				}
			} catch (UnsatisfactoryException e) {
				status_code = 400;
				Console.Error.Write(e);
			}


			if (null != json_response)
			{
				await JsonSerializer.SerializeAsync(
					full_context.Response.OutputStream,
					json_response
				);

				full_context.Response.ContentType = "application/json";
			} else if (null != png_response) {
				full_context.Response.ContentLength64 = png_response.Size;
				png_response.AsStream().CopyTo(full_context.Response.OutputStream);
			} else {
				status_code = 404;
			}

			full_context.Response.StatusCode = status_code;
			full_context.Response.Close();
		}
	}

	protected static JsonObject UriToAssetMetaDataAsync(SanityCheckedSatisfactoryContext context)
	{
		return context.ToMetadata();
	}

	protected static JsonArray UriToTextureList(SanityCheckedSatisfactoryContext context)
	{
		JsonArray output = [];

		foreach (GameFile file in context.Files.Values)
		{
			if (file.Path.EndsWith(".uasset"))
			{
				IPackage? package = context.LoadPackage(file.Path);

				if (null != package)
				{
					foreach (object item in package.GetExports())
					{
						if (item is UTexture2D)
						{
							string path = file.PathWithoutExtension;

							if (path.StartsWith("FactoryGame/Content/FactoryGame/"))
							{
								string double_last = $"{path}.{path.Split("/").Last()}";

								if (context.TextureExists(double_last))
								{
									path = double_last;
								}

								string with_prefix = $"Game/{path[20..]}";

								if (context.TextureExists(with_prefix))
								{
									path = with_prefix;
								}
							}

							path = $"/{path}";

							if (!output.Contains(path))
							{
								output.Add(path);
							}
						}
					}
				}
			}
		}

		return output;
	}
}

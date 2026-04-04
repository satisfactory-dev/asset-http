using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Json.Schema;
using SkiaSharp;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures;

namespace SatisfactorDotDev.AssetHttp;

class AssetHttpJsonItem
{
	public string version;

	public string unreal_engine;
}

class AssetHttp
{
	private static HttpListener? listener = null;
	private static Dictionary<string, Games.Satisfactory> Satisfactory = new Dictionary<string, Games.Satisfactory>();


	public static void Main()
	{
		listener = new HttpListener();

		listener.Prefixes.Add($"http://127.0.0.1:5000/");

		string schema_string = """
			{
				"$schema": "https://json-schema.org/draft/2020-12/schema",
				"$defs": {
					"unreal_engine": {
						"type": "string",
						"enum": [
							"5.2",
							"5.3"
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

		JsonNode data = JsonNode.Parse(data_contents);

		EvaluationResults data_items = schema.Evaluate(data);

		if ( ! data_items.IsValid) {
			throw new Exception("data not valid!");
		}

		List<Games.Satisfactory> versions = new List<Games.Satisfactory>();

		foreach (JsonObject entry in (JsonArray) data) {
			EGame unreal_engine;

			switch (entry["unreal_engine"].ToString()) {
				case "4.20":
					unreal_engine = EGame.GAME_UE4_20;
					break;
				case "4.21":
					unreal_engine = EGame.GAME_UE4_21;
					break;
				case "4.22":
					unreal_engine = EGame.GAME_UE4_22;
					break;
				case "4.23":
					unreal_engine = EGame.GAME_UE4_23;
					break;
				case "4.24":
					unreal_engine = EGame.GAME_UE4_24;
					break;
				case "4.25":
					unreal_engine = EGame.GAME_UE4_25;
					break;
				case "4.26":
					unreal_engine = EGame.GAME_UE4_26;
					break;
				case "5.0":
					unreal_engine = EGame.GAME_UE5_0;
					break;
				case "5.1":
					unreal_engine = EGame.GAME_UE5_1;
					break;
				case "5.2":
					unreal_engine = EGame.GAME_UE5_2;
					break;
				case "5.3":
					unreal_engine = EGame.GAME_UE5_3;
					break;
				case "5.4":
					unreal_engine = EGame.GAME_UE5_4;
					break;
				case "5.5":
					unreal_engine = EGame.GAME_UE5_5;
					break;
				case "5.6":
					unreal_engine = EGame.GAME_UE5_6;
					break;
				case "5.7":
					unreal_engine = EGame.GAME_UE5_7;
					break;
				default:
					throw new Exception("Unsupported Unreal Engine version");
			}

			versions.Add(new Games.Satisfactory(
				entry["version"].ToString(),
				unreal_engine
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
			HttpListenerContext context = listener.GetContext();

			if (context.Request.Url.LocalPath.StartsWith("/test.txt"))
			{
				byte[] buffer = System.Text.Encoding.UTF8.GetBytes("foo");
				context.Response.ContentLength64 = buffer.Length;
				context.Response.OutputStream.Write(buffer);
				context.Response.OutputStream.Close();
			}

			UriToAsset(context);
		}

		listener.Stop();
	}

	protected static void UriToAsset(HttpListenerContext context)
	{
		Uri Url = context.Request.Url;

		if (!Url.LocalPath.StartsWith("/satisfactory/")) {
			return;
		}

		string[] parts = Url.LocalPath.Split("/", 4);

		if (
			parts.Length < 4
			|| !(Satisfactory.ContainsKey(parts[2]))
		) {
			return;
		}

		object files = Satisfactory[parts[2]].Files;

		CTexture texture = Satisfactory[parts[2]].LoadTexture($"/{parts[3]}");

		SKData png = texture.ToSkBitmap().Encode(SKEncodedImageFormat.Png, 100);

		context.Response.ContentLength64 = png.Size;
		png.AsStream().CopyTo(context.Response.OutputStream);
		context.Response.OutputStream.Close();
	}
}

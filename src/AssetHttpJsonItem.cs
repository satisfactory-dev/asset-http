using System.Text.Json.Nodes;

namespace SatisfactorDotDev.AssetHttp;

class AssetHttpJsonItem(
	string version,
	string unreal_engine,
	bool usmap
) {
	public string Version = version;

	public string Unreal_engine = unreal_engine;

	public bool Usmap = usmap;

	public static implicit operator AssetHttpJsonItem(JsonNode? maybe)
	{
		if (maybe is not JsonObject)
		{
			throw new Exception("Node must be an object!");
		}

		JsonObject obj = (JsonObject) maybe;

		JsonNode? node_usmap = obj["usmap"];

		if (
			!obj.TryGetPropertyValue("unreal_engine", out JsonNode? node_unreal_engine)
			|| null == node_unreal_engine
		) {
			throw new Exception("Node must contain unreal_engine!");
		}

		#pragma warning disable IDE0046 // Convert to conditional expression
		if (
			!obj.TryGetPropertyValue("version", out JsonNode? node_version)
			|| null == node_version
		) {
			throw new Exception("Node must contain version!");
		}
		#pragma warning restore IDE0046 // Convert to conditional expression

		return new AssetHttpJsonItem(
			node_version.GetValue<string>(),
			node_unreal_engine.GetValue<string>(),
			null != node_usmap && node_usmap.GetValue<bool>()
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

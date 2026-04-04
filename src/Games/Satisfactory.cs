using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures;
using SkiaSharp;

namespace SatisfactorDotDev.AssetHttp.Games;

class Satisfactory
{
	public readonly string game_version;
	public readonly EGame ue_version;

	public readonly bool look_for_usmap;

	private DefaultFileProvider? _provider = null;

	public Satisfactory(
		string game,
		EGame unreal_engine,
		bool look_for_usmap
	) {
		game_version = game;
		ue_version = unreal_engine;
		this.look_for_usmap = look_for_usmap;
	}

	public bool Exists
	{
		get
		{
			return (
				File.Exists($"{this.directory}/FactoryGame.exe")
				|| File.Exists($"{this.directory}/FactoryGameSteam.exe")
			);
		}
	}

	private string directory
	{
		get
		{
			return $"../data/Satisfactory/{game_version}";
		}
	}

	private DefaultFileProvider provider
	{
		get
		{
			if (null == _provider) {
				string oodle_dll_path = Path.Combine("./", OodleHelper.OODLE_NAME_OLD);
				OodleHelper.Initialize(Path.GetFullPath(oodle_dll_path));
				_provider = new DefaultFileProvider(
					$"{this.directory}/FactoryGame/Content/Paks",
					SearchOption.AllDirectories,
					true,
					new VersionContainer(ue_version)
				);
				if (look_for_usmap) {
				_provider.MappingsContainer = new FileUsmapTypeMappingsProvider(
					$"{this.directory}/CommunityResources/FactoryGame.usmap"
				);
				}
				_provider.Initialize();
				_provider.SubmitKey(new FGuid(), new FAesKey(($"0x{new string('0', 64)}")));
				/*
				_provider.LoadLocalization(ELanguage.English);
				*/
			}

			return _provider;
		}
	}

	private object LoadObject(string path)
	{
		return this.provider.LoadPackageObject(path);
	}

	public CTexture? LoadTexture(string path)
	{
		object obj = this.LoadObject(path);

		if (!(obj is UTexture2D)) {
			return null;
		}

		return TextureDecoder.Decode((UTexture2D) obj);
	}

	public object Files
	{
		get
		{
			return this.provider.Files;
		}
	}
}

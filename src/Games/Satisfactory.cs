using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;

using CUE4Parse_Conversion.Textures;

namespace SatisfactorDotDev.AssetHttp.Games;

class Satisfactory(
    string game,
    EGame unreal_engine,
    bool look_for_usmap
) {
	public readonly string Game_Version = game;
	public readonly EGame UE_Version = unreal_engine;

	public readonly bool Look_for_usmap = look_for_usmap;

	public bool Exists
	{
		get
		{
			return (
				File.Exists($"{Directory}/FactoryGame.exe")
				|| File.Exists($"{Directory}/FactoryGameSteam.exe")
			);
		}
	}

	private string Directory
	{
		get
		{
			return $"../data/Satisfactory/{Game_Version}";
		}
	}

	private DefaultFileProvider Provider
	{
		get
		{
			if (null == field) {
				string oodle_dll_path = Path.Combine("./", OodleHelper.OODLE_NAME_OLD);
				OodleHelper.Initialize(Path.GetFullPath(oodle_dll_path));
				field = new DefaultFileProvider(
					$"{Directory}/FactoryGame/Content/Paks",
					SearchOption.AllDirectories,
					new VersionContainer(UE_Version),
					StringComparer.OrdinalIgnoreCase
				);
				if (Look_for_usmap) {
					field.MappingsContainer = new FileUsmapTypeMappingsProvider(
						$"{Directory}/CommunityResources/FactoryGame.usmap"
					);
				}
				field.Initialize();
				field.SubmitKey(new FGuid(), new FAesKey(($"0x{new string('0', 64)}")));
				/*
				field.LoadLocalization(ELanguage.English);
				*/
			}

			return field;
		}
	} = null;

	private object LoadObject(string path)
	{
		return Provider.LoadPackageObject(path);
	}

	public CTexture? LoadTexture(string path)
	{
		object obj = LoadObject(path);

		return obj is not UTexture2D ? null : TextureDecoder.Decode((UTexture2D) obj);
	}

	public FileProviderDictionary Files
	{
		get
		{
			return Provider.Files;
		}
	}
}

setup:
	@git submodule update --init --recursive
	@dotnet restore ./CUE4Parse/CUE4Parse/CUE4Parse.csproj
	@dotnet restore ./src/AssetHttp.csproj
	@make UnrealEngine UnrealEngine--copy-Oodle

.PHONY: UnrealEngine
UnrealEngine:
	@cd UnrealEngine && ./Setup.sh && ./GenerateProjectFiles.sh && make UnrealPak

UnrealEngine--copy-Oodle:
	@cp ./UnrealEngine/Engine/Source/Programs/Shared/EpicGames.Oodle/Sdk/2.9.10/linux/lib/liboo2corelinux64.so.9 ./src/oo2core_9_win64.dll

build: setup deps build--skip-setup

build--skip-setup:
	@dotnet build ./src/AssetHttp.csproj
	@rsync -avu ./src/bin/Debug/net10.0/linux-x64/ ./bin/

deps: deps--cue4parse

deps--cue4parse:
	@dotnet build ./CUE4Parse/CUE4Parse/CUE4Parse.csproj

publish: setup deps publish--skip-setup

publish--skip-setup:
	@dotnet publish ./src/AssetHttp.csproj
	@rsync -avu ./src/bin/Release/net10.0/linux-x64/publish/ ./bin/

lint:
	@dotnet format style -v detailed --severity info --verify-no-changes src

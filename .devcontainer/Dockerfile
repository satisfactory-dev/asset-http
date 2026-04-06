FROM mcr.microsoft.com/devcontainers/dotnet:10.0

USER root

RUN \
	dotnet workload update \
	&& echo "done setting up deps"

USER vscode

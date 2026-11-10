# Build from the repository root: docker build -f service/runner/Dockerfile -t wherigo-compiler-job .
# Windows containers require a compatible Windows host. No credentials enter this image.
FROM mcr.microsoft.com/dotnet/framework/sdk:4.8-windowsservercore-ltsc2022 AS build
WORKDIR C:/src
COPY . .
RUN powershell -NoProfile -File service/runner/build.ps1

FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2022
WORKDIR C:/compiler
COPY --from=build C:/src/bin/Release/ C:/compiler/
USER ContainerUser
ENTRYPOINT ["C:\\compiler\\CompilerJob.exe"]

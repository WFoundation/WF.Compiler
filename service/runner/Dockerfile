# Build from the repository root: docker build -f service/runner/Dockerfile -t wherigo-compiler-job .
# Windows containers require a compatible Windows host. No credentials enter this image.
FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR C:/src
COPY . .
RUN powershell -NoProfile -File service/runner/build.ps1

# The build output is self contained, so plain Server Core is enough (it has GDI+ for System.Drawing)
FROM mcr.microsoft.com/windows/servercore:ltsc2022
WORKDIR C:/compiler
COPY --from=build C:/src/bin/Release/ C:/compiler/
COPY --from=build C:/src/service/runner/install-runtime.ps1 C:/compiler/install-runtime.ps1
RUN powershell -NoProfile -File C:/compiler/install-runtime.ps1
USER ContainerUser
ENTRYPOINT ["C:\\compiler\\CompilerJob.exe"]

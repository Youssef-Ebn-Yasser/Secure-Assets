FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Shared/Shared.csproj src/Shared/
COPY src/Workers/FfmpegWorker/FfmpegWorker.csproj src/Workers/FfmpegWorker/
RUN dotnet restore src/Workers/FfmpegWorker/FfmpegWorker.csproj

COPY src/Shared/ src/Shared/
COPY src/Workers/FfmpegWorker/ src/Workers/FfmpegWorker/
WORKDIR /app/src/Workers/FfmpegWorker
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "FfmpegWorker.dll"]

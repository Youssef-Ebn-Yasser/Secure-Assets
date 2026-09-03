FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Shared/Shared.csproj src/Shared/
COPY src/Workers/PdfWorker/PdfWorker.csproj src/Workers/PdfWorker/
RUN dotnet restore src/Workers/PdfWorker/PdfWorker.csproj

COPY src/Shared/ src/Shared/
COPY src/Workers/PdfWorker/ src/Workers/PdfWorker/
WORKDIR /app/src/Workers/PdfWorker
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends libgdiplus libc6-dev libfontconfig1 && rm -rf /var/lib/apt/lists/*
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "PdfWorker.dll"]

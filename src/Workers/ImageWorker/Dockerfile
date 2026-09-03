FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Shared/Shared.csproj src/Shared/
COPY src/Workers/ImageWorker/ImageWorker.csproj src/Workers/ImageWorker/
RUN dotnet restore src/Workers/ImageWorker/ImageWorker.csproj

COPY src/Shared/ src/Shared/
COPY src/Workers/ImageWorker/ src/Workers/ImageWorker/
WORKDIR /app/src/Workers/ImageWorker
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "ImageWorker.dll"]

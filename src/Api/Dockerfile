FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj files and restore dependencies
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj

# Copy source files and build release
COPY src/Shared/ src/Shared/
COPY src/Api/ src/Api/
WORKDIR /app/src/Api
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Api.dll"]

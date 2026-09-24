FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY .git .git/
COPY version.json ./
COPY src/server .

RUN dotnet publish Stefan.Server.API/Stefan.Server.API.csproj \
    --configuration Release \
    --output /app/publish

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Stefan.Server.API.dll"]

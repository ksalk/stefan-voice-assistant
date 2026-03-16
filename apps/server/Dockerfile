FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish src/Stefan.Server.API/Stefan.Server.API.csproj \
    --configuration Release \
    --output /app/publish

# Copy model files that are not part of the publish output
RUN cp src/Stefan.Server.API/ggml-base.bin /app/publish/ && \
    cp -r src/Stefan.Server.API/vosk-model-full /app/publish/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Stefan.Server.API.dll"]

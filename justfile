# https://just.systems

dbup:
    docker compose --profile db up -d

runserver:
    dotnet run --project apps/server/src/Stefan.Server.API

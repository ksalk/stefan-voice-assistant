# https://just.systems

dbup:
    docker compose --profile db up -d

migrate-add-stefan NAME:
    dotnet ef migrations add {{NAME}} --project src/server/Stefan.Server.Infrastructure --context StefanDbContext --output-dir Migrations/StefanDb

migrate-add-tools NAME:
    dotnet ef migrations add {{NAME}} --project src/server/Stefan.Server.Infrastructure --context ToolsDbContext --output-dir Migrations/ToolsDb

migrate-update:
    dotnet ef database update --project src/server/Stefan.Server.Infrastructure --context StefanDbContext
    dotnet ef database update --project src/server/Stefan.Server.Infrastructure --context ToolsDbContext

buildserver:
    dotnet build --project src/server/Stefan.Server.API
    
runserver:
    dotnet run --project src/server/Stefan.Server.API

buildnodeimage:
    docker buildx build --platform linux/arm64 -t stefan-node -f src/node/Dockerfile . --load

publishnode VERSION:
    docker buildx build --platform linux/arm64 --pull \
      -t git.harnas.top/ksalk/stefan-voice-assistant:{{VERSION}} \
      -t git.harnas.top/ksalk/stefan-voice-assistant:latest \
      -f src/node/Dockerfile . --push

buildnode:
    dotnet build --project src/node/Stefan.Node

runnode:
    dotnet run --project src/node/Stefan.Node

runnodetest:
    dotnet run --project src/node/Stefan.Node --send-file ../../../tests/Stefan.Node.IntegrationTests/TestAudioFiles/how-much-longer.wav

runnodeplaytest FILEPATH:
    dotnet run --project src/node/Stefan.Node --play-file {{FILEPATH}}

runui:
    pnpm --prefix src/dashboard/stefan-ui run dev

buildnodetestimage:
    docker build --build-arg TARGET_ARCH=linux-x64 -t stefan-node:test -f src/node/Dockerfile . --load
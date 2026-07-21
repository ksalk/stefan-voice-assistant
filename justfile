# https://just.systems

# Database
dbup:
    docker compose --profile db up -d

migrate-add-stefan NAME:
    dotnet ef migrations add {{NAME}} --project src/server/Stefan.Server.Infrastructure --context StefanDbContext --output-dir Migrations/StefanDb

migrate-add-tools NAME:
    dotnet ef migrations add {{NAME}} --project src/server/Stefan.Server.Infrastructure --context ToolsDbContext --output-dir Migrations/ToolsDb

migrate-update:
    dotnet ef database update --project src/server/Stefan.Server.Infrastructure --context StefanDbContext
    dotnet ef database update --project src/server/Stefan.Server.Infrastructure --context ToolsDbContext

# Server
buildserver:
    dotnet build --project src/server/Stefan.Server.API
    
runserver:
    dotnet run --project src/server/Stefan.Server.API

buildserverimage:
    docker buildx build --platform linux/amd64 -t stefan-server -f src/server/Dockerfile . --load

publishserver VERSION:
    docker buildx build --platform linux/amd64 --pull \
      -t git.harnas.top/ksalk/stefan-server:{{VERSION}} \
      -t git.harnas.top/ksalk/stefan-server:latest \
      -f src/server/Dockerfile . --push

buildservertestimage:
    docker buildx build --platform linux/amd64 -t stefan-server:test -f src/server/Dockerfile . --load

buildserverdbtestimage:
    docker buildx build --platform linux/amd64 -t stefan-db:test -f src/server/Stefan.Db.Dockerfile . --load

buildservertestimages: buildservertestimage buildserverdbtestimage

# Node
buildnodeimage:
    docker buildx build --platform linux/arm64 -t stefan-node -f src/node/Dockerfile . --load

publishnode VERSION:
    docker buildx build --platform linux/arm64 --pull \
      -t git.harnas.top/ksalk/stefan-node:{{VERSION}} \
      -t git.harnas.top/ksalk/stefan-node:latest \
      -f src/node/Dockerfile . --push

buildnode:
    dotnet build --project src/node/Stefan.Node

runnode:
    dotnet run --project src/node/Stefan.Node

runnodetest:
    dotnet run --project src/node/Stefan.Node --send-file ../../../tests/node/Stefan.Node.IntegrationTests/TestAudioFiles/how-much-longer.wav

runnodeplaytest FILEPATH:
    dotnet run --project src/node/Stefan.Node --play-file {{FILEPATH}}

buildnodetestimage:
    docker build --build-arg TARGET_ARCH=linux-x64 -t stefan-node:test -f src/node/Dockerfile . --load

# Dashboard
runui:
    pnpm --prefix src/dashboard/stefan-ui run dev

builddashboardimage:
    docker buildx build --platform linux/amd64 -t stefan-dashboard -f src/dashboard/stefan-ui/Dockerfile src/dashboard/stefan-ui --load

publishdashboard VERSION:
    docker buildx build --platform linux/amd64 --pull \
      -t git.harnas.top/ksalk/stefan-dashboard:{{VERSION}} \
      -t git.harnas.top/ksalk/stefan-dashboard:latest \
      -f src/dashboard/stefan-ui/Dockerfile src/dashboard/stefan-ui --push
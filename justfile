# https://just.systems

dbup:
    docker compose --profile db up -d

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
    dotnet run --project src/node/Stefan.Node --send-file ../../../src/node-python/test_commands/how_much_longer.wav

runnodeplaytest FILEPATH:
    dotnet run --project src/node/Stefan.Node --play-file {{FILEPATH}}

runui:
    pnpm --prefix apps/dashboard/stefan-ui run dev
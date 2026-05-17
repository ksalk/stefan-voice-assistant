# https://just.systems

dbup:
    docker compose --profile db up -d

buildserver:
    dotnet build --project src/server/Stefan.Server.API
    
runserver:
    dotnet run --project src/server/Stefan.Server.API

buildnodeimage:
    docker buildx build --platform linux/arm64 -t stefan-node -f src/node/Dockerfile . --load

buildnode:
    dotnet build --project src/node/Stefan.Node

runnode:
    dotnet run --project src/node/Stefan.Node

runnodetest:
    dotnet run --project src/node/Stefan.Node --send-file ../../../src/node-python/test_commands/how_much_longer.wav

runui:
    pnpm --prefix apps/dashboard/stefan-ui run dev

runnodepython:
    uv pip install -r src/node-python/requirements.txt 
    uv run src/node-python/src/main.py

runnodepythontest:
    uv venv --clear
    uv pip install -r src/node-python/requirements.txt 
    uv run src/node-python/src/main.py --test-command src/node-python/test_commands/how_much_longer.wav
# https://just.systems

dbup:
    docker compose --profile db up -d

runserver:
    dotnet run --project src/server/Stefan.Server.API

runnode:
    dotnet run --project src/node/Stefan.Node

runui:
    pnpm --prefix apps/dashboard/stefan-ui run dev

runnodepython:
    uv pip install -r src/node-python/requirements.txt 
    uv run src/node-python/src/main.py

runnodepythontest:
    uv venv --clear
    uv pip install -r src/node-python/requirements.txt 
    uv run src/node-python/src/main.py --test-command src/node-python/test_commands/how_much_longer.wav
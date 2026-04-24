# https://just.systems

dbup:
    docker compose --profile db up -d

runserver:
    dotnet run --project apps/server/src/Stefan.Server.API

runui:
    pnpm --prefix apps/dashboard run dev

runnode:
    uv pip install -r apps/node/requirements.txt 
    uv run apps/node/src/main.py

runnodetest:
    uv venv --clear
    uv pip install -r apps/node/requirements.txt 
    uv run apps/node/src/main.py --test-command apps/node/test_commands/how_much_longer.wav
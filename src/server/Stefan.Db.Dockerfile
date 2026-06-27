FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

RUN dotnet tool install --global dotnet-ef --version 10.0.9
ENV PATH="$PATH:/root/.dotnet/tools"

COPY .git .git/
COPY version.json ./
COPY src/server .

RUN dotnet restore Stefan.Server.Infrastructure/Stefan.Server.Infrastructure.csproj

RUN dotnet ef migrations script --idempotent \
      --project Stefan.Server.Infrastructure --context StefanDbContext \
      --output /app/01-stefan-migrations.sql \
    && dotnet ef migrations script --idempotent \
      --project Stefan.Server.Infrastructure --context ToolsDbContext \
      --output /app/02-tools-migrations.sql

FROM --platform=linux/amd64 postgres:16-alpine AS init
ENV POSTGRES_DB=stefan_db \
    POSTGRES_USER=stefan \
    POSTGRES_PASSWORD=changeme \
    PGDATA=/var/lib/postgresql/data

COPY --from=build /app/01-stefan-migrations.sql /app/01-stefan-migrations.sql
COPY --from=build /app/02-tools-migrations.sql /app/02-tools-migrations.sql

RUN docker-entrypoint.sh postgres & \
      SERVER_PID=$! \
      && until pg_isready -U stefan -d stefan_db 2>/dev/null; do sleep 0.5; done \
      && psql -U stefan -d stefan_db -v ON_ERROR_STOP=1 -f /app/01-stefan-migrations.sql \
      && psql -U stefan -d stefan_db -v ON_ERROR_STOP=1 -f /app/02-tools-migrations.sql \
      && kill -TERM $SERVER_PID \
      && wait $SERVER_PID

FROM --platform=linux/amd64 postgres:16-alpine
ENV POSTGRES_DB=stefan_db \
    POSTGRES_USER=stefan \
    POSTGRES_PASSWORD=changeme \
    PGDATA=/var/lib/postgresql/data

COPY --from=init /var/lib/postgresql/data /var/lib/postgresql/data
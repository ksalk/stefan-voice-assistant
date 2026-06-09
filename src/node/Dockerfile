FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGET_ARCH=linux-arm64

RUN if [ "$TARGET_ARCH" = "linux-arm64" ]; then \
      apt-get update && apt-get install -y --no-install-recommends \
        clang \
        zlib1g-dev \
        gcc-aarch64-linux-gnu \
        libc6-dev-arm64-cross \
        && rm -rf /var/lib/apt/lists/*; \
    else \
      apt-get update && apt-get install -y --no-install-recommends \
        clang \
        zlib1g-dev \
        && rm -rf /var/lib/apt/lists/*; \
    fi

WORKDIR /src

COPY .git .git/
COPY src/node/Directory.Build.props src/node/Directory.Packages.props src/node/global.json ./
COPY src/node/Stefan.Node/Stefan.Node.csproj Stefan.Node/
COPY version.json ./
RUN dotnet restore Stefan.Node/Stefan.Node.csproj -r $TARGET_ARCH

COPY src/node/Stefan.Node/ Stefan.Node/

RUN if [ "$TARGET_ARCH" = "linux-arm64" ]; then \
      ln -sf /usr/bin/aarch64-linux-gnu-objcopy /usr/local/bin/objcopy; \
    fi

RUN dotnet publish Stefan.Node/Stefan.Node.csproj \
    -c Release \
    -r $TARGET_ARCH \
    -o /app \
    --self-contained true \
    /p:StripSymbols=true \
    && rm -f /app/*.dbg

FROM debian:bookworm-slim AS models

# TODO: prune nonrequired models
RUN apt-get update && apt-get install -y --no-install-recommends curl bzip2 ca-certificates
RUN mkdir -p /models
RUN curl -fsSL https://github.com/k2-fsa/sherpa-onnx/releases/download/kws-models/sherpa-onnx-kws-zipformer-zh-en-3M-2025-12-20.tar.bz2 | tar -xj --strip-components=1 -C /models
RUN rm -rf /var/lib/apt/lists/*

FROM debian:bookworm-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
    libasound2 \
    alsa-utils \
    libstdc++6 \
    libicu72 \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app ./
COPY --from=models /models /app/models

RUN rm -f /app/appsettings.Development.json

ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["./Stefan.Node"]

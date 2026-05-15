FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

RUN apt-get update && apt-get install -y --no-install-recommends \
    clang \
    zlib1g-dev \
    gcc-aarch64-linux-gnu \
    libc6-dev-arm64-cross \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

COPY .git .git/
COPY src/node/Directory.Build.props src/node/Directory.Packages.props src/node/global.json ./
COPY src/node/Stefan.Node/Stefan.Node.csproj Stefan.Node/
COPY version.json ./
RUN dotnet restore Stefan.Node/Stefan.Node.csproj -r linux-arm64

COPY src/node/Stefan.Node/ Stefan.Node/

RUN ln -sf /usr/bin/aarch64-linux-gnu-objcopy /usr/local/bin/objcopy

RUN dotnet publish Stefan.Node/Stefan.Node.csproj \
    -c Release \
    -r linux-arm64 \
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
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app ./
COPY --from=models /models /app/models

EXPOSE 8080

ENTRYPOINT ["./Stefan.Node"]

# --- Build the IL patcher (no Valheim files involved)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS patcher-build
WORKDIR /src
COPY patcher/ ./patcher/
RUN dotnet publish ./patcher/ValheimNetPatcher.csproj -c Release -o /out

# --- Final image: extend the official Valheim server image
FROM ghcr.io/lloesche/valheim-server:latest

# Add the patcher binary and make it executable
COPY --from=patcher-build /out/ValheimNetPatcher /opt/valheim-tools/ValheimNetPatcher
COPY scripts/pre_start.sh /opt/valheim-tools/pre_start.sh
RUN chmod +x /opt/valheim-tools/ValheimNetPatcher /opt/valheim-tools/pre_start.sh

# Default queue limit (bytes). Override in compose if you want.
ENV VALHEIM_SEND_QUEUE_LIMIT=30720
# make patch output persist under /config (volume) and run on every start/update
ENV PRE_START_HOOK="/bin/sh -lc '/opt/valheim-tools/pre_start.sh'"
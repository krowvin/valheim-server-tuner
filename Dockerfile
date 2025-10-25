# --- Build the IL patcher (no Valheim files involved)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS patcher-build
WORKDIR /src
COPY patcher/ ./patcher/
RUN dotnet publish ./patcher/ValheimNetPatcher.csproj -c Release -o /out

# --- Final image: extend the official Valheim server image
FROM ghcr.io/lloesche/valheim-server:latest

# Add the patcher binary and make it executable
COPY --from=patcher-build /out/ValheimNetPatcher /opt/valheim-tools/ValheimNetPatcher
RUN chmod +x /opt/valheim-tools/ValheimNetPatcher

# Default queue limit (bytes). Override in compose if you want.
ENV VALHEIM_SEND_QUEUE_LIMIT=30720

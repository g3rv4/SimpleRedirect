ARG ARCH=
FROM mcr.microsoft.com/dotnet/sdk:8.0.100-1-alpine3.18 AS builder
WORKDIR /src
COPY src /src/
RUN dotnet publish -c Release /src -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0.0-alpine3.18-${ARCH}
ENV CONFIG_PATH=/data/config.json
WORKDIR /app
COPY --from=builder /app /app
CMD /usr/bin/dotnet SimpleRedirect.dll

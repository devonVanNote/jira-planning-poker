FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PlanningPoker.csproj .
RUN dotnet restore PlanningPoker.csproj
COPY . .
RUN dotnet publish PlanningPoker.csproj -c Release -o /app/publish --no-restore

# Show what blazor files the publish produced so we can see exactly what's there
RUN echo "=== blazor files in publish output ===" && \
    find /app/publish -name "*blazor*" 2>/dev/null | sort || true
RUN echo "=== _framework folder ===" && \
    ls /app/publish/wwwroot/_framework/ 2>/dev/null || echo "NOT FOUND"

# Strategy 1: fingerprinted copy (blazor.web.HASH.js -> blazor.web.js)
RUN find /app/publish/wwwroot/_framework -name "blazor.web.*.js" ! -name "*.map" 2>/dev/null \
    | head -1 | xargs -I{} cp {} /app/publish/wwwroot/_framework/blazor.web.js 2>/dev/null || true

# Strategy 2: copy from NuGet cache if still missing
RUN [ -f /app/publish/wwwroot/_framework/blazor.web.js ] || \
    ( mkdir -p /app/publish/wwwroot/_framework && \
      find /root/.nuget -name "blazor.web.js" 2>/dev/null | head -1 \
      | xargs -I{} cp {} /app/publish/wwwroot/_framework/blazor.web.js )

RUN echo "=== final blazor.web.js ===" && \
    ls -lh /app/publish/wwwroot/_framework/blazor.web.js 2>/dev/null || echo "STILL MISSING"

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PlanningPoker.dll"]

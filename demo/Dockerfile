FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PlanningPoker.csproj .
RUN dotnet restore PlanningPoker.csproj
COPY . .
RUN dotnet publish PlanningPoker.csproj -c Release -o /app/publish --no-restore

# .NET 9+ fingerprints blazor.web.js with a content hash (e.g. blazor.web.abc123.js).
# Copy it to the plain path so UseStaticFiles() can serve it at /_framework/blazor.web.js.
RUN find /app/publish/wwwroot/_framework -name "blazor.web.*.js" ! -name "*.map" \
    | head -1 | xargs -I{} cp {} /app/publish/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PlanningPoker.dll"]

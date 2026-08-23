# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, as its own layer, so a source-only change does not re-download packages.
COPY InventoryManagementSystem.sln ./
COPY InventoryManagementSystem/InventoryManagementSystem.csproj InventoryManagementSystem/
COPY InventoryManagementSystem.Tests/InventoryManagementSystem.Tests.csproj InventoryManagementSystem.Tests/
RUN dotnet restore InventoryManagementSystem/InventoryManagementSystem.csproj

COPY . .
RUN dotnet publish InventoryManagementSystem/InventoryManagementSystem.csproj \
    -c Release -o /app/publish --no-restore

# Runtime - aspnet:8.0 to match the target framework. A mismatched tag here is a
# classic way to ship an image that builds and then refuses to start.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

# The SQLite file lives here so a volume can be mounted over it and survive the
# container being replaced.
RUN mkdir -p /data
ENV ConnectionStrings__InventoryDb="Data Source=/data/inventory.db"
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "InventoryManagementSystem.dll"]

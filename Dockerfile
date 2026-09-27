FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy csproj and restore
COPY SegundaOportunidad/*.csproj ./SegundaOportunidad/
RUN dotnet restore SegundaOportunidad/SegundaOportunidad.csproj

# Copy everything else and build
COPY . ./
RUN dotnet publish SegundaOportunidad/SegundaOportunidad.csproj -c Release -o /app/out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

# Use the PORT environment variable provided by Render
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

# Expose port (not strictly necessary for Render as it uses the PORT env var)
EXPOSE 8080

ENTRYPOINT ["dotnet", "SegundaOportunidad.dll"]

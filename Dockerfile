FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY SegundaOportunidad/*.csproj ./SegundaOportunidad/
RUN dotnet restore SegundaOportunidad/SegundaOportunidad.csproj
COPY . ./
RUN dotnet publish SegundaOportunidad/SegundaOportunidad.csproj -c Release -o /app/out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
# PORT se evalúa al arrancar, no durante docker build. No copiar bases ni claves.
ENTRYPOINT ["sh", "-c", "exec dotnet SegundaOportunidad.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]

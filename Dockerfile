FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/Safer.Api/Safer.Api.csproj backend/Safer.Api/
RUN dotnet restore backend/Safer.Api/Safer.Api.csproj

COPY backend/Safer.Api/ backend/Safer.Api/
RUN dotnet publish backend/Safer.Api/Safer.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
COPY feadback/*.kml /app/samples/

ENV ASPNETCORE_ENVIRONMENT=Production
ENV SampleParcelsPath=/app/samples

EXPOSE 10000
ENTRYPOINT ["sh", "-c", "dotnet Safer.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]

# Build context is the REPO ROOT (not this folder), because the API project references
# sibling projects (Axpense.Core, Axpense.Data, Axpense.Infrastructure, Axpense.Service)
# that live one level up. docker-compose.prod.yml sets `context: .` and
# `dockerfile: src/Axpense.Api/Dockerfile` accordingly.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the .csproj files first so `dotnet restore` is cached across builds
# as long as dependencies don't change (i.e. code-only changes skip restore).
COPY Axpense.Core/Axpense.Core.csproj Axpense.Core/
COPY Axpense.Data/Axpense.Data.csproj Axpense.Data/
COPY Axpense.Infrastructure/Axpense.Infrastructure.csproj Axpense.Infrastructure/
COPY Axpense.Service/Axpense.Service.csproj Axpense.Service/
COPY src/Axpense.Api/Axpense.Api.csproj src/Axpense.Api/
RUN dotnet restore src/Axpense.Api/Axpense.Api.csproj

COPY Axpense.Core/ Axpense.Core/
COPY Axpense.Data/ Axpense.Data/
COPY Axpense.Infrastructure/ Axpense.Infrastructure/
COPY Axpense.Service/ Axpense.Service/
COPY src/Axpense.Api/ src/Axpense.Api/

RUN dotnet publish src/Axpense.Api/Axpense.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
# Mount point for uploaded files (docker-compose.prod.yml mounts the axpense_files volume here).
RUN mkdir -p /app/storage
ENTRYPOINT ["dotnet", "Axpense.Api.dll"]

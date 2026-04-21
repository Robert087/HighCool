FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ERP.sln ./
COPY Api/ERP.Api.csproj Api/
COPY Application/ERP.Application.csproj Application/
COPY Domain/ERP.Domain.csproj Domain/
COPY Infrastructure/ERP.Infrastructure.csproj Infrastructure/

RUN dotnet restore Api/ERP.Api.csproj

COPY Api/ Api/
COPY Application/ Application/
COPY Domain/ Domain/
COPY Infrastructure/ Infrastructure/

WORKDIR /src/Api
RUN dotnet publish ERP.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "ERP.Api.dll"]

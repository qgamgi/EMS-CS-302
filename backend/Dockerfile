FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EMS.API/EMS.API.csproj ./EMS.API/
RUN dotnet restore ./EMS.API/EMS.API.csproj

COPY EMS.API/ ./EMS.API/
RUN dotnet publish ./EMS.API/EMS.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "EMS.API.dll"]

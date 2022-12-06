#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

ARG Nuget_CustomFeedUserName
ARG Nuget_CustomFeedPassword

WORKDIR /src
COPY ["src/Gateway/src/Spirebyte.APIGateway.csproj", "src/Gateway/src/"]
RUN dotnet nuget add source --username $Nuget_CustomFeedUserName --password $Nuget_CustomFeedUserName --store-password-in-clear-text --name github "https://nuget.pkg.github.com/Spirebyte-Project-Management/index.json"
RUN dotnet restore "src/Gateway/src/Spirebyte.APIGateway.csproj"
COPY . .
WORKDIR "/src/src/Gateway/src"
RUN dotnet build "Spirebyte.APIGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Spirebyte.APIGateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Spirebyte.APIGateway.dll"]

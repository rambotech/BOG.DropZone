FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 80

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY ["BOG.DropZone/BOG.DropZone.csproj", "BOG.DropZone/"]
RUN dotnet restore "BOG.DropZone/BOG.DropZone.csproj"
COPY . .
WORKDIR "/src/BOG.DropZone"
RUN dotnet build "BOG.DropZone.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "BOG.DropZone.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "BOG.DropZone.dll"]

# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /app

# Copy the project files and restore any dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the remaining files and build the app
COPY . .
RUN dotnet publish -c Release -o out

# Use the official .NET runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app
COPY --from=build /app/out .

EXPOSE 8080

ENTRYPOINT ["dotnet", "YTBackgroundBackend.dll"]
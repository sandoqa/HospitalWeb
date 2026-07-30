FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore

RUN dotnet publish -c Release -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app

COPY --from=build /app/publish .


# نسخ قاعدة البيانات SQLite
COPY HospitalWeb/App_Data/hospital.db /app/App_Data/hospital.db


EXPOSE 8080


ENTRYPOINT ["dotnet", "HospitalWeb.dll"]
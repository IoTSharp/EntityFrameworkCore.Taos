#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
RUN apt-get  -y   -q update   && apt-get install  -y  --no-install-recommends  -q  apt-utils lsof net-tools wget curl iputils-ping inetutils-tools && \
	apt-get autoremove -y &&  apt-get clean  &&  apt-get autoclean && rm  /var/cache/apt/* -rf && ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime
RUN curl -o tdengine-tsdb-oss-client.tar.gz "https://downloads.taosdata.com/tdengine-tsdb-oss/3.3.8.8/tdengine-tsdb-oss-client-3.3.8.8-linux-x64.tar.gz" && \
    tar  -xvf tdengine-tsdb-oss-client.tar.gz && rm tdengine-tsdb-oss-client.tar.gz -f  && cd  $(ls tdengine-tsdb-oss-client*  -d)  && \
     ./install_client.sh && \
    rm $(pwd) -rf
WORKDIR /app
 
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Example/Example.csproj", "src/Example/"]
COPY ["src/IoTSharp.Data.Taos/IoTSharp.Data.Taos.csproj", "src/IoTSharp.Data.Taos/"]
COPY ["src/LICENSE", "LICENSE"]
RUN dotnet restore "src/Example/Example.csproj"
COPY . .
WORKDIR "/src/src/Example"
RUN dotnet build "Example.csproj" --no-restore  -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Example.csproj" --no-restore  -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Example.dll"]

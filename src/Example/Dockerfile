#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim AS base
MAINTAINER Yanhong Ma 2024
RUN echo "deb https://mirrors.tuna.tsinghua.edu.cn/debian/ bookworm main contrib non-free" > /etc/apt/sources.list && \
	echo "deb https://mirrors.tuna.tsinghua.edu.cn/debian/ bookworm-updates main contrib non-free" >> /etc/apt/sources.list && \
	echo "deb https://mirrors.tuna.tsinghua.edu.cn/debian/ bookworm-backports main contrib non-free" >> /etc/apt/sources.list && \
    apt-get  -y   -q update   && apt-get install  -y  --no-install-recommends  -q  apt-utils lsof net-tools wget curl iputils-ping inetutils-tools && \
	apt-get autoremove -y &&  apt-get clean  &&  apt-get autoclean && rm  /var/cache/apt/* -rf && ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime
RUN curl -o TDengine-client.tar.gz "https://www.taosdata.com/assets-download/3.0/TDengine-client-3.2.1.0-Linux-x64.tar.gz" && \
    tar  -xvf TDengine-client.tar.gz && rm TDengine-client.tar.gz -f  && cd  $(ls TDengine-client*  -d)  && \
     ./install_client.sh && \
    rm $(pwd) -rf
WORKDIR /app
 
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY ["src/Example/Example.csproj", "src/Example/"]
COPY ["src/IoTSharp.Data.Taos/IoTSharp.Data.Taos.csproj", "src/IoTSharp.Data.Taos/"]
COPY ["src/EFCore.Taos.Core/IoTSharp.EntityFrameworkCore.Taos.csproj", "src/EFCore.Taos.Core/"]
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

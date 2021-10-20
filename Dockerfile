FROM mcr.microsoft.com/dotnet/sdk:5.0 as builder
WORKDIR /src
COPY . /src
RUN cd Wittyer \
    && dotnet publish -f netcoreapp2.0 -r linux-x64 -c Release -o /output \
    && chmod +x /output/Wittyer

FROM mcr.microsoft.com/dotnet/runtime:5.0
LABEL git_repository=https://git.illumina.com/DASTE/Ilmn.Das.App.Wittyer.git
WORKDIR /opt/Wittyer
RUN apt-get -y update && apt-get -y install tabix libunwind8
COPY --from=builder /output /opt/Wittyer
ENTRYPOINT ["/opt/Wittyer/Wittyer"]

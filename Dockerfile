FROM microsoft/dotnet:2.0-sdk as builder
WORKDIR /src
COPY . /src
RUN dotnet publish -f netcoreapp2.0 -r linux-x64 -c Release -o /output \
      && chmod +x /output/Wittyer
      
FROM microsoft/dotnet:2.0.9-runtime
LABEL git_repository=https://git.illumina.com/DASTE/Ilmn.Das.App.Wittyer.git
WORKDIR /opt/Wittyer
RUN apt-get -y update && apt-get -y install tabix
COPY --from=builder /output /opt/Wittyer
ENTRYPOINT ["/opt/Wittyer/Wittyer"]

#!/bin/sh

set -eou pipefail

git_branch=$(git symbolic-ref -q --short HEAD || git describe --tags --exact-match) # or tag
git_commit=$(git rev-parse HEAD)
docker_tag=$(echo $git_branch | awk -F'/' '{print $NF}')

docker build --label git_commit=$git_commit -t docker-general.dockerhub.illumina.com/wittyer:$docker_tag .

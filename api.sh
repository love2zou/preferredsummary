#!/bin/bash
# 进入源码根目录
#cd /usr/src/preferredsummary
# 拉取代码
#git fetch --all  
#git reset --hard origin/main
#git pull 'git@github.com:love2zou/preferredsummary.git'
set -euo pipefail
cd /usr/src/preferredsummary/api

IMAGE_TAG="preferred_new.image.icuok:latest"
CONTAINER_NAME="preferred_new.api"
DOCKERFILE="api_dockerfile"

echo "== Stop & remove old container =="
docker rm -f "${CONTAINER_NAME}" 2>/dev/null || true

echo "== Remove old images (optional) =="
# 仅删除同 repo 的旧镜像（保留最新 tag 也会被替换）
repo="${IMAGE_TAG%%:*}"
old_ids=$(docker images --format "{{.Repository}}:{{.Tag}} {{.ID}}" | awk -v r="$repo" '$1 ~ "^"r":" {print $2}' | sort -u)
if [ -n "${old_ids}" ]; then
  docker rmi --force ${old_ids} 2>/dev/null || true
fi

echo "== Build (with cache + BuildKit) =="
export DOCKER_BUILDKIT=1
# --pull：更新基础镜像；不要用 --no-cache（会让 apt 每次重装）
docker build \
  --pull \
  -f "${DOCKERFILE}" \
  -t "${IMAGE_TAG}" \
  . \
  --network=host

echo "== Free port 8080 if occupied by other container =="
if docker ps --filter "publish=8080" --format "{{.ID}}" | grep -q .; then
  echo "Detected container occupying port 8080, stopping/removing..."
  docker ps --filter "publish=8080" --format "{{.ID}}" | xargs -r docker rm -f
fi

echo "== Run =="
# 重要：如果 Dockerfile 里 ASPNETCORE_URLS=http://+:8080
# 那么这里应该映射 8080:8080（而不是 8080:80）
docker run --name="${CONTAINER_NAME}" \
  -p 8080:80 \
  -v /etc/upload:/app/wwwroot/upload \
  -v /etc/localtime:/etc/localtime \
  --user root \
  --restart=always \
  -dit \
  "${IMAGE_TAG}"

echo "== Images =="
docker images | head -n 20

echo "== Containers =="
docker ps | head -n 20

echo "== Logs (tail) =="
docker logs --tail=200 -f "${CONTAINER_NAME}"
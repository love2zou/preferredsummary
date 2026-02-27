#!/bin/bash
# 进入源码根目录
#cd /usr/src/preferredsummary
# 拉取代码
#git fetch --all  
#git reset --hard origin/main
#git pull 'git@github.com:love2zou/preferredsummary.git'
set -euo pipefail
# 切换到api目录 - 修复路径问题
cd /usr/src/preferredsummary/api

IMAGE_TAG="preferred_new.image.icuok"
CONTAINER_NAME="preferred_new.api"

echo "== Stop & remove old container =="
docker rm -f "${CONTAINER_NAME}" 2>/dev/null || true

echo "== Remove old images (optional) =="
# 只删除同名镜像的旧版本（避免误删别的）
old_images=$(docker images --format "{{.Repository}}:{{.Tag}} {{.ID}}" | awk -v repo="${IMAGE_TAG%%:*}" '$1 ~ repo {print $2}')
if [ -n "${old_images}" ]; then
  docker rmi --force ${old_images} 2>/dev/null || true
fi

echo "== Build (no-cache) =="
docker build --no-cache -f api_dockerfile -t "${IMAGE_TAG}" . --network=host

echo "== Free port 8080 if occupied by other container =="
if docker ps --filter "publish=8080" --format "{{.ID}}" | grep -q .; then
  echo "Detected container occupying port 8080, stopping/removing..."
  docker ps --filter "publish=8080" --format "{{.ID}}" | xargs -r docker rm -f
fi

echo "== Run =="
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
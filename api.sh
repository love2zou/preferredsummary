#!/bin/bash
# 进入源码根目录
#cd /usr/src/preferredsummary
# 拉取代码
#git fetch --all  
#git reset --hard origin/main
#git pull 'git@github.com:love2zou/preferredsummary.git'
# 切换到api目录 - 修复路径问题
cd /usr/src/preferredsummary/api
# docker login -u love2zou -p zq7790161
# 停止并删除旧容器（添加错误处理）
docker stop preferred_new.api 2>/dev/null || true
docker rm preferred_new.api 2>/dev/null || true
# 按条件删除镜像（添加存在性检查）
old_images=$(docker images | grep preferred_new.image | awk '{print $3}')
if [ ! -z "$old_images" ]; then
    docker rmi --force $old_images
fi
# 构建镜像
imtag=$(uuidgen |sed 's/-//g')
docker build -f api_dockerfile -t preferred_new.image.${imtag} . --network=host
# 检测并释放 8080 端口
if docker ps --filter "publish=8080" --format "{{.ID}}" | grep -q .; then
    echo "检测到有容器占用 8080 端口，正在停止并删除..."
    docker ps --filter "publish=8080" --format "{{.ID}}" | xargs -r docker stop
    docker ps -a --filter "publish=8080" --format "{{.ID}}" | xargs -r docker rm
fi

# 运行容器 - 添加ASPNETCORE_URLS环境变量
docker run --name=preferred_new.api -p 8080:80 -v /etc/upload:/app/wwwroot/upload -v /etc/localtime:/etc/localtime --user root -dit --restart=always -d preferred_new.image.${imtag}
# docker run --add-host=yzmzq.cloud:159.75.184.108 --name=preferred_new.api -p 8080:8080 -v /etc/localtime:/etc/localtime -dit --restart=always -d preferred_new.image.${imtag}
# 查看镜像和容器
docker images
docker ps
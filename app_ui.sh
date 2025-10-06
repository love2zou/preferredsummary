#!/bin/bash
# 进入源码根目录
#cd /usr/src/preferredsummary
# 拉取代码
#git fetch --all  
#git reset --hard origin/main
#git pull 'git@github.com:love2zou/preferredsummary.git'

# 2. 配置Docker镜像加速器（如果还没配置）
sudo mkdir -p /etc/docker
sudo tee /etc/docker/daemon.json <<-'EOF'
{
  "registry-mirrors": [
    "https://registry.cn-hangzhou.aliyuncs.com",
    "https://mirror.ccs.tencentyun.com"
  ]
}
EOF
sudo systemctl daemon-reload
sudo systemctl restart docker

# 切换到ui目录
cd /usr/src/preferredsummary/ui

# 停止并删除旧容器
docker stop preferred_reservation.ui 2>/dev/null || true
docker rm preferred_reservation.ui 2>/dev/null || true

# 按条件删除镜像
old_images=$(docker images | grep preferred_reservation.ui.image | awk '{print $3}')
if [ ! -z "$old_images" ]; then
    docker rmi --force $old_images
fi

# 构建镜像
imtag=$(uuidgen |sed 's/-//g')
docker build -f reservation_dockerfile -t preferred_reservation.ui.image.${imtag} . --network=host

# 运行容器
docker run --name=preferred_reservation.ui -p 8090:8090 -v /etc/localtime:/etc/localtime --user root -dit --restart=always -d preferred_reservation.ui.image.${imtag}

# 查看镜像和容器
docker images
docker ps
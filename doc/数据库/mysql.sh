# !/bin/bash
#进入mysql的配置目录
cd /root/mysql/conf
rm -rf my.cnf
ls
echo "删除my.cnf文件成功" 
#生成my.cnf文件
touch /root/mysql/conf/my.cnf
# 生成自定义的my.cnf文件
echo "[mysqld]" > my.cnf
echo "datadir = /var/lib/mysql" > my.cnf
echo "socket = /var/run/mysqld/mysqld.sock" > my.cnf
echo "port = 3306" > my.cnf
echo "key_buffer_size = 64M" >> my.cnf
echo "max_connections = 200" >> my.cnf
echo "innodb_buffer_pool_size = 256M" >> my.cnf
echo "character-set-server = utf8mb4" >> my.cnf
echo "lower_case_table_names = 1" >> my.cnf
echo "[mysql]" > my.cnf
echo "default-character-set = utf8mb4" > my.cnf
#停止并删除旧容器
docker stop mysql.docker
docker rm mysql.docker
# 运行MySQL容器，并设置root密码和映射端口，同时指定自定义的my.cnf文件
docker run -p 3306:3306 --restart=always --name mysql.docker -v /root/mysql/conf/my.cnf:/etc/mysql/conf.d/my.cnf -v /root/mysql/logs:/var/log/mysql -v /root/mysql/data:/var/lib/mysql -e MYSQL_ROOT_PASSWORD=devpwd -d mysql:8.0.32
#查看镜像和容器
docker images
docker ps
# 等待MySQL容器启动
sleep 30
# 进入MySQL容器
docker exec -it mysql.docker bash

# 在MySQL中创建数据库用户和数据库
mysql -u root -p"devpwd" <<EOF
GRANT ALL ON *.* TO 'root'@'%';
flush privileges;
CREATE USER 'devuser'@'%' IDENTIFIED BY 'devpwd';
GRANT ALL ON *.* TO 'devuser'@'%';
flush privileges;
ALTER USER 'devuser'@'%' IDENTIFIED WITH mysql_native_password BY 'devpwd';
flush privileges;
select user,host,plugin from mysql.user;
CREATE DATABASE IF NOT EXISTS Db_PreferredURL CHARACTER SET UTF8;
GRANT ALL PRIVILEGES ON Db_PreferredURL.* TO 'devuser'@'%';
FLUSH PRIVILEGES;
EOF
USE Db_PreferredData;

/* 给地址表增加：类型字段-UrlType
　　table_name ：表名；
　　column_name：需要添加的字段名；
　　VARCHAR(100)：字段类型为varchar，长度100；
　　DEFAULT NULL：默认值NULL；
　　AFTER old_column：新增字段添加在old_column字段后面。
*/
ALTER TABLE Tb_SystemResource ADD COLUMN HostName VARCHAR(100) NOT NULL COMMENT '主机名称' AFTER Id;
ALTER TABLE Tb_SystemResource ADD COLUMN DiskName VARCHAR(100) NOT NULL COMMENT '系统盘名称' AFTER MemoryUsage;
ALTER TABLE Tb_Notification ADD COLUMN SendStatus INT DEFAULT 0 NOT NULL COMMENT '发送状态(0 未发送, 1 已发送, 2 发送失败)' AFTER NotifyStatus;

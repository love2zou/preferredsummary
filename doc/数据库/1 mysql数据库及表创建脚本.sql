/*==============================================================*/
/* DATABASE: Db_PreferredData   优选地址                                         */
/*==============================================================*/
CREATE DATABASE IF NOT EXISTS Db_PreferredData CHARACTER SET UTF8;
/*==============================================================*/
/* Table-1: Tb_User   用户表                               */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_User (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   UserName            	VARCHAR(50)           NOT NULL COMMENT '用户名',
   Email                VARCHAR(100)          NULL     COMMENT '邮箱',
   PasswordHash         VARCHAR(100)          NOT NULL COMMENT '密码',
   Salt			        VARCHAR(50)           NOT NULL COMMENT 'hash加密数',
   PhoneNumber 		    VARCHAR(20)           NULL     COMMENT '电话号码',
   ProfilePictureUrl    NVARCHAR(500)         NULL     COMMENT '个人头像',
   Bio                  NVARCHAR(500)         NULL     COMMENT '个人简介',
   UserTypeCode         VARCHAR(50)           NOT NULL COMMENT '用户类型代码',
   UserToSystemCode     VARCHAR(50)           NOT NULL COMMENT '用户所属系统代码',
   IsActive             BIGINT                NOT NULL COMMENT '用户状态（禁用、锁定、正常）',
   IsEmailVerified      BIGINT                NOT NULL COMMENT '邮箱验证码确认状态',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间',
   LastLoginTime          DATETIME              NULL COMMENT '最后登录时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;

/*==============================================================*/
/* Table-1: Tb_NetWorkURL   地址表                               */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_NetWorkURL (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   Url            		NVARCHAR(100)         NOT NULL COMMENT '域名地址',
   NAME                 VARCHAR(100)          NOT NULL COMMENT '名称',
   Description          NVARCHAR(200)         NULL     COMMENT '描述',
   ImageCode			VARCHAR(50)           NOT NULL COMMENT '图片代码',
   CategoryCode 		VARCHAR(50)           NOT NULL COMMENT '分类代码',
   IsAvailable          BIGINT                NULL     COMMENT '是否可用',
   IsMark               BIGINT                NOT NULL COMMENT '是否推荐',
   TagCodeType          NVARCHAR(200)         NOT NULL COMMENT '标签类型',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;

/*==============================================================*/
/* Table-2: Tb_SystemResource   系统资源占用表                     */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_SystemResource (
    Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
	HostName			 VARCHAR(100)          NOT NULL COMMENT '主机名称',
    CpuUsage             DECIMAL(5,2)          NOT NULL COMMENT 'CPU占用百分比，保留两位小数',
    MemoryUsage          DECIMAL(5,2)          NOT NULL COMMENT '内存占用百分比，保留两位小数',
	DiskName		     VARCHAR(100)          NOT NULL COMMENT '系统盘名称',
    DiskUsage            DECIMAL(5,2)          NOT NULL COMMENT '磁盘占用百分比，保留两位小数',
    DiskTotal            BIGINT                NOT NULL COMMENT '总磁盘容量，单位字节',
    DiskUsed             BIGINT                NOT NULL COMMENT '已使用磁盘容量，单位字节',
    DiskFree             BIGINT                NOT NULL COMMENT '可用磁盘容量，单位字节',
	CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
    UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;
/*==============================================================*/
/* Table-3: Tb_Notification  通知表                                */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_Notification (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   IsRead 			    INT    DEFAULT 0      NOT NULL COMMENT '是否已读(0 未读, 1 已读)',
   Name 			    NVARCHAR(36)          NOT NULL COMMENT '标题',
   Content 			    NVARCHAR(400)         NOT NULL COMMENT '内容',
   NotifyType 			NVARCHAR(20)          NOT NULL COMMENT '通知类型(接口)',
   NotifyStatus 		INT    DEFAULT 0      NOT NULL COMMENT '通知状态(0 正常, 1 错误, 2 告警)',
   SendTime 			DATETIME              NOT NULL COMMENT '发送时间',
   SendUser             NVARCHAR(100)         NOT NULL COMMENT '发送人',
   Receiver             NVARCHAR(100)         NOT NULL COMMENT '接收人',
   Remark               NVARCHAR(200)         NULL     COMMENT '备注',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;

/*==============================================================*/
/* Table-4: Tb_Tag  标签表                                */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_Tag (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   ParName 			    NVARCHAR(50)          NOT NULL COMMENT '应用模块',
   TagCode 			    NVARCHAR(20)          NOT NULL COMMENT '标签代码',
   TagName 			    NVARCHAR(50)          NOT NULL COMMENT '标签名称',
   HexColor 			VARCHAR(10)           NOT NULL COMMENT '标签字体颜色(如：#EF0011)',
   RgbColor 			VARCHAR(50)           NOT NULL COMMENT '标签背景色(如：rgb(1,32,12,0.1))',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;
/*==============================================================*/
/* Table-5: Tb_CategoryMenu  分类管理表                                */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_CategoryMenu (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   CategoryCode 		VARCHAR(50)          NOT NULL COMMENT '分类代码',
   CategoryName 		VARCHAR(100)          NOT NULL COMMENT '分类名称',
   CategoryIcon 		VARCHAR(50)          NOT NULL COMMENT '分类图标',
   Description 			NVARCHAR(500)         NULL COMMENT '描述',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;
/*==============================================================*/
/* Table1: Tb_Pictures  图片数据源表                                */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_Pictures (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   AppType				VARCHAR(20)           NOT NULL COMMENT '应用类型',
   ImageCode			VARCHAR(50)           NOT NULL COMMENT '图片代码',
   ImageName			VARCHAR(100)          NOT NULL COMMENT '图片名称',
   ImagePath 			NVARCHAR(400)         NOT NULL COMMENT '图片路径',
   AspectRatio			VARCHAR(20)           NOT NULL COMMENT '图片宽高比',
   Width                INT                   NULL COMMENT '图片宽度(px)',
   Height               INT                   NULL COMMENT '图片高度(px)',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;
/*==============================================================*/
/* Table1: Tb_FileRecord  文件记录表                              */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_FileRecord (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   FileName 			NVARCHAR(128)         NOT NULL COMMENT '文件名称',
   FileType 			VARCHAR(10)           NOT NULL COMMENT '文件类型',
   FilePath             NVARCHAR(500)         NOT NULL COMMENT '文件路径',
   FileSize 			VARCHAR(100)          NOT NULL COMMENT '文件大小(Mb)',
   Description          NVARCHAR(500)         NULL COMMENT '文件描述',
   UploadUserId         INT    DEFAULT 0      NOT NULL COMMENT '用户ID',
   AppType				VARCHAR(20)           NOT NULL COMMENT '应用类型',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;
/*==============================================================*/
/* Table1: Tb_ScheduledTask  定时任务表                              */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_ScheduledTask (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   Name                 VARCHAR(100)          NOT NULL COMMENT '任务名称',
   Code                 VARCHAR(100)          NOT NULL UNIQUE COMMENT '任务编码（唯一标识）',
   Cron                 VARCHAR(50)           NOT NULL COMMENT 'Cron 表达式',
   Handler              VARCHAR(255)          NOT NULL COMMENT '执行处理器（类名或URL或脚本路径）',
   Parameters           TEXT                  COMMENT '执行参数（可选，JSON 格式）',
   Enabled              BOOLEAN               NOT NULL DEFAULT TRUE COMMENT '是否启用',
   LastRunTime          DATETIME              DEFAULT NULL COMMENT '最后运行时间',
   NextRuntime          DATETIME              DEFAULT NULL COMMENT '下次运行时间',
   Remark               VARCHAR(255)          DEFAULT NULL COMMENT '备注',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;
/*==============================================================*/
/* Table1: Tb_ScheduledTaskLog  定时任务执行日志表                              */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_ScheduledTaskLog (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   TaskId               INT                   NOT NULL COMMENT '任务ID',
   TaskCode             VARCHAR(100)          NOT NULL COMMENT '任务编码（冗余记录）',
   StartTime            DATETIME              NOT NULL COMMENT '开始时间',
   EndTime              DATETIME              DEFAULT NULL COMMENT '结束时间',
   Success              BOOLEAN               NOT NULL DEFAULT FALSE COMMENT '是否成功',
   Message              TEXT                  COMMENT '执行结果或异常信息',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;
/*==============================================================*/
/* 健身预约: Tb_CoachMemberRelation  教练会员关系表                              */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_CoachMemberRelation (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   CoachId            	INT                   NOT NULL COMMENT '教练Id',
   MemberId             INT                   NOT NULL COMMENT '会员Id',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间',
)ENGINE=INNODB DEFAULT CHARSET=utf8;
/*==============================================================*/
/* 健身预约: Tb_BookTask  预约课程表                              */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_BookTask (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   CoachId            	INT                   NOT NULL COMMENT '教练Id',
   MemberId             INT                   NOT NULL COMMENT '会员Id',
   BookDate             DATETIME              NOT NULL COMMENT '预约日期',
   BookTimeSlot         VARCHAR(20)           NOT NULL COMMENT '预约时间段',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8;
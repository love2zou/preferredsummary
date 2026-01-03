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
   IsActive             INT                NOT NULL COMMENT '用户状态（禁用、锁定、正常）',
   IsEmailVerified      INT                NOT NULL COMMENT '邮箱验证码确认状态',
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
   IsAvailable          INT                NULL     COMMENT '是否可用',
   IsMark               INT                NOT NULL COMMENT '是否推荐',
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

/*==============================================================*/
/* 录波分析: Tb_ZwavFile  ZWAV原始文件表                          */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_ZwavFile (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识',
   OriginalName 	 	VARCHAR(255)         NOT NULL COMMENT '原始文件名',
   StoragePath          VARCHAR(1024)        NOT NULL COMMENT '文件存储路径',
   ExtractPath          VARCHAR(1024)        NOT NULL COMMENT '解压路径',
   FileSize             BIGINT                NOT NULL COMMENT '文件大小(b)',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
)ENGINE=INNODB DEFAULT CHARSET=utf8mb4 COMMENT='ZWAV文件信息表';
/*==============================================================*/
/* 录波分析: Tb_ZwavAnalysis  ZWAV解析任务表                      */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_ZwavAnalysis (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
   AnalysisGuid         VARCHAR(36)         NOT NULL COMMENT '解析任务唯一标识(GUID)',
   Status               VARCHAR(16)         NOT NULL COMMENT '解析状态(Queued/Parsing/Ready/Failed)',
   Progress             INT      DEFAULT 0  NOT NULL COMMENT '解析进度(0-100)',
   ErrorMessage         TEXT                NULL COMMENT '错误信息',
   FileId               INT              NOT NULL COMMENT '关联文件ID',
   TotalRecords         BIGINT              NULL COMMENT 'DAT总记录数',
   RecordSize           INT                 NULL COMMENT '单条记录字节长度',
   DigitalWords         INT                 NULL COMMENT '数字量字长度',
   StartTime            DATETIME            NULL COMMENT '解析开始时间',
   FinishTime           DATETIME            NULL COMMENT '解析完成时间',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='ZWAV解析任务主表';
/*==============================================================*/
/* 录波分析: Tb_ZwavCfg  CFG配置解析表                            */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_ZwavCfg (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
   AnalysisId            INT              NOT NULL COMMENT '解析任务ID',
   FullCfgText           TEXT                NULL COMMENT 'cfg全文',
   StationName           VARCHAR(128)        NULL COMMENT '站名',
   DeviceId              VARCHAR(128)        NULL COMMENT '装置编号',
   Revision              VARCHAR(64)         NULL COMMENT 'CFG版本',
   AnalogCount           INT      DEFAULT 0  NOT NULL COMMENT '模拟量通道数',
   DigitalCount          INT      DEFAULT 0  NOT NULL COMMENT '数字量通道数',
   FrequencyHz           DECIMAL(10,3)       NULL COMMENT '系统频率(Hz)',
   TimeMul               DECIMAL(18,8)       NULL COMMENT '时间倍率',
   StartTimeRaw          VARCHAR(64)         NULL COMMENT '开始时间(原始字符串)',
   TriggerTimeRaw        VARCHAR(64)         NULL COMMENT '触发时间(原始字符串)',
   FormatType            VARCHAR(16)         NULL COMMENT '数据格式(BINARY/ASCII)',
   DataType              VARCHAR(16)         NULL COMMENT '数据类型(INT16/INT32等)',
   SampleRateJson        JSON                NULL COMMENT '采样率段配置(JSON)',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='CFG配置解析表';
/*==============================================================*/
/* 录波分析: Tb_ZwavChannel  通道定义表                           */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_ZwavChannel (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
   AnalysisId           INT              NOT NULL COMMENT '解析任务ID',
   ChannelIndex         INT                 NOT NULL COMMENT '通道序号(1-based)',
   ChannelType          VARCHAR(16)         NOT NULL COMMENT '通道类型(Analog/Digital/Virtual)',
   ChannelCode          VARCHAR(64)         NULL COMMENT '通道编码(Ia/Ua/Trip/3I0等)',
   ChannelName          VARCHAR(128)        NULL COMMENT '通道名称',
   Phase                VARCHAR(16)         NULL COMMENT '相别(A/B/C/N)',
   Unit                 VARCHAR(32)         NULL COMMENT '单位',
   RatioA               DECIMAL(18,8)       NULL COMMENT '比例系数A',
   OffsetB              DECIMAL(18,8)       NULL COMMENT '偏移量B',
   Skew                 DECIMAL(18,8)       NULL COMMENT '采样偏移',
   IsEnable             INT      DEFAULT 1  NOT NULL COMMENT '是否启用(0 否,1 是)',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='ZWAV通道定义表';
/*==============================================================*/
/* 录波分析: Tb_ZwavHdr  HDR解析结果表                            */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_ZwavHdr (
   Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
   AnalysisId           INT              NOT NULL COMMENT '解析任务ID',
   FaultStartTime       VARCHAR(50)            NULL COMMENT '故障发生时间',
   FaultKeepingTime     VARCHAR(50)         NULL COMMENT '故障持续时间',
   DeviceInfoJson       TEXT                NULL COMMENT '设备信息(JSON)',
   TripInfoJSON         TEXT       			NULL COMMENT '保护动作信息(JSON)',
   FaultInfoJson        TEXT                NULL COMMENT '故障信息(JSON)',
   DigitalStatusJson    TEXT                NULL COMMENT '启动时切换状态(JSON)',
   DigitalEventJson     TEXT                NULL COMMENT '启动后变化信息(JSON)',
   SettingValueJson     TEXT                NULL COMMENT '设备设置信息(JSON)',
   RelayEnaValueJSON    TEXT                NULL COMMENT '继电保护“软压板”投入状态值(JSON)',
   SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
   UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='HDR解析结果表';
/*==============================================================*/
/* 录波分析: Tb_ZwavData  波形数据表                   */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_ZwavData (
    Id INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
	AnalysisId INT NOT NULL COMMENT '解析任务ID',
    SampleNo INT NOT NULL COMMENT '样本号',
    TimeRaw INT NOT NULL COMMENT '原始时间戳',
    -- 模拟量通道（根据要求最大70个通道）
    Channel1 DOUBLE COMMENT '模拟量通道1',
    Channel2 DOUBLE COMMENT '模拟量通道2',
    Channel3 DOUBLE COMMENT '模拟量通道3',
    Channel4 DOUBLE COMMENT '模拟量通道4',
    Channel5 DOUBLE COMMENT '模拟量通道5',
    Channel6 DOUBLE COMMENT '模拟量通道6',
    Channel7 DOUBLE COMMENT '模拟量通道7',
    Channel8 DOUBLE COMMENT '模拟量通道8',
    Channel9 DOUBLE COMMENT '模拟量通道9',
    Channel10 DOUBLE COMMENT '模拟量通道10',
    Channel11 DOUBLE COMMENT '模拟量通道11',
    Channel12 DOUBLE COMMENT '模拟量通道12',
    Channel13 DOUBLE COMMENT '模拟量通道13',
    Channel14 DOUBLE COMMENT '模拟量通道14',
    Channel15 DOUBLE COMMENT '模拟量通道15',
    Channel16 DOUBLE COMMENT '模拟量通道16',
    Channel17 DOUBLE COMMENT '模拟量通道17',
    Channel18 DOUBLE COMMENT '模拟量通道18',
    Channel19 DOUBLE COMMENT '模拟量通道19',
    Channel20 DOUBLE COMMENT '模拟量通道20',
    Channel21 DOUBLE COMMENT '模拟量通道21',
    Channel22 DOUBLE COMMENT '模拟量通道22',
    Channel23 DOUBLE COMMENT '模拟量通道23',
    Channel24 DOUBLE COMMENT '模拟量通道24',
    Channel25 DOUBLE COMMENT '模拟量通道25',
    Channel26 DOUBLE COMMENT '模拟量通道26',
    Channel27 DOUBLE COMMENT '模拟量通道27',
    Channel28 DOUBLE COMMENT '模拟量通道28',
    Channel29 DOUBLE COMMENT '模拟量通道29',
    Channel30 DOUBLE COMMENT '模拟量通道30',
    Channel31 DOUBLE COMMENT '模拟量通道31',
    Channel32 DOUBLE COMMENT '模拟量通道32',
    Channel33 DOUBLE COMMENT '模拟量通道33',
    Channel34 DOUBLE COMMENT '模拟量通道34',
    Channel35 DOUBLE COMMENT '模拟量通道35',
    Channel36 DOUBLE COMMENT '模拟量通道36',
    Channel37 DOUBLE COMMENT '模拟量通道37',
    Channel38 DOUBLE COMMENT '模拟量通道38',
    Channel39 DOUBLE COMMENT '模拟量通道39',
    Channel40 DOUBLE COMMENT '模拟量通道40',
    Channel41 DOUBLE COMMENT '模拟量通道41',
    Channel42 DOUBLE COMMENT '模拟量通道42',
    Channel43 DOUBLE COMMENT '模拟量通道43',
    Channel44 DOUBLE COMMENT '模拟量通道44',
    Channel45 DOUBLE COMMENT '模拟量通道45',
    Channel46 DOUBLE COMMENT '模拟量通道46',
    Channel47 DOUBLE COMMENT '模拟量通道47',
    Channel48 DOUBLE COMMENT '模拟量通道48',
    Channel49 DOUBLE COMMENT '模拟量通道49',
    Channel50 DOUBLE COMMENT '模拟量通道50',
    Channel51 DOUBLE COMMENT '模拟量通道51',
    Channel52 DOUBLE COMMENT '模拟量通道52',
    Channel53 DOUBLE COMMENT '模拟量通道53',
    Channel54 DOUBLE COMMENT '模拟量通道54',
    Channel55 DOUBLE COMMENT '模拟量通道55',
    Channel56 DOUBLE COMMENT '模拟量通道56',
    Channel57 DOUBLE COMMENT '模拟量通道57',
    Channel58 DOUBLE COMMENT '模拟量通道58',
    Channel59 DOUBLE COMMENT '模拟量通道59',
    Channel60 DOUBLE COMMENT '模拟量通道60',
    Channel61 DOUBLE COMMENT '模拟量通道61',
    Channel62 DOUBLE COMMENT '模拟量通道62',
    Channel63 DOUBLE COMMENT '模拟量通道63',
    Channel64 DOUBLE COMMENT '模拟量通道64',
    Channel65 DOUBLE COMMENT '模拟量通道65',
    Channel66 DOUBLE COMMENT '模拟量通道66',
    Channel67 DOUBLE COMMENT '模拟量通道67',
    Channel68 DOUBLE COMMENT '模拟量通道68',
    Channel69 DOUBLE COMMENT '模拟量通道69',
    Channel70 DOUBLE COMMENT '模拟量通道70',
    -- 数字量通道
    DigitalWords VARBINARY(100) COMMENT '数字量字（bitset，最多支持 800bit）',
	SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号',
	CrtTime              DATETIME              NOT NULL COMMENT '创建时间',
	UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='ZWAV波形数据表';
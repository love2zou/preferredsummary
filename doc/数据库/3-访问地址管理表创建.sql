CREATE TABLE IF NOT EXISTS Tb_NetWorkURL ( 
    Id                   INT PRIMARY KEY AUTO_INCREMENT COMMENT '唯一标识', 
    Url            		 NVARCHAR(100)         NOT NULL COMMENT '域名地址', 
    NAME                 VARCHAR(100)          NOT NULL COMMENT '名称', 
    Description          NVARCHAR(200)         NULL     COMMENT '描述', 
    ImageCode 			 VARCHAR(50)           NOT NULL COMMENT '图片代码', 
    IconUrl              NVARCHAR(200)         NULL     COMMENT '图标地址', 
    IsAvailable          BIGINT                NULL     COMMENT '是否可用', 
    IsMark               BIGINT                NOT NULL COMMENT '是否推荐', 
    TagCodeType          NVARCHAR(200)         NOT NULL COMMENT '标签类型', 
    SeqNo                INT    DEFAULT 0      NOT NULL COMMENT '排序号', 
    CrtTime              DATETIME              NOT NULL COMMENT '创建时间', 
    UpdTime              DATETIME              NOT NULL COMMENT '最后修改时间' 
 )ENGINE=INNODB DEFAULT CHARSET=utf8;
USE Db_PreferredData;

/*==============================================================*/
/* 暂降分析任务表                                                */
/*==============================================================*/
CREATE TABLE IF NOT EXISTS Tb_ZwavSagTask (
   Id                    INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
   TaskNo                VARCHAR(64)        NOT NULL COMMENT '任务编号',
   TaskName              VARCHAR(100)       NULL COMMENT '任务名称',
   SourceType            VARCHAR(32)        NOT NULL COMMENT '任务来源：Manual 手动，TripEvent 跳闸事件',
   Status                TINYINT            NOT NULL COMMENT '任务状态：0接收中，1已关闭待收尾，2已完成，3已完成但有失败',
   Progress              INT DEFAULT 0      NOT NULL COMMENT '任务进度(0-100)',
   IsClosed              TINYINT(1) DEFAULT 0 NOT NULL COMMENT '是否已手动关闭',
   ClosedTime            DATETIME           NULL COMMENT '手动关闭时间',
   StartParseTime        DATETIME           NULL COMMENT '开始解析时间',
   FinishParseTime       DATETIME           NULL COMMENT '结束解析时间',
   LastReceiveTime       DATETIME           NULL COMMENT '最近一次接收录波文件时间',
   ReferenceType         VARCHAR(32)        NULL COMMENT '参考电压处理方式',
   ReferenceVoltage      DECIMAL(18,6)      NULL COMMENT '参考电压值',
   SagThresholdPct       DECIMAL(10,3)      NULL COMMENT '暂降阈值(%)',
   RecoverThresholdPct   DECIMAL(10,3)      NULL COMMENT '恢复阈值(%)',
   InterruptThresholdPct DECIMAL(10,3)      NULL COMMENT '中断阈值(%)',
   HysteresisPct         DECIMAL(10,3)      NULL COMMENT '迟滞(%)',
   MinDurationMs         DECIMAL(12,3)      NULL COMMENT '最小持续时间(毫秒)',
   ReceivedFileCount     INT DEFAULT 0      NOT NULL COMMENT '已接收录波文件数量',
   FinishedFileCount     INT DEFAULT 0      NOT NULL COMMENT '已完成自动解析数量',
   SuccessFileCount      INT DEFAULT 0      NOT NULL COMMENT '自动解析成功数量',
   FailedFileCount       INT DEFAULT 0      NOT NULL COMMENT '自动解析失败数量',
   PendingFileCount      INT DEFAULT 0      NOT NULL COMMENT '待自动解析数量',
   TotalParseMs          BIGINT DEFAULT 0   NOT NULL COMMENT '累计解析耗时(毫秒)',
   ErrorMessage          TEXT               NULL COMMENT '最近一次错误信息',
   SeqNo                 INT DEFAULT 0      NOT NULL COMMENT '排序号',
   CrtTime               DATETIME           NOT NULL COMMENT '创建时间',
   UpdTime               DATETIME           NOT NULL COMMENT '最后修改时间',
   UNIQUE KEY UK_ZwavSagTask_TaskNo (TaskNo),
   KEY IX_ZwavSagTask_IsClosed_CrtTime (IsClosed, CrtTime)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='暂降分析任务表';

/*==============================================================*/
/* Tb_ZwavSagEvent 合并任务字段并删除未使用字段                  */
/*==============================================================*/
ALTER TABLE Tb_ZwavSagEvent
    ADD COLUMN IF NOT EXISTS TaskId INT NULL COMMENT '所属暂降分析任务ID' AFTER FileId,
    ADD COLUMN IF NOT EXISTS AnalysisId INT NULL COMMENT '录波解析任务ID' AFTER TaskId,
    ADD COLUMN IF NOT EXISTS AnalysisGuid VARCHAR(64) NULL COMMENT '录波解析任务GUID' AFTER AnalysisId,
    DROP COLUMN IF EXISTS IsMergedStatEvent,
    DROP COLUMN IF EXISTS MergeGroupId,
    DROP COLUMN IF EXISTS RawEventCount,
    DROP COLUMN IF EXISTS Remark;

ALTER TABLE Tb_ZwavSagEvent
    ADD INDEX IX_ZwavSagEvent_Task_Status_CrtTime (TaskId, Status, CrtTime),
    ADD INDEX IX_ZwavSagEvent_AnalysisId (AnalysisId),
    ADD INDEX IX_ZwavSagEvent_AnalysisGuid (AnalysisGuid);

# 健身私教预约系统扩展设计

## 识别出的页面批次

### 第 1 批 会员端核心预约闭环
- 首页
- 私教列表
- 教练详情
- 预约教练
- 预约确认
- 我的预约
- 课程详情 / 签到
- 课程评价
- 我的课程
- 个人中心

### 第 2 批 预约上课与评价流程
- 预约成功
- 取消预约
- 取消成功
- 到店签到
- 签到成功
- 课程完成
- 课程评价
- 联系教练

### 第 3 批 课程购买支付订单
- 课程购买
- 课包详情
- 订单确认
- 支付方式
- 支付结果
- 我的订单
- 订单详情
- 优惠券选择

### 第 4 批 会员资料训练数据
- 个人资料
- 编辑资料
- 健身目标
- 身体数据
- 数据趋势
- 训练记录
- 训练计划
- 消息通知

### 第 5 批 教练端管理流程
- 教练工作台
- 教练排班
- 预约审核
- 学员列表
- 学员详情
- 课程记录
- 训练计划制定
- 教练消息中心

## 新增后端接口

控制器：`api/ReservationPortal`

- `GET /api/ReservationPortal/member/flow?memberId=1`
  - 返回预约成功、签到、评价、聊天等流程页聚合数据

- `GET /api/ReservationPortal/member/commerce?memberId=1`
  - 返回课程商城、订单、优惠券数据

- `GET /api/ReservationPortal/member/center?memberId=1`
  - 返回个人资料、身体数据、训练记录、训练计划、通知数据

- `GET /api/ReservationPortal/coach/workbench?coachUserId=1`
  - 返回教练工作台、排班、审核、学员、记录、消息聚合数据

## 新增数据表

- `Tb_ReservationCoursePackage`
- `Tb_ReservationCourseOrder`
- `Tb_ReservationCoupon`
- `Tb_ReservationMemberCoupon`
- `Tb_ReservationBodyMetric`
- `Tb_ReservationTrainingRecord`
- `Tb_ReservationTrainingPlan`
- `Tb_ReservationTrainingPlanItem`
- `Tb_ReservationConversationMessage`
- `Tb_ReservationCheckInRecord`

## 代码落点

- `Models/ReservationPortal.cs`
- `Services/IReservationPortalService.cs`
- `Services/ReservationPortalService.cs`
- `Controllers/ReservationPortalController.cs`
- `Services/ReservationDemoService.cs`
- `Data/ApplicationDbContext.cs`

## 当前说明

- 已补充演示种子数据写入逻辑，覆盖课包、课程订单、优惠券、身体数据、训练记录、训练计划、会话消息、签到记录。
- 已通过 `dotnet build -o D:\github\preferredsummary\.build\reservation-api-verify` 验证新增代码可编译。
- 尚未执行 EF Migration 或数据库更新命令；如需真正落库，可在数据库环境确认后继续补迁移并执行。

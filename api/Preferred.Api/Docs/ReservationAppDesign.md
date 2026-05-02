# Reservation App API & DB Design

## 目标

本设计服务于 `ui/reservation-ui` 的 6 个核心页面：

- 首页
- 私教列表
- 教练详情
- 预约教练
- 我的预约
- 我的

后端设计基于 `Preferred.Api` 现有的 `Controller + Service + DbContext + EF Core(MySQL)` 框架实现，并保留与现有 `tb_user`、`Tb_CoachMemberRelation` 的兼容关系。

## 新增数据表

### 1. `Tb_ReservationClub`

用于门店/会所基础信息。

核心字段：

- `Id`
- `ClubCode`
- `ClubName`
- `City`
- `District`
- `Address`
- `BusinessHours`
- `IsActive`
- `SeqNo`
- `CrtTime`
- `UpdTime`

### 2. `Tb_ReservationTrainerProfile`

用于教练展示档案，和 `tb_user.Id` 通过 `UserId` 关联。

核心字段：

- `Id`
- `UserId`
- `ClubId`
- `DisplayName`
- `Title`
- `Gender`
- `YearsOfExperience`
- `Rating`
- `ReviewCount`
- `ServedClients`
- `Satisfaction`
- `BasePrice`
- `TrainingArea`
- `Highlight`
- `Introduction`
- `Story`
- `HeroImageUrl`
- `HeroTone`
- `AccentTone`
- `IsRecommended`
- `IsActive`
- `SeqNo`
- `CrtTime`
- `UpdTime`

### 3. `Tb_ReservationTrainerTag`

用于教练目标、擅长方向、徽章、资质等标签扩展。

核心字段：

- `Id`
- `TrainerProfileId`
- `TagType`
- `TagName`
- `SeqNo`
- `CrtTime`
- `UpdTime`

建议 `TagType` 枚举值：

- `Goal`
- `Specialty`
- `Badge`
- `Certification`

### 4. `Tb_ReservationTrainerSessionType`

用于教练可售课程类型。

核心字段：

- `Id`
- `TrainerProfileId`
- `SessionCode`
- `SessionName`
- `Description`
- `DurationMinutes`
- `Price`
- `IsActive`
- `SeqNo`
- `CrtTime`
- `UpdTime`

### 5. `Tb_ReservationTrainerScheduleSlot`

用于教练可预约日历与时间段。

核心字段：

- `Id`
- `TrainerProfileId`
- `ClubId`
- `ScheduleDate`
- `StartTime`
- `EndTime`
- `IsAvailable`
- `SeqNo`
- `CrtTime`
- `UpdTime`

### 6. `Tb_ReservationMemberPackage`

用于会员课包与剩余课时。

核心字段：

- `Id`
- `MemberId`
- `ClubId`
- `PackageName`
- `MembershipName`
- `TotalSessions`
- `RemainingSessions`
- `EffectiveDate`
- `ExpireDate`
- `StatusCode`
- `SeqNo`
- `CrtTime`
- `UpdTime`

建议 `StatusCode` 枚举值：

- `Active`
- `Expired`
- `Frozen`

### 7. `Tb_ReservationOrder`

用于预约订单主表。

核心字段：

- `Id`
- `ReservationNo`
- `MemberId`
- `TrainerProfileId`
- `ClubId`
- `SessionTypeId`
- `ScheduleSlotId`
- `ReservationDate`
- `StartTime`
- `EndTime`
- `PriceAmount`
- `StatusCode`
- `Remark`
- `CancelTime`
- `CompleteTime`
- `SeqNo`
- `CrtTime`
- `UpdTime`

建议 `StatusCode` 枚举值：

- `Upcoming`
- `Completed`
- `Cancelled`

### 8. `Tb_ReservationReview`

用于教练详情页评价。

核心字段：

- `Id`
- `ReservationOrderId`
- `TrainerProfileId`
- `MemberId`
- `AuthorName`
- `Rating`
- `ReviewTag`
- `Content`
- `IsVisible`
- `SeqNo`
- `CrtTime`
- `UpdTime`

## 已实现 API

控制器：`api/ReservationApp`

### 门店

- `GET /api/ReservationApp/clubs?city=上海`

### 首页

- `GET /api/ReservationApp/home?memberId=1`

返回内容：

- 会员卡片信息
- 下一节预约
- 推荐教练列表

### 私教列表

- `GET /api/ReservationApp/trainers`

支持参数：

- `clubId`
- `goal`
- `gender`
- `keyword`
- `sortBy`

### 教练详情

- `GET /api/ReservationApp/trainers/{trainerId}`

返回内容：

- 教练头图信息
- 统计信息
- 目标/擅长/徽章/资质
- 课程类型
- 可预约日期与时间
- 用户评价

### 预约页

- `GET /api/ReservationApp/trainers/{trainerId}/booking?memberId=1`

返回内容：

- 教练摘要
- 课程类型
- 日期时间段
- 会员剩余课时

### 创建预约

- `POST /api/ReservationApp/reservations`

请求体：

```json
{
  "memberId": 1,
  "trainerId": 1,
  "sessionTypeId": 2,
  "reservationDate": "2026-05-18",
  "startTime": "19:00",
  "remark": "今天想重点练背"
}
```

处理逻辑：

- 校验教练、课程、课包
- 校验该时间段是否仍可预约
- 创建预约订单
- 扣减会员剩余课时
- 锁定对应排班时间段
- 自动补一条 `Tb_CoachMemberRelation`

### 我的预约

- `GET /api/ReservationApp/reservations?memberId=1`
- `GET /api/ReservationApp/reservations?memberId=1&status=upcoming`
- `GET /api/ReservationApp/reservations?memberId=1&status=completed`
- `GET /api/ReservationApp/reservations?memberId=1&status=cancelled`

### 取消预约

- `POST /api/ReservationApp/reservations/cancel`

请求体：

```json
{
  "memberId": 1,
  "reservationId": 1001
}
```

处理逻辑：

- 将订单改为 `Cancelled`
- 释放排班时间段
- 返还会员剩余课时

### 我的

- `GET /api/ReservationApp/profile?memberId=1`

返回内容：

- 会员卡片
- 待上课数量
- 已完成数量
- 已取消数量

## 前后端字段映射建议

和 `ui/reservation-ui/src/data/mockData.ts` 的主要映射关系如下：

- `trainer.name` <- `Tb_ReservationTrainerProfile.DisplayName`
- `trainer.photoUrl` <- `Tb_ReservationTrainerProfile.HeroImageUrl` 或 `tb_user.ProfilePictureUrl`
- `trainer.goals` <- `Tb_ReservationTrainerTag(TagType=Goal)`
- `trainer.specialties` <- `Tb_ReservationTrainerTag(TagType=Specialty)`
- `trainer.badges` <- `Tb_ReservationTrainerTag(TagType=Badge)`
- `trainer.certifications` <- `Tb_ReservationTrainerTag(TagType=Certification)`
- `trainer.sessionTypes` <- `Tb_ReservationTrainerSessionType`
- `trainer.availableDates` <- `Tb_ReservationTrainerScheduleSlot`
- `reservations` <- `Tb_ReservationOrder`
- `user.remainingSessions` <- `Tb_ReservationMemberPackage.RemainingSessions`

## 代码落点

本次实现涉及：

- `Models/ReservationApp.cs`
- `Services/IReservationAppService.cs`
- `Services/ReservationAppService.cs`
- `Controllers/ReservationAppController.cs`
- `Data/ApplicationDbContext.cs`
- `Extensions/ServiceCollectionExtensions.cs`

## 下一步建议

- 补充 EF Migration，把新增表真正落库
- 初始化一批门店、教练、课包、排班、评价测试数据
- 将 `ui/reservation-ui` 中的 mock store 替换为真实接口请求

<template>
  <div class="right-panel">
    <el-tabs type="border-card" class="info-tabs">
      <el-tab-pane label="CFG 信息">
        <el-scrollbar height="calc(100vh - 180px)">
          <div v-if="cfgData" class="info-list">
            <div class="info-item"><label>厂站名:</label> <span>{{ cfgData.stationName }}</span></div>
            <div class="info-item"><label>设备ID:</label> <span>{{ cfgData.deviceId }}</span></div>
            <div class="info-item"><label>版本:</label> <span>{{ cfgData.revision }}</span></div>
            <div class="info-item"><label>模拟量数:</label> <span>{{ cfgData.analogCount }}</span></div>
            <div class="info-item"><label>开关量数:</label> <span>{{ cfgData.digitalCount }}</span></div>
            <div class="info-item"><label>频率:</label> <span>{{ cfgData.frequencyHz }} Hz</span></div>
            <div class="info-item"><label>时间倍率:</label> <span>{{ cfgData.timeMul }}</span></div>
            <div class="info-item"><label>启动时间:</label> <span>{{ cfgData.startTimeRaw }}</span></div>
            <div class="info-item"><label>触发时间:</label> <span>{{ cfgData.triggerTimeRaw }}</span></div>
            <div class="info-item"><label>数据格式:</label> <span>{{ cfgData.formatType }}</span></div>
            <div class="info-item"><label>数据类型:</label> <span>{{ cfgData.dataType }}</span></div>

            <div class="info-section" v-if="cfgData.sampleRateJson">
              <h4>采样率配置</h4>
              <pre>{{ formatJson(cfgData.sampleRateJson) }}</pre>
            </div>

            <div class="info-section" v-if="cfgData.fullCfgText">
              <h4>CFG 全文</h4>
              <pre>{{ cfgData.fullCfgText }}</pre>
            </div>
          </div>
        </el-scrollbar>
      </el-tab-pane>

      <el-tab-pane label="HDR 信息">
        <el-scrollbar height="calc(100vh - 180px)">
          <div v-if="hdrData" class="info-list">
            <el-collapse v-model="activeHdrNames" class="hdr-collapse">
              <el-collapse-item title="设备信息" name="1" v-if="hdrData.deviceInfoJson && hdrData.deviceInfoJson.length">
                <el-table :data="hdrData.deviceInfoJson" size="small" border>
                  <el-table-column type="index" label="序号" width="50" align="center" />
                  <el-table-column prop="name" label="名称" width="120" />
                  <el-table-column prop="value" label="值" />
                </el-table>
              </el-collapse-item>

              <el-collapse-item title="故障信息" name="3" v-if="hdrData.faultInfoJson && hdrData.faultInfoJson.length">
                <el-table :data="hdrData.faultInfoJson" size="small" border>
                  <el-table-column type="index" label="序号" width="50" align="center" />
                  <el-table-column prop="name" label="名称" width="160" />
                  <el-table-column label="值">
                    <template #default="{ row }">
                      {{ row.value }} {{ row.unit }}
                    </template>
                  </el-table-column>
                </el-table>
              </el-collapse-item>

              <el-collapse-item title="保护动作信息" name="2" v-if="tripInfoArray.length">
                <div style="margin-bottom: 10px; padding: 0 10px;">
                  <div class="info-item"><label>故障开始时间:</label> <span>{{ hdrData.faultStartTime }}</span></div>
                  <div class="info-item"><label>故障持续时间:</label> <span>{{ hdrData.faultKeepingTime }}</span></div>
                </div>
                <el-table :data="tripInfoArray" size="small" border>
                  <el-table-column type="index" label="序号" width="50" align="center" />
                  <el-table-column prop="time" label="时间" width="70" />
                  <el-table-column prop="phase" label="相位" width="50" align="center" />
                  <el-table-column label="保护动作">
                    <template #default="{ row }">
                      {{ row.name }} {{ row.value === '1' ? '动作' : (row.value === '0' ? '复归' : row.value) }}
                    </template>
                  </el-table-column>
                </el-table>
              </el-collapse-item>

              <el-collapse-item title="启动时切换状态" name="4" v-if="hdrData.digitalStatusJson && hdrData.digitalStatusJson.length">
                <el-table :data="hdrData.digitalStatusJson" size="small" border>
                  <el-table-column type="index" label="序号" width="50" align="center" />
                  <el-table-column prop="name" label="名称" width="180" />
                  <el-table-column prop="value" label="状态" align="center" />
                </el-table>
              </el-collapse-item>

              <el-collapse-item title="启动后变化信息" name="5" v-if="hdrData.digitalEventJson && hdrData.digitalEventJson.length">
                <el-table :data="hdrData.digitalEventJson" size="small" border>
                  <el-table-column type="index" label="序号" width="50" align="center" />
                  <el-table-column prop="time" label="时间" width="70" />
                  <el-table-column prop="name" label="名称" width="120" />
                  <el-table-column label="状态">
                    <template #default="{ row }">
                      {{ row.value === '1' ? '0 => 1' : (row.value === '0' ? '1 => 0' : row.value) }}
                    </template>
                  </el-table-column>
                </el-table>
              </el-collapse-item>

              <el-collapse-item title="设备设置信息" name="6" v-if="hdrData.settingValueJson && hdrData.settingValueJson.length">
                <el-table :data="hdrData.settingValueJson" size="small" border>
                  <el-table-column type="index" label="序号" width="50" align="center" />
                  <el-table-column prop="name" label="名称" width="160" />
                  <el-table-column label="值">
                    <template #default="{ row }">
                      {{ row.value }} {{ row.unit }}
                    </template>
                  </el-table-column>
                </el-table>
              </el-collapse-item>

              <el-collapse-item
                title="继电保护“软压板”投入状态值"
                name="7"
                v-if="hdrData.relayEnaValueJSON && hdrData.relayEnaValueJSON.length"
              >
                <el-table :data="hdrData.relayEnaValueJSON" size="small" border>
                  <el-table-column type="index" label="序号" width="60" align="center" />
                  <el-table-column prop="name" label="名称" width="180" />
                  <el-table-column prop="value" label="值" />
                </el-table>
              </el-collapse-item>
            </el-collapse>
          </div>
          <div v-else class="empty">暂无 HDR 信息</div>
        </el-scrollbar>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { CfgDto, HdrDto } from '@/services/zwavService'

const props = defineProps<{
  cfgData: CfgDto | null
  hdrData: HdrDto | null
  tripInfoArray: any[]
}>()

const activeHdrNames = ref(['1', '2', '3', '4', '5', '6', '7'])

const formatJson = (jsonStr: string) => {
  try {
    if (!jsonStr) return ''
    const obj = JSON.parse(jsonStr)
    return JSON.stringify(obj, null, 2)
  } catch {
    return jsonStr
  }
}
</script>

<style scoped>
.right-panel {
  flex: 1;
  background-color: #fff;
  border-radius: 4px;
  box-shadow: 0 2px 12px 0 rgba(0, 0, 0, 0.1);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.info-tabs {
  height: 100%;
  border: none;
  box-shadow: none;
}

.info-tabs :deep(.el-tabs__content) {
  padding: 15px;
  height: calc(100% - 40px);
  overflow: hidden;
}

.info-list {
  font-size: 13px;
}

.info-item {
  margin-bottom: 10px;
  display: flex;
  border-bottom: 1px solid #ebeeF5;
  padding-bottom: 5px;
}

.info-item label {
  color: #909399;
  width: 100px;
  flex-shrink: 0;
}

.info-item span {
  color: #303133;
  word-break: break-all;
}

.info-section {
  margin-top: 20px;
}
.info-section h4 {
  margin: 0 0 10px;
  font-size: 14px;
  color: #303133;
  border-left: 3px solid #409eff;
  padding-left: 8px;
}

.hdr-collapse {
  border-top: none;
  border-bottom: none;
}

.hdr-collapse :deep(.el-collapse-item__header) {
  font-size: 14px;
  font-weight: bold;
  color: #303133;
  border-bottom: 1px solid #ebeeF5;
}
.hdr-collapse :deep(.el-collapse-item__content) {
  padding-bottom: 20px;
}

pre {
  background-color: #f4f4f5;
  padding: 10px;
  border-radius: 4px;
  font-family: monospace;
  white-space: pre-wrap;
  word-wrap: break-word;
  color: #606266;
  font-size: 12px;
}

.empty {
  color: #909399;
  text-align: center;
  margin-top: 20px;
}
</style>

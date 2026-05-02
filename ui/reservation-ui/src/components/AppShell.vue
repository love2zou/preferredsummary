<template>
  <div class="app-shell">
    <div class="app-shell__ambient app-shell__ambient--one"></div>
    <div class="app-shell__ambient app-shell__ambient--two"></div>
    <main class="device-stage">
      <section class="device-frame">
        <AppStatusBar />
        <div class="device-frame__content" :class="{ 'has-tabbar': showTabBar }">
          <slot />
        </div>
        <BottomTabBar v-if="showTabBar" />
      </section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import AppStatusBar from './AppStatusBar.vue'
import BottomTabBar from './BottomTabBar.vue'

const route = useRoute()
const showTabBar = computed(() => route.meta.showTabBar !== false)
</script>

<style scoped>
.app-shell {
  min-height: 100vh;
  padding: 28px 20px;
  position: relative;
  overflow: hidden;
  background:
    radial-gradient(circle at top left, rgba(218, 247, 225, 0.86), transparent 24%),
    radial-gradient(circle at bottom right, rgba(255, 237, 221, 0.82), transparent 24%),
    linear-gradient(180deg, #f5f6f6 0%, #eef1ef 100%);
}

.app-shell__ambient {
  position: absolute;
  border-radius: 50%;
  filter: blur(12px);
}

.app-shell__ambient--one {
  top: 60px;
  left: -40px;
  width: 180px;
  height: 180px;
  background: rgba(15, 138, 67, 0.08);
}

.app-shell__ambient--two {
  right: -20px;
  bottom: 100px;
  width: 220px;
  height: 220px;
  background: rgba(255, 122, 33, 0.08);
}

.device-stage {
  position: relative;
  z-index: 1;
  display: flex;
  justify-content: center;
}

.device-frame {
  width: min(100%, 430px);
  min-height: calc(100vh - 56px);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1.5px solid rgba(24, 24, 24, 0.12);
  border-radius: 32px;
  background: rgba(255, 255, 255, 0.96);
  box-shadow:
    0 26px 54px rgba(15, 23, 42, 0.1),
    inset 0 1px 0 rgba(255, 255, 255, 0.78);
  backdrop-filter: blur(22px);
}

.device-frame__content {
  flex: 1;
  overflow: auto;
  scrollbar-width: none;
}

.device-frame__content::-webkit-scrollbar {
  display: none;
}

.device-frame__content.has-tabbar {
  padding-bottom: 16px;
}

@media (max-width: 480px) {
  .app-shell {
    padding: 0;
  }

  .device-frame {
    min-height: 100vh;
    border: 0;
    border-radius: 0;
    box-shadow: none;
  }
}
</style>

<template>
  <div class="trainer-art" :class="`trainer-art--${variant}`" :style="toneStyle">
    <img v-if="photoUrl" class="trainer-art__photo" :src="photoUrl" :alt="name" />
    <template v-else>
      <div class="trainer-art__backdrop"></div>
      <div class="trainer-art__copy" v-if="variant === 'hero'">
        <span class="trainer-art__slogan">NEVER GIVE UP</span>
      </div>
      <div class="trainer-art__figure">
        <span class="trainer-art__head"></span>
        <span class="trainer-art__hair"></span>
        <span class="trainer-art__neck"></span>
        <span class="trainer-art__body"></span>
        <span class="trainer-art__arm trainer-art__arm--left"></span>
        <span class="trainer-art__arm trainer-art__arm--right"></span>
      </div>
    </template>
    <div class="trainer-art__overlay" v-if="variant === 'hero'"></div>
    <div class="trainer-art__label">{{ name }}</div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  name: string
  tone: string
  accent: string
  photoUrl?: string
  variant?: 'thumb' | 'card' | 'hero'
}>(), {
  variant: 'card',
  photoUrl: ''
})

const toneStyle = computed(() => ({
  '--art-tone': props.tone,
  '--art-accent': props.accent
}))
</script>

<style scoped>
.trainer-art {
  position: relative;
  overflow: hidden;
  isolation: isolate;
  background:
    radial-gradient(circle at 18% 24%, rgba(255, 255, 255, 0.2), transparent 22%),
    linear-gradient(145deg, color-mix(in srgb, var(--art-tone) 84%, #ffffff), color-mix(in srgb, var(--art-tone) 44%, #101418));
}

.trainer-art--thumb {
  width: 62px;
  height: 62px;
  border-radius: 12px;
}

.trainer-art--card {
  width: 100%;
  min-height: 96px;
  border-radius: 14px;
}

.trainer-art--hero {
  width: 100%;
  height: 228px;
  border-radius: 0;
  background:
    radial-gradient(circle at 70% 20%, rgba(255, 255, 255, 0.16), transparent 18%),
    linear-gradient(180deg, rgba(20, 20, 20, 0.12), rgba(10, 10, 10, 0.48)),
    linear-gradient(150deg, color-mix(in srgb, var(--art-tone) 18%, #131313), #1f1f1f 52%, #111111);
}

.trainer-art__photo {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.trainer-art__overlay {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(180deg, rgba(0, 0, 0, 0.12), rgba(0, 0, 0, 0.32)),
    radial-gradient(circle at 72% 18%, rgba(255, 255, 255, 0.16), transparent 18%);
}

.trainer-art__backdrop {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(90deg, transparent 0, transparent 62%, rgba(255, 255, 255, 0.05) 62%, rgba(255, 255, 255, 0.05) 64%, transparent 64%, transparent 100%),
    linear-gradient(0deg, rgba(255, 190, 92, 0.26), rgba(255, 190, 92, 0.26)) 0 58% / 100% 2px no-repeat;
  opacity: 0.8;
}

.trainer-art--thumb .trainer-art__backdrop,
.trainer-art--card .trainer-art__backdrop {
  background:
    radial-gradient(circle at 82% 18%, rgba(255, 255, 255, 0.62), transparent 20%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.05), rgba(255, 255, 255, 0));
}

.trainer-art__copy {
  position: absolute;
  top: 42px;
  right: 20px;
  text-align: right;
  z-index: 2;
}

.trainer-art__slogan {
  color: rgba(255, 255, 255, 0.42);
  font-size: 24px;
  font-weight: 800;
  line-height: 1.05;
  display: inline-block;
  width: 112px;
}

.trainer-art__figure {
  position: absolute;
  right: 12px;
  bottom: 0;
  width: 112px;
  height: 122px;
}

.trainer-art--thumb .trainer-art__figure {
  right: -1px;
  width: 54px;
  height: 58px;
}

.trainer-art--hero .trainer-art__figure {
  right: 28px;
  width: 168px;
  height: 198px;
}

.trainer-art__head,
.trainer-art__hair,
.trainer-art__neck,
.trainer-art__body,
.trainer-art__arm {
  position: absolute;
  display: block;
}

.trainer-art__head {
  top: 8px;
  left: 46px;
  width: 30px;
  height: 34px;
  border-radius: 48% 48% 46% 46%;
  background: linear-gradient(180deg, #f3d0b4, #dba177);
}

.trainer-art__hair {
  top: 3px;
  left: 44px;
  width: 34px;
  height: 16px;
  border-radius: 16px 16px 8px 8px;
  background: #161616;
}

.trainer-art__neck {
  top: 35px;
  left: 56px;
  width: 10px;
  height: 10px;
  border-radius: 4px;
  background: #d59b75;
}

.trainer-art__body {
  left: 28px;
  bottom: 0;
  width: 72px;
  height: 84px;
  border-radius: 24px 24px 8px 8px;
  background: linear-gradient(180deg, #1f1f1f, #101010);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.09);
}

.trainer-art__body::before {
  content: '';
  position: absolute;
  top: 0;
  left: 22px;
  width: 28px;
  height: 24px;
  border-radius: 0 0 18px 18px;
  background: rgba(0, 0, 0, 0.26);
}

.trainer-art__arm {
  bottom: 24px;
  width: 20px;
  height: 60px;
  border-radius: 16px;
  background: linear-gradient(180deg, #efc8aa, #ce956f);
}

.trainer-art__arm--left {
  left: 18px;
  transform: rotate(14deg);
}

.trainer-art__arm--right {
  right: 0;
  transform: rotate(-14deg);
}

.trainer-art--thumb .trainer-art__head {
  top: 3px;
  left: 24px;
  width: 14px;
  height: 16px;
}

.trainer-art--thumb .trainer-art__hair {
  top: 1px;
  left: 23px;
  width: 16px;
  height: 7px;
}

.trainer-art--thumb .trainer-art__neck {
  top: 16px;
  left: 29px;
  width: 4px;
  height: 5px;
}

.trainer-art--thumb .trainer-art__body {
  left: 11px;
  width: 34px;
  height: 37px;
  border-radius: 12px 12px 4px 4px;
}

.trainer-art--thumb .trainer-art__arm {
  bottom: 12px;
  width: 9px;
  height: 25px;
}

.trainer-art--hero .trainer-art__head {
  top: 18px;
  left: 70px;
  width: 46px;
  height: 52px;
}

.trainer-art--hero .trainer-art__hair {
  top: 11px;
  left: 66px;
  width: 52px;
  height: 21px;
}

.trainer-art--hero .trainer-art__neck {
  top: 60px;
  left: 86px;
  width: 14px;
  height: 12px;
}

.trainer-art--hero .trainer-art__body {
  left: 36px;
  width: 110px;
  height: 122px;
  border-radius: 34px 34px 10px 10px;
}

.trainer-art--hero .trainer-art__arm {
  bottom: 36px;
  width: 32px;
  height: 88px;
}

.trainer-art__label {
  position: absolute;
  left: 10px;
  bottom: 10px;
  z-index: 3;
  padding: 2px 7px;
  border-radius: 999px;
  color: rgba(255, 255, 255, 0.92);
  background: rgba(15, 23, 42, 0.26);
  backdrop-filter: blur(6px);
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.trainer-art--hero .trainer-art__label {
  left: auto;
  right: 16px;
  bottom: 16px;
  font-size: 10px;
}
</style>

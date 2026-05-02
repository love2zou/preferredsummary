<template>
  <AppShell>
    <router-view v-slot="{ Component, route }">
      <transition name="screen-slide" mode="out-in">
        <component :is="Component" :key="route.fullPath" />
      </transition>
    </router-view>
  </AppShell>
</template>

<script setup lang="ts">
import AppShell from '@/components/AppShell.vue'
</script>

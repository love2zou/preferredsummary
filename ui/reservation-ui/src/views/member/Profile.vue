<template>
  <div class="page-container">
    <div class="page-header">
      <el-icon class="back-btn" @click="goBack"><ArrowLeft /></el-icon>
      <div class="page-title">会员信息</div>
      <div class="header-placeholder"></div>
    </div>

    <div class="mini-card">
      <div class="profile-header">
        <div class="avatar">
          <el-icon><User /></el-icon>
        </div>
        <div class="user-info">
          <div class="username">{{ userStore.user?.name }}</div>
          <div class="user-phone">{{ userStore.user?.phone }}</div>
        </div>
      </div>
    </div>

    <div class="mini-card">
      <div class="menu-list">
        <div class="menu-item" @click="handleLogout">
          <span>退出登录
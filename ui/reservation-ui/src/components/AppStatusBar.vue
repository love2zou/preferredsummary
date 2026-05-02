<template>
  <div class="status-bar">
    <span class="status-bar__time">9:41</span>
    <div class="status-bar__icons">
      <span class="status-bar__network">⋮⋮</span>
      <span class="status-bar__wifi">
        <i></i>
      </span>
      <span class="status-bar__battery">
        <span class="status-bar__battery-fill"></span>
      </span>
    </div>
  </div>
</template>

<style scoped>
.status-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 20px 8px;
  color: #111827;
  font-size: 12px;
  font-weight: 700;
}

.status-bar__icons {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.status-bar__network {
  font-size: 8px;
  letter-spacing: -1px;
  transform: rotate(90deg);
}

.status-bar__wifi {
  position: relative;
  width: 14px;
  height: 10px;
}

.status-bar__wifi::before,
.status-bar__wifi::after,
.status-bar__wifi i {
  content: '';
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  border: 1.4px solid currentColor;
  border-top-left-radius: 20px;
  border-top-right-radius: 20px;
  border-bottom: 0;
}

.status-bar__wifi::before {
  bottom: 0;
  width: 12px;
  height: 7px;
}

.status-bar__wifi::after {
  bottom: 1px;
  width: 8px;
  height: 5px;
}

.status-bar__wifi i {
  bottom: 2px;
  width: 4px;
  height: 3px;
}

.status-bar__battery {
  position: relative;
  width: 22px;
  height: 11px;
  border: 1.4px solid currentColor;
  border-radius: 3px;
}

.status-bar__battery::after {
  content: '';
  position: absolute;
  top: 3px;
  right: -3px;
  width: 2px;
  height: 5px;
  border-radius: 0 2px 2px 0;
  background: currentColor;
}

.status-bar__battery-fill {
  display: block;
  width: 14px;
  height: 5px;
  margin: 2px;
  border-radius: 2px;
  background: currentColor;
}
</style>

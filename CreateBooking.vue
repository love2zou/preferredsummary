<script setup>
// ... existing code ...
// 加载可绑定教练（从用户管理拉取，前端过滤）
async function loadBindableCoaches() {
  try {
    let page = 1;
    const size = 100;
    const aggregated = [] as any[];

    while (true) {
      const resp: any = await userService.list(page, size, { isActive: true });
      const list: any[] = Array.isArray(resp?.data) ? resp.data : [];
      aggregated.push(...list);

      const totalPages = Number(resp?.totalPages ?? 1);
      if (page >= totalPages || list.length === 0) break;
      page++;
    }

    bindCandidates.value = aggregated.filter(
      (u: any) => u?.userTypeCode === 'coach' && u?.userToSystemCode === '绿色健身系统'
    );
  } catch (err) {
    console.warn('loadBindableCoaches failed:', err);
    bindCandidates.value = [];
  }
}
// ... existing code ...
</script>
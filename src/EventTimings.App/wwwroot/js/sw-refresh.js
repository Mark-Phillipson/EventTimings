window.swRefresh = {
  refreshAndBypassCache: async function() {
    try {
      if ('serviceWorker' in navigator) {
        const regs = await navigator.serviceWorker.getRegistrations();
        if (regs && regs.length) {
          await Promise.all(regs.map(r => r.unregister()));
        }
      }
    } catch (e) {
      console.error('sw-refresh error', e);
    }
    try {
      const url = new URL(location.href);
      url.searchParams.set('_swr', Date.now());
      location.replace(url.toString());
    } catch (e) {
      // fallback
      location.reload(true);
    }
  }
};

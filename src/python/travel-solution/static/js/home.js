document.addEventListener('DOMContentLoaded', () => {
  const startButton = document.getElementById('startChat');
  if (!startButton) return;

  startButton.addEventListener('click', async () => {
    try {
      const res = await fetch('/chats/new', { method: 'POST' });
      if (!res.ok) throw new Error('Unable to start chat');
      const data = await res.json();
      window.location.href = data.redirectUrl;
    } catch (err) {
      alert('Unable to start chat right now.');
      console.error(err);
    }
  });
});
